using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using uYouWin.Models;
using uYouWin.Services.Playback.Fmp4;

namespace uYouWin.Services.Playback
{
    public sealed class YouTubeMediaStreamSource :
        IDisposable
    {
        private readonly PlaybackResource _resource;

        private Fmp4StreamReader _videoReader;
        private Fmp4StreamReader _audioReader;

        private readonly CancellationTokenSource _cts =
            new CancellationTokenSource();

        private readonly object _stateLock =
            new object();

        private bool _disposed;

        private YouTubeMediaStreamSource(
            PlaybackResource resource)
        {
            _resource = resource;
        }

        public MediaStreamSource Source { get; private set; }

        public VideoStreamDescriptor VideoDescriptor
        {
            get;
            private set;
        }

        public AudioStreamDescriptor AudioDescriptor
        {
            get;
            private set;
        }

        public static async Task<
            YouTubeMediaStreamSource> CreateAsync(
                PlaybackResource resource,
                CancellationToken cancellationToken)
        {
            if (resource == null)
                throw new ArgumentNullException(
                    nameof(resource));

            var result =
                new YouTubeMediaStreamSource(
                    resource);

            await result.InitializeAsync(
                cancellationToken);

            return result;
        }

        private async Task InitializeAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            _videoReader =
                new Fmp4StreamReader(
                    _resource.VideoUrl,
                    "vide",
                    true);

            _audioReader =
                new Fmp4StreamReader(
                    _resource.AudioUrl,
                    "soun",
                    false);

            try
            {
                await Task.WhenAll(
                    _videoReader.InitializeAsync(
                        cancellationToken),

                    _audioReader.InitializeAsync(
                        cancellationToken));
            }
            catch
            {
                Dispose();
                throw;
            }

            /*
             * Put both tracks onto a common timeline.
             *
             * The earliest first sample becomes t=0.
             */
            TimeSpan videoFirst =
                _videoReader.GetNormalizedTimestamp(
                    _videoReader.Samples[0]);

            TimeSpan audioFirst =
                _audioReader.GetNormalizedTimestamp(
                    _audioReader.Samples[0]);

            TimeSpan origin =
                videoFirst < audioFirst
                    ? videoFirst
                    : audioFirst;

            _videoReader.SetTimelineOrigin(
                origin);

            _audioReader.SetTimelineOrigin(
                origin);

            VideoEncodingProperties videoProperties =
                VideoEncodingProperties.CreateH264();

            videoProperties.Width =
                (uint)_videoReader.TrackInfo.Width;

            videoProperties.Height =
                (uint)_videoReader.TrackInfo.Height;

            videoProperties.Bitrate =
                0;

            videoProperties.ProfileId =
                _videoReader.TrackInfo.ProfileId;

            if (_videoReader.TrackInfo.FrameRate > 0)
            {
                SetFrameRate(
                    videoProperties,
                    _videoReader.TrackInfo.FrameRate);
            }

            if (_videoReader.TrackInfo.AvcConfiguration != null)
            {
                videoProperties.SetFormatUserData(
                    _videoReader.TrackInfo.AvcConfiguration);
            }

            AudioEncodingProperties audioProperties =
                AudioEncodingProperties.CreateAac(
                    _audioReader.TrackInfo.SampleRate,
                    _audioReader.TrackInfo.ChannelCount,
                    (uint)Math.Max(
                        1,
                        Math.Round(
                            _resource.AudioBitrateKbps *
                            1000)));

                         audioProperties.Subtype =
                         MediaEncodingSubtypes.Aac;

                  byte[] aacFormatUserData =
                           BuildAacFormatUserData(
                     _audioReader.TrackInfo.AudioSpecificConfig);

                      if (aacFormatUserData != null)
                  {
                     audioProperties.SetFormatUserData(
                      aacFormatUserData);
                         }

            VideoDescriptor =
                new VideoStreamDescriptor(
                    videoProperties);

            VideoDescriptor.Name =
                "YouTube H.264 Video";

            AudioDescriptor =
                new AudioStreamDescriptor(
                    audioProperties);

            AudioDescriptor.Name =
                "YouTube AAC Audio";

            Source =
                new MediaStreamSource(
                    VideoDescriptor,
                    AudioDescriptor);

            Source.BufferTime =
                TimeSpan.FromSeconds(3);

            Source.CanSeek =
                true;

            Source.IsLive =
                false;

            double duration =
                Math.Max(
                    _videoReader.Duration.TotalSeconds,
                    _audioReader.Duration.TotalSeconds);

            if (_resource.DurationSeconds > 0)
            {
                duration =
                    Math.Max(
                        duration,
                        _resource.DurationSeconds);
            }

            Source.Duration =
                TimeSpan.FromSeconds(
                    duration);

            Source.Starting +=
                Source_Starting;

            Source.SampleRequested +=
                Source_SampleRequested;

            Source.Closed +=
                Source_Closed;

            Source.Paused +=
                Source_Paused;
        }

