using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Services.Playback.Streaming;

namespace uYouWin.Services.Playback.Fmp4
{
    internal sealed class Fmp4StreamReader : IDisposable
    {
        private readonly HttpRangeReader _http;
        private readonly string _requiredHandler;
        private readonly bool _isVideo;

        private readonly SemaphoreSlim _sampleSemaphore =
            new SemaphoreSlim(1, 1);

        private readonly List<Fmp4Sample> _samples =
            new List<Fmp4Sample>();

        private bool _initialized;
        private bool _disposed;

        private int _currentSampleIndex;

        private TimeSpan _timelineOrigin =
            TimeSpan.Zero;

        private long _nextScanOffset;

        public Fmp4TrackInfo TrackInfo { get; private set; }

        public IReadOnlyList<Fmp4Sample> Samples
        {
            get { return _samples; }
        }

        public TimeSpan Duration { get; private set; }

        public Fmp4StreamReader(
            string url,
            string requiredHandler,
            bool isVideo)
        {
            _http =
                new HttpRangeReader(url);

            _requiredHandler =
                requiredHandler;

            _isVideo =
                isVideo;
        }

        public async Task InitializeAsync(
            CancellationToken cancellationToken)
        {
            if (_initialized)
                return;

            ThrowIfDisposed();

            TrackInfo =
                await ReadInitializationAsync(
                    cancellationToken);

            await ScanFragmentsAsync(
                cancellationToken);

            if (_samples.Count == 0)
            {
                throw new InvalidDataException(
                    "No media samples were found.");
            }

            TimeSpan lastEnd =
                _samples
                    .Select(s => s.Timestamp + s.Duration)
                    .Max();

            Duration = lastEnd;

            _initialized = true;
        }

        public void SetTimelineOrigin(
            TimeSpan origin)
        {
            _timelineOrigin = origin;
        }

        public TimeSpan GetNormalizedTimestamp(
            Fmp4Sample sample)
        {
            return sample.Timestamp -
                   _timelineOrigin;
        }

        public Fmp4SeekResult PrepareSeek(
            TimeSpan target)
        {
            if (_samples.Count == 0)
                throw new InvalidOperationException(
                    "Reader is not initialized.");

            if (!_isVideo)
            {
                int audioIndex =
                    FindFirstSampleAtOrAfter(
                        target);

                TimeSpan actual =
                    GetNormalizedTimestamp(
                        _samples[audioIndex]);

                _currentSampleIndex =
                    audioIndex;

                return new Fmp4SeekResult
                {
                    SampleIndex = audioIndex,
                    ActualStart = actual
                };
            }

            int keyframeIndex =
                FindVideoKeyframeAtOrBefore(
                    target);

            TimeSpan actualStart =
                GetNormalizedTimestamp(
                    _samples[keyframeIndex]);

            /*
             * H.264 decode order and presentation order can differ.
             *
             * Choose the minimum PTS within the current GOP so every
             * sample emitted after the seek is >= the actual start
             * position communicated to MediaStreamSource.
             */
            for (int i = keyframeIndex + 1;
                 i < _samples.Count;
                 i++)
            {
                if (i != keyframeIndex &&
                    _samples[i].IsKeyFrame)
                {
                    break;
                }

                TimeSpan timestamp =
                    GetNormalizedTimestamp(
                        _samples[i]);

                if (timestamp < actualStart)
                    actualStart = timestamp;
            }

            /*
             * Find the first decode-order sample of the selected GOP.
             */
            _currentSampleIndex =
                keyframeIndex;

            return new Fmp4SeekResult
            {
                SampleIndex =
                    keyframeIndex,

                ActualStart =
                    actualStart
            };
        }

