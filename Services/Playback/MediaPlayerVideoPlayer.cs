using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    public sealed class MediaPlayerVideoPlayer :
        VideoPlayerInterface
    {
        private MediaSource _mediaSource;

        private YouTubeMediaStreamSource
            _streamSource;

        private bool _disposed;

        public MediaPlayer Player
        {
            get;
        }

        public object NativePlayer
        {
            get
            {
                return Player;
            }
        }

        public MediaPlayerVideoPlayer()
        {
            Player =
                new MediaPlayer();

            Player.AudioCategory =
                MediaPlayerAudioCategory.Movie;
        }

        public async Task OpenAsync(
            PlaybackResource resource,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (resource == null)
                throw new ArgumentNullException(
                    nameof(resource));

            CloseSource();

            _streamSource =
                await YouTubeMediaStreamSource.CreateAsync(
                    resource,
                    cancellationToken);

            _mediaSource =
                MediaSource.CreateFromMediaStreamSource(
                    _streamSource.Source);

            var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Windows.Foundation.TypedEventHandler<MediaPlayer, object> onOpened = (s, e) => opened.TrySetResult(true);
            Windows.Foundation.TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> onFailed =
                (s, e) => opened.TrySetException(new InvalidOperationException(e.ErrorMessage));
            Player.MediaOpened += onOpened;
            Player.MediaFailed += onFailed;
            try
            {
                using (cancellationToken.Register(() => opened.TrySetCanceled()))
                {
                    Player.Source = _mediaSource;
                    await opened.Task;
                }
            }
            finally
            {
                Player.MediaOpened -= onOpened;
                Player.MediaFailed -= onFailed;
            }
        }

        public void Play()
        {
            ThrowIfDisposed();
            Player.Play();
        }

        public void Pause()
        {
            ThrowIfDisposed();

            Player.Pause();
        }

        public void Stop()
        {
            ThrowIfDisposed();

            Player.Pause();

            try
            {
                Player.PlaybackSession.Position =
                    TimeSpan.Zero;
            }
            catch
            {
            }
        }

        public void Seek(
            double position)
        {
            ThrowIfDisposed();

            if (position < 0)
                position = 0;

            Player.PlaybackSession.Position =
                TimeSpan.FromSeconds(position);
        }

        public double Position
        {
            get
            {
                if (_disposed)
                    return 0;

                return Player
                    .PlaybackSession
                    .Position.TotalSeconds;
            }
        }

        public double Duration
        {
            get
            {
                if (_disposed)
                    return 0;

                return Player
                    .PlaybackSession
                    .NaturalDuration.TotalSeconds;
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (_disposed)
                    return false;

                return
                    Player
                        .PlaybackSession
                        .PlaybackState ==
                    MediaPlaybackState.Playing;
            }
        }

        public double Volume
        {
            get
            {
                if (_disposed)
                    return 0;

                return Player.Volume;
            }
            set
            {
                if (_disposed)
                    return;

                Player.Volume = Math.Max(0, Math.Min(1, value));
            }
        }

        private void CloseSource()
        {
            if (_mediaSource != null)
            {
                try
                {
                    _mediaSource.Dispose();
                }
                catch
                {
                }

                _mediaSource = null;
            }

            if (_streamSource != null)
            {
                _streamSource.Dispose();
                _streamSource = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(MediaPlayerVideoPlayer));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            CloseSource();

            try
            {
                Player.Pause();
            }
            catch
            {
            }

            Player.Dispose();
        }
    }
}