        private static void SetFrameRate(
            VideoEncodingProperties properties,
            double fps)
        {
            const uint denominator = 1000;

            uint numerator =
                (uint)Math.Round(
                    fps * denominator);

            if (numerator == 0)
                return;

            properties.FrameRate.Numerator =
                numerator;

                  properties.FrameRate.Denominator =
                    denominator;
                   }

                   /*
                    * AudioEncodingProperties.CreateAac produces the
                    * MFAudioFormat_AAC subtype. For that subtype, MF_MT_USER_DATA
                    * (set via SetFormatUserData) must contain the tail of a
                    * HEAACWAVEINFO structure (the 12 bytes that follow its
                  * WAVEFORMATEX member: wPayloadType, wAudioProfileLevelIndication,
                * wStructType, wReserved1, dwReserved2), followed by the raw
                 * AudioSpecificConfig() bytes - not the AudioSpecificConfig alone.
                    *
                  * Without this wrapper, Media Foundation's AAC decoder cannot
            * negotiate the input type and the audio stream silently fails,
                * even though the video stream plays normally.
                    */
                   private static byte[] BuildAacFormatUserData(
              byte[] audioSpecificConfig)
                   {
                if (audioSpecificConfig == null ||
                 audioSpecificConfig.Length == 0)
                    {
                 return null;
              }

                 const ushort wPayloadType = 0; // raw_data_block only
                       const ushort wAudioProfileLevelIndication = 0xFE; // not specified
                const ushort wStructType = 0; // followed by AudioSpecificConfig()
                   const ushort wReserved1 = 0;
               const uint dwReserved2 = 0;

                       var result =
            new byte[12 + audioSpecificConfig.Length];

            WriteUInt16LittleEndian(
                 result, 0, wPayloadType);

                    WriteUInt16LittleEndian(
                      result, 2, wAudioProfileLevelIndication);

                   WriteUInt16LittleEndian(
              result, 4, wStructType);

              WriteUInt16LittleEndian(
            result, 6, wReserved1);

                       WriteUInt32LittleEndian(
                           result, 8, dwReserved2);

             Array.Copy(
                           audioSpecificConfig,
               0,
                result,
                12,
                  audioSpecificConfig.Length);

            return result;
               }

                   private static void WriteUInt16LittleEndian(
                    byte[] buffer,
                       int offset,
                  ushort value)
                   {
                       buffer[offset] = (byte)(value & 0xFF);
                       buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
                   }

                   private static void WriteUInt32LittleEndian(
                    byte[] buffer,
             int offset,
                   uint value)
              {
                   buffer[offset] = (byte)(value & 0xFF);
                   buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
                       buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
               buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
                   }