        public async Task<Fmp4DecodedSample>
            ReadNextSampleAsync(
                CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await _sampleSemaphore.WaitAsync(
                cancellationToken);

            try
            {
                if (_currentSampleIndex >= _samples.Count)
                    return null;

                Fmp4Sample sample =
                    _samples[_currentSampleIndex];

                _currentSampleIndex++;

                byte[] data =
                    await _http.ReadAsync(
                        sample.Offset,
                        sample.Size,
                        cancellationToken);

                if (_isVideo)
                {
                    data =
                        ConvertAvcSampleToAnnexB(
                            data);
                }

                return new Fmp4DecodedSample
                {
                    Data = data,

                    Timestamp =
                        GetNormalizedTimestamp(
                            sample),

                    Duration =
                        sample.Duration,

                    IsKeyFrame =
                        sample.IsKeyFrame
                };
            }
            finally
            {
                _sampleSemaphore.Release();
            }
        }

        private async Task<Fmp4TrackInfo>
            ReadInitializationAsync(
                CancellationToken cancellationToken)
        {
            long offset = 0;

            while (true)
            {
                Mp4TopLevelBoxHeader header =
                    await ReadTopLevelHeaderAsync(
                        offset,
                        cancellationToken);

                if (header.Type == "moov")
                {
                    byte[] moov =
                        await _http.ReadAsync(
                            offset + header.HeaderSize,
                            checked((int)(
                                header.Size -
                                header.HeaderSize)),
                            cancellationToken);

                    return Fmp4TrackParser.ParseTrack(
                        moov,
                        _requiredHandler);
                }

                offset += header.Size;
            }
        }

        private async Task ScanFragmentsAsync(
            CancellationToken cancellationToken)
        {
            /*
             * Read moov again because we need the trex defaults.
             */
            Mp4TopLevelBoxHeader moovHeader =
                await FindTopLevelBoxAsync(
                    "moov",
                    cancellationToken);

            byte[] moov =
                await _http.ReadAsync(
                    _moovPayloadOffset,
                    checked((int)(
                        moovHeader.Size -
                        moovHeader.HeaderSize)),
                    cancellationToken);

            Dictionary<uint, Fmp4TrackInfo>
                trexDefaults =
                    Fmp4TrackParser.ParseTrexDefaults(
                        moov);

            Fmp4TrackInfo defaults;

            if (!trexDefaults.TryGetValue(
                    TrackInfo.TrackId,
                    out defaults))
            {
                defaults =
                    new Fmp4TrackInfo
                    {
                        TrackId =
                            TrackInfo.TrackId
                    };
            }

            TrackInfo.DefaultSampleDuration =
                defaults.DefaultSampleDuration;

            TrackInfo.DefaultSampleSize =
                defaults.DefaultSampleSize;

            TrackInfo.DefaultSampleFlags =
                defaults.DefaultSampleFlags;

            long length;

            if (!_http.Length.HasValue)
            {
                /*
                 * A metadata request will establish the total size
                 * through Content-Range on normal YouTube responses.
                 */
                await _http.ReadAsync(
                    0,
                    1,
                    cancellationToken);
            }

            if (!_http.Length.HasValue)
            {
                throw new InvalidDataException(
                    "Unable to determine media resource length.");
            }

            length =
                _http.Length.Value;

            long offset =
                _moovPayloadOffset +
                (moovHeader.Size -
                 moovHeader.HeaderSize);

            _nextScanOffset =
                offset;

            while (offset < length)
            {
                Mp4TopLevelBoxHeader header;

                try
                {
                    header =
                        await ReadTopLevelHeaderAsync(
                            offset,
                            cancellationToken);
                }
                catch (InvalidDataException)
                {
                    break;
                }

                if (header.Size <= 0)
                    break;

                if (header.Type == "moof")
                {
                    byte[] moof =
                        await _http.ReadAsync(
                            offset + header.HeaderSize,
                            checked((int)(
                                header.Size -
                                header.HeaderSize)),
                            cancellationToken);

                    long mdatOffset =
                        await FindMdatAfterMoofAsync(
                            offset + header.Size,
                            length,
                            cancellationToken);

                    Mp4TopLevelBoxHeader mdatHeader =
                        await ReadTopLevelHeaderAsync(
                            mdatOffset,
                            cancellationToken);

                    long mdatPayloadOffset =
                        mdatOffset +
                        mdatHeader.HeaderSize;

                    ParseMoof(
                        moof,
                        offset,
                        mdatPayloadOffset,
                        defaults);

                    offset =
                        mdatOffset +
                        mdatHeader.Size;
                }
                else
                {
                    offset += header.Size;
                }

                _nextScanOffset =
                    offset;
            }

            if (_samples.Count == 0)
                throw new InvalidDataException(
                    "No fMP4 fragments were found.");
        }

