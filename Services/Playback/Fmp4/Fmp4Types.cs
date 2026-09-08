using System;
using System.Collections.Generic;

namespace uYouWin.Services.Playback.Fmp4
{
    internal sealed class Mp4Box
    {
        public string Type { get; set; }

        public long Size { get; set; }

        public int HeaderSize { get; set; }

        public byte[] Data { get; set; }
    }

    internal sealed class Fmp4Sample
    {
        public long Offset { get; set; }

        public int Size { get; set; }

        public TimeSpan Timestamp { get; set; }

        public TimeSpan Duration { get; set; }

        public bool IsKeyFrame { get; set; }
    }

    internal sealed class Fmp4TrackInfo
    {
        public uint TrackId { get; set; }

        public string HandlerType { get; set; }

        public uint TimeScale { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double FrameRate { get; set; }

        public uint SampleRate { get; set; }

        public uint ChannelCount { get; set; }

        public int ProfileId { get; set; }

        public int NaluLengthSize { get; set; }

        public byte[] AvcConfiguration { get; set; }

        public byte[] Sps { get; set; }

        public byte[] Pps { get; set; }

        public byte[] AudioSpecificConfig { get; set; }

        public uint DefaultSampleDuration { get; set; }

        public uint DefaultSampleSize { get; set; }

        public uint DefaultSampleFlags { get; set; }

        public Fmp4TrackInfo()
        {
            HandlerType = string.Empty;
            NaluLengthSize = 4;
        }
    }

    internal sealed class TfhdInfo
    {
        public uint TrackId { get; set; }

        public bool HasBaseDataOffset { get; set; }

        public long BaseDataOffset { get; set; }

        public bool HasDefaultSampleDuration { get; set; }

        public uint DefaultSampleDuration { get; set; }

        public bool HasDefaultSampleSize { get; set; }

        public uint DefaultSampleSize { get; set; }

        public bool HasDefaultSampleFlags { get; set; }

        public uint DefaultSampleFlags { get; set; }

        public bool DefaultBaseIsMoof { get; set; }
    }

    internal sealed class TfdtInfo
    {
        public long BaseMediaDecodeTime { get; set; }
    }

    internal sealed class TrunInfo
    {
        public int Version { get; set; }

        public int Flags { get; set; }

        public bool HasDataOffset { get; set; }

        public int DataOffset { get; set; }

        public bool HasFirstSampleFlags { get; set; }

        public uint FirstSampleFlags { get; set; }

        public bool HasSampleDuration { get; set; }

        public bool HasSampleSize { get; set; }

        public bool HasSampleFlags { get; set; }

        public bool HasCompositionTimeOffset { get; set; }

        public List<Fmp4TrunSample> Samples { get; set; }

        public TrunInfo()
        {
            Samples = new List<Fmp4TrunSample>();
        }
    }

    internal sealed class Fmp4TrunSample
    {
        public uint Duration { get; set; }

        public uint Size { get; set; }

        public uint Flags { get; set; }

        public int CompositionTimeOffset { get; set; }
    }

    internal sealed class Fmp4SeekResult
    {
        public int SampleIndex { get; set; }

        public TimeSpan ActualStart { get; set; }
    }

    internal sealed class Fmp4TrackParseResult
    {
        public Fmp4TrackInfo Track { get; set; }

        public Dictionary<uint, Fmp4TrackInfo> TrexDefaults { get; set; }

        public Fmp4TrackParseResult()
        {
            TrexDefaults =
                new Dictionary<uint, Fmp4TrackInfo>();
        }
    }
}