        private async void Source_Starting(
            MediaStreamSource sender,
            MediaStreamSourceStartingEventArgs args)
        {
            MediaStreamSourceStartingRequestDeferral deferral =
                args.Request.GetDeferral();

            try
            {
                // A null position means resume without repositioning the readers.
                if (!args.Request.StartPosition.HasValue)
                    return;

                TimeSpan requested =
                    args.Request.StartPosition.Value;

                if (requested < TimeSpan.Zero)
                    requested =
                        TimeSpan.Zero;

                CancellationToken token =
                    _cts.Token;

                Fmp4SeekResult videoSeek =
                    await _videoReader.PrepareSeekAsync(
                        requested,
                        token);

                Fmp4SeekResult audioSeek =
                    await _audioReader.PrepareSeekAsync(
                        requested,
                        token);

                TimeSpan actualStart =
                    videoSeek.ActualStart <
                    audioSeek.ActualStart
                        ? videoSeek.ActualStart
                        : audioSeek.ActualStart;

                /*
                 * Both readers must start at or after the actual
                 * timeline position we advertise.
                 */
                await _videoReader.PrepareSeekAsync(
                    actualStart,
                    token);

                await _audioReader.PrepareSeekAsync(
                    actualStart,
                    token);

                args.Request.SetActualStartPosition(
                    actualStart);
            }
            catch
            {
                sender.NotifyError(
                    MediaStreamSourceErrorStatus.Other);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void Source_SampleRequested(
            MediaStreamSource sender,
            MediaStreamSourceSampleRequestedEventArgs args)
        {
            MediaStreamSourceSampleRequest request =
                args.Request;

            MediaStreamSourceSampleRequestDeferral deferral =
                request.GetDeferral();

            try
            {
                CancellationToken token =
                    _cts.Token;

                bool isVideo =
                    object.ReferenceEquals(
                        request.StreamDescriptor,
                        VideoDescriptor);

                bool isAudio =
                    object.ReferenceEquals(
                        request.StreamDescriptor,
                        AudioDescriptor);

                if (!isVideo && !isAudio)
                {
                    request.Sample = null;
                    return;
                }

                Fmp4DecodedSample sample;

                if (isVideo)
                {
                    sample =
                        await _videoReader
                            .ReadNextSampleAsync(
                                token);
                }
                else
                {
                    sample =
                        await _audioReader
                            .ReadNextSampleAsync(
                                token);
                }

                if (sample == null)
                {
                    request.Sample = null;
                    return;
                }

                var buffer =
                    sample.Data.AsBuffer();

                MediaStreamSample mediaSample =
                    MediaStreamSample.CreateFromBuffer(
                        buffer,
                        sample.Timestamp);

                mediaSample.Duration =
                    sample.Duration;

                request.Sample =
                    mediaSample;
            }
            catch (OperationCanceledException)
            {
                request.Sample =
                    null;
            }
            catch (Exception)
            {
                try
                {
                    sender.NotifyError(
                        MediaStreamSourceErrorStatus.Other);
                }
                catch
                {
                }

                request.Sample =
                    null;
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void Source_Paused(
            MediaStreamSource sender,
            object args)
        {
            /*
             * Nothing needs to be done here.
             *
             * The MediaStreamSource will issue Starting again when
             * playback resumes.
             */
        }

        private void Source_Closed(
            MediaStreamSource sender,
            MediaStreamSourceClosedEventArgs args)
        {
            Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(YouTubeMediaStreamSource));
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            try
            {
                _cts.Cancel();
            }
            catch
            {
            }

            try
            {
                if (Source != null)
                {
                    Source.Starting -=
                        Source_Starting;

                    Source.SampleRequested -=
                        Source_SampleRequested;

                    Source.Closed -=
                        Source_Closed;

                    Source.Paused -=
                        Source_Paused;
                }
            }
            catch
            {
            }

            if (_videoReader != null)
            {
                _videoReader.Dispose();
                _videoReader = null;
            }

            if (_audioReader != null)
            {
                _audioReader.Dispose();
                _audioReader = null;
            }

            _cts.Dispose();
        }
    }
}