        private long _moovPayloadOffset;

        private async Task<Mp4TopLevelBoxHeader>
            FindTopLevelBoxAsync(
                string requestedType,
                CancellationToken cancellationToken)
        {
            long offset = 0;

            while (true)
            {
                Mp4TopLevelBoxHeader header =
                    await ReadTopLevelHeaderAsync(
                        offset,
                        cancellationToken);

                if (header.Type == requestedType)
                {
                    if (requestedType == "moov")
                    {
                        _moovPayloadOffset =
                            offset +
                            header.HeaderSize;
                    }

                    return header;
                }

                offset += header.Size;
            }
        }

        private async Task<long>
            FindMdatAfterMoofAsync(
                long startOffset,
                long length,
                CancellationToken cancellationToken)
        {
            long offset =
                startOffset;

            while (offset < length)
            {
                Mp4TopLevelBoxHeader header =
                    await ReadTopLevelHeaderAsync(
                        offset,
                        cancellationToken);

                if (header.Type == "mdat")
                    return offset;

                offset += header.Size;
            }

            throw new InvalidDataException(
                "No mdat box was found after moof.");
        }

        private async Task<Mp4TopLevelBoxHeader>
            ReadTopLevelHeaderAsync(
                long offset,
                CancellationToken cancellationToken)
        {
            byte[] first =
                await _http.ReadAsync(
                    offset,
                    8,
                    cancellationToken);

            uint size32 =
                ReadUInt32(first, 0);

            string type =
                ReadFourCC(first, 4);

            if (size32 == 1)
            {
                byte[] extended =
                    await _http.ReadAsync(
                        offset + 8,
                        8,
                        cancellationToken);

                ulong size =
                    ReadUInt64(
                        extended,
                        0);

                if (size >
                    long.MaxValue)
                {
                    throw new InvalidDataException(
                        "MP4 box is too large.");
                }

                return new Mp4TopLevelBoxHeader
                {
                    Type = type,
                    Size = (long)size,
                    HeaderSize = 16
                };
            }

            if (size32 == 0)
            {
                if (!_http.Length.HasValue)
                    throw new InvalidDataException(
                        "Zero-sized MP4 box needs known media length.");

                return new Mp4TopLevelBoxHeader
                {
                    Type = type,

                    Size =
                        _http.Length.Value -
                        offset,

                    HeaderSize = 8
                };
            }

            return new Mp4TopLevelBoxHeader
            {
                Type = type,

                Size =
                    size32,

                HeaderSize = 8
            };
        }

        private void ParseMoof(
            byte[] moofData,
            long moofOffset,
            long mdatPayloadOffset,
            Fmp4TrackInfo trexDefaults)
        {
            foreach (Mp4Box traf in
                     Mp4BoxParser.FindChildren(
                         moofData,
                         "traf"))
            {
                TfhdInfo tfhd =
                    ParseTfhd(
                        traf.Data);

                if (tfhd.TrackId != TrackInfo.TrackId)
                    continue;

                TfdtInfo tfdt =
                    ParseTfdt(
                        traf.Data);

                List<TrunInfo> truns =
                    ParseTruns(
                        traf.Data);

                if (truns.Count == 0)
                    continue;

                long baseDataOffset =
                    tfhd.HasBaseDataOffset
                        ? tfhd.BaseDataOffset
                        : (tfhd.DefaultBaseIsMoof
                            ? moofOffset
                            : mdatPayloadOffset);

                long currentDataOffset =
                    mdatPayloadOffset;

                long decodeTime =
                    tfdt.BaseMediaDecodeTime;

                foreach (TrunInfo trun in truns)
                {
                    long runOffset;

                    if (trun.HasDataOffset)
                    {
                        runOffset =
                            baseDataOffset +
                            trun.DataOffset;
                    }
                    else
                    {
                        runOffset =
                            currentDataOffset;
                    }

                    runOffset =
                        AddTrunSamples(
                            trun,
                            tfhd,
                            trexDefaults,
                            runOffset,
                            ref decodeTime);

                    currentDataOffset =
                        runOffset;
                }
            }
        }

