using System;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    public sealed class PlaybackService :
        IDisposable
    {
        private readonly PlaybackResolverInterface _resolver;
        private readonly VideoPlayerInterface _player;

        private readonly PlaybackSettings _settings;

        private CancellationTokenSource
            _playbackCancellation;

        private bool _disposed;

        public PlaybackService(
            PlaybackResolverInterface resolver,
            VideoPlayerInterface player,
            PlaybackSettings settings)
        {
            _resolver =
                resolver ??
                throw new ArgumentNullException(
                    nameof(resolver));

            _player =
                player ??
                throw new ArgumentNullException(
                    nameof(player));

            _settings =
                settings ??
                throw new ArgumentNullException(
                    nameof(settings));
        }

        public VideoPlayerInterface Player
        {
            get
            {
                return _player;
            }
        }

        public PlaybackResource
            CurrentResource
        {
            get;
            private set;
        }

        public async Task PlayAsync(
            Video video)
        {
            ThrowIfDisposed();

            if (video == null)
                throw new ArgumentNullException(
                    nameof(video));

            CancelCurrentPlayback();

            _playbackCancellation =
                new CancellationTokenSource();

            CancellationToken token =
                _playbackCancellation.Token;

            PlaybackResource resource =
                await _resolver.ResolveAsync(
                    video,
                    _settings,
                    token);

            token.ThrowIfCancellationRequested();

            CurrentResource =
                resource;

            await _player.OpenAsync(
                resource,
                token);

            token.ThrowIfCancellationRequested();

            _player.Play();
        }

        public void Pause()
        {
            ThrowIfDisposed();

            _player.Pause();
        }

        public void Stop()
        {
            ThrowIfDisposed();

            CancelCurrentPlayback();

            _player.Stop();

            CurrentResource =
                null;
        }

        public void Seek(
            double position)
        {
            ThrowIfDisposed();

            _player.Seek(
                position);
        }

        private void CancelCurrentPlayback()
        {
            if (_playbackCancellation == null)
                return;

            try
            {
                _playbackCancellation.Cancel();
            }
            catch
            {
            }

            _playbackCancellation.Dispose();

            _playbackCancellation =
                null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(PlaybackService));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            CancelCurrentPlayback();

            _player.Dispose();
        }

        public MediaPlayerVideoPlayer MediaPlayer
        {
            get
            {
                return _player as MediaPlayerVideoPlayer;
            }
        }
    }
}