        private long AddTrunSamples(
            TrunInfo trun,
            TfhdInfo tfhd,
            Fmp4TrackInfo trexDefaults,
            long runOffset,
            ref long decodeTime)
        {
            long currentDecodeTime =
                decodeTime;

            uint defaultDuration =
                tfhd.HasDefaultSampleDuration
                    ? tfhd.DefaultSampleDuration
                    : trexDefaults.DefaultSampleDuration;

            uint defaultSize =
                tfhd.HasDefaultSampleSize
                    ? tfhd.DefaultSampleSize
                    : trexDefaults.DefaultSampleSize;

            uint defaultFlags =
                tfhd.HasDefaultSampleFlags
                    ? tfhd.DefaultSampleFlags
                    : trexDefaults.DefaultSampleFlags;

            for (int i = 0;
                 i < trun.Samples.Count;
                 i++)
            {
                Fmp4TrunSample item =
                    trun.Samples[i];

                uint duration =
                    trun.HasSampleDuration
                        ? item.Duration
                        : defaultDuration;

                uint size =
                    trun.HasSampleSize
                        ? item.Size
                        : defaultSize;

                uint flags =
                    trun.HasSampleFlags
                        ? item.Flags
                        : defaultFlags;

                if (trun.HasFirstSampleFlags &&
                    i == 0)
                {
                    flags =
                        trun.FirstSampleFlags;
                }

                if (duration == 0)
                    throw new InvalidDataException(
                        "fMP4 sample has zero duration.");

                if (size == 0)
                    throw new InvalidDataException(
                        "fMP4 sample has zero size.");

                long dts =
                    currentDecodeTime;

                long compositionOffset =
                    item.CompositionTimeOffset;

                long pts =
                    dts +
                    compositionOffset;

                bool keyFrame =
                    !_isVideo ||
                    ((flags & 0x00010000) == 0);

                _samples.Add(
                    new Fmp4Sample
                    {
                        Offset = runOffset,
                        Size = checked((int)size),

                        Timestamp =
                            Mp4BinaryReader.ToTimeSpan(
                                pts,
                                TrackInfo.TimeScale),

                        Duration =
                            Mp4BinaryReader.ToTimeSpan(
                                duration,
                                TrackInfo.TimeScale),

                        IsKeyFrame =
                            keyFrame
                    });

                runOffset += size;
                currentDecodeTime += duration;
            }

            decodeTime =
                currentDecodeTime;

            return runOffset;
        }

        private static TfhdInfo ParseTfhd(
            byte[] trafData)
        {
            Mp4Box box =
                Mp4BoxParser.FindChild(
                    trafData,
                    "tfhd");

            if (box == null)
                throw new InvalidDataException(
                    "traf has no tfhd.");

            if (box.Data.Length < 8)
                throw new InvalidDataException(
                    "Invalid tfhd.");

            int flags =
                (box.Data[1] << 16) |
                (box.Data[2] << 8) |
                box.Data[3];

            int offset = 4;

            var result =
                new TfhdInfo
                {
                    TrackId =
                        Mp4BinaryReader.UInt32(
                            box.Data,
                            offset),

                    DefaultBaseIsMoof =
                        (flags & 0x020000) != 0
                };

            offset += 4;

            if ((flags & 0x000001) != 0)
            {
                result.HasBaseDataOffset = true;

                result.BaseDataOffset =
                    Mp4BinaryReader.Int64(
                        box.Data,
                        offset);

                offset += 8;
            }

            if ((flags & 0x000002) != 0)
            {
                offset += 4;
            }

            if ((flags & 0x000008) != 0)
            {
                result.HasDefaultSampleDuration = true;

                result.DefaultSampleDuration =
                    Mp4BinaryReader.UInt32(
                        box.Data,
                        offset);

                offset += 4;
            }

            if ((flags & 0x000010) != 0)
            {
                result.HasDefaultSampleSize = true;

                result.DefaultSampleSize =
                    Mp4BinaryReader.UInt32(
                        box.Data,
                        offset);

                offset += 4;
            }

            if ((flags & 0x000020) != 0)
            {
                result.HasDefaultSampleFlags = true;

                result.DefaultSampleFlags =
                    Mp4BinaryReader.UInt32(
                        box.Data,
                        offset);
            }

            return result;
        }

        private static TfdtInfo ParseTfdt(
            byte[] trafData)
        {
            Mp4Box box =
                Mp4BoxParser.FindChild(
                    trafData,
                    "tfdt");

            if (box == null)
            {
                return new TfdtInfo
                {
                    BaseMediaDecodeTime = 0
                };
            }

            int version =
                box.Data[0];

            if (version == 1)
            {
                return new TfdtInfo
                {
                    BaseMediaDecodeTime =
                        Mp4BinaryReader.Int64(
                            box.Data,
                            4)
                };
            }

            return new TfdtInfo
            {
                BaseMediaDecodeTime =
                    Mp4BinaryReader.UInt32(
                        box.Data,
                        4)
            };
        }

        private static List<TrunInfo>
            ParseTruns(byte[] trafData)
        {
            var result =
                new List<TrunInfo>();

            foreach (Mp4Box box in
                     Mp4BoxParser.FindChildren(
                         trafData,
                         "trun"))
            {
                result.Add(
                    ParseTrun(
                        box.Data));
            }

            return result;
        }

        private static TrunInfo ParseTrun(
            byte[] data)
        {
            if (data.Length < 8)
                throw new InvalidDataException(
                    "Invalid trun.");

            int version =
                data[0];

            int flags =
                (data[1] << 16) |
                (data[2] << 8) |
                data[3];

            uint sampleCount =
                Mp4BinaryReader.UInt32(
                    data,
                    4);

            var result =
                new TrunInfo
                {
                    Version = version,
                    Flags = flags,

                    HasDataOffset =
                        (flags & 0x000001) != 0,

                    HasFirstSampleFlags =
                        (flags & 0x000004) != 0,

                    HasSampleDuration =
                        (flags & 0x000100) != 0,

                    HasSampleSize =
                        (flags & 0x000200) != 0,

                    HasSampleFlags =
                        (flags & 0x000400) != 0,

                    HasCompositionTimeOffset =
                        (flags & 0x000800) != 0
                };

            int offset = 8;

            if (result.HasDataOffset)
            {
                result.DataOffset =
                    Mp4BinaryReader.Int32(
                        data,
                        offset);

                offset += 4;
            }

            if (result.HasFirstSampleFlags)
            {
                result.FirstSampleFlags =
                    Mp4BinaryReader.UInt32(
                        data,
                        offset);

                offset += 4;
            }

            for (uint i = 0;
                 i < sampleCount;
                 i++)
            {
                var sample =
                    new Fmp4TrunSample();

                if (result.HasSampleDuration)
                {
                    sample.Duration =
                        Mp4BinaryReader.UInt32(
                            data,
                            offset);

                    offset += 4;
                }

                if (result.HasSampleSize)
                {
                    sample.Size =
                        Mp4BinaryReader.UInt32(
                            data,
                            offset);

                    offset += 4;
                }

                if (result.HasSampleFlags)
                {
                    sample.Flags =
                        Mp4BinaryReader.UInt32(
                            data,
                            offset);

                    offset += 4;
                }

                if (result.HasCompositionTimeOffset)
                {
                    sample.CompositionTimeOffset =
                        version == 1
                            ? Mp4BinaryReader.Int32(
                                data,
                                offset)
                            : checked((int)
                                Mp4BinaryReader.UInt32(
                                    data,
                                    offset));

                    offset += 4;
                }

                result.Samples.Add(sample);
            }

            return result;
        }

        private int FindFirstSampleAtOrAfter(
            TimeSpan target)
        {
            for (int i = 0;
                 i < _samples.Count;
                 i++)
            {
                if (GetNormalizedTimestamp(
                        _samples[i]) >= target)
                {
                    return i;
                }
            }

            return _samples.Count - 1;
        }

        private int FindVideoKeyframeAtOrBefore(
            TimeSpan target)
        {
            int lastKeyframe = 0;

            for (int i = 0;
                 i < _samples.Count;
                 i++)
            {
                TimeSpan timestamp =
                    GetNormalizedTimestamp(
                        _samples[i]);

                if (timestamp > target)
                    break;

                if (_samples[i].IsKeyFrame)
                    lastKeyframe = i;
            }

            return lastKeyframe;
        }

        private byte[] ConvertAvcSampleToAnnexB(
            byte[] input)
        {
            if (TrackInfo.NaluLengthSize <= 0)
                throw new InvalidDataException(
                    "Invalid NALU length size.");

            var output =
                new List<byte>(
                    input.Length + 256);

            /*
             * SPS/PPS are required again when entering a new
             * independently decodable H.264 segment.
             */
            bool prependParameterSets =
                true;

            if (prependParameterSets)
            {
                AppendStartCode(
                    output);

                output.AddRange(
                    TrackInfo.Sps);

                AppendStartCode(
                    output);

                output.AddRange(
                    TrackInfo.Pps);
            }

            int offset = 0;

            while (offset < input.Length)
            {
                int naluLength;

                if (TrackInfo.NaluLengthSize == 4)
                {
                    if (offset + 4 > input.Length)
                        throw new InvalidDataException(
                            "Invalid H.264 sample.");

                    naluLength =
                        checked((int)
                            Mp4BinaryReader.UInt32(
                                input,
                                offset));

                    offset += 4;
                }
                else if (TrackInfo.NaluLengthSize == 2)
                {
                    if (offset + 2 > input.Length)
                        throw new InvalidDataException(
                            "Invalid H.264 sample.");

                    naluLength =
                        Mp4BinaryReader.UInt16(
                            input,
                            offset);

                    offset += 2;
                }
                else
                {
                    if (offset + 1 > input.Length)
                        throw new InvalidDataException(
                            "Invalid H.264 sample.");

                    naluLength =
                        input[offset];

                    offset++;
                }

                if (naluLength < 0 ||
                    offset + naluLength > input.Length)
                {
                    throw new InvalidDataException(
                        "H.264 NALU extends beyond sample.");
                }

                AppendStartCode(
                    output);

                for (int i = 0;
                     i < naluLength;
                     i++)
                {
                    output.Add(
                        input[offset + i]);
                }

                offset += naluLength;
            }

            return output.ToArray();
        }

        private static void AppendStartCode(
            List<byte> output)
        {
            output.Add(0x00);
            output.Add(0x00);
            output.Add(0x00);
            output.Add(0x01);
        }

        private static uint ReadUInt32(
            byte[] data,
            int offset)
        {
            return
                ((uint)data[offset] << 24) |
                ((uint)data[offset + 1] << 16) |
                ((uint)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static ulong ReadUInt64(
            byte[] data,
            int offset)
        {
            uint high =
                ReadUInt32(
                    data,
                    offset);

            uint low =
                ReadUInt32(
                    data,
                    offset + 4);

            return
                ((ulong)high << 32) |
                low;
        }

        private static string ReadFourCC(
            byte[] data,
            int offset)
        {
            return new string(
                new[]
                {
                    (char)data[offset],
                    (char)data[offset + 1],
                    (char)data[offset + 2],
                    (char)data[offset + 3]
                });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(Fmp4StreamReader));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _sampleSemaphore.Dispose();
            _http.Dispose();
        }
    }

    internal sealed class Fmp4DecodedSample
    {
        public byte[] Data { get; set; }

        public TimeSpan Timestamp { get; set; }

        public TimeSpan Duration { get; set; }

        public bool IsKeyFrame { get; set; }
    }

    internal sealed class Mp4TopLevelBoxHeader
    {
        public string Type { get; set; }

        public long Size { get; set; }

        public int HeaderSize { get; set; }
    }
}