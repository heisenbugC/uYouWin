using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using uYouWin.Models;
using uYouWin.Services.Cache;
using uYouWin.Services.Library;
using uYouWin.Services.Playback;

namespace uYouWin.ViewModels
{
    /// <summary>
    /// Central, application-wide view model wrapping <see cref="PlaybackService"/>.
    ///
    /// A single instance is owned by <see cref="App"/> and shared by
    /// <see cref="Views.MainWindow"/>'s bottom media bar and any open
    /// <see cref="Views.VideoPage"/>, so playback state (current video,
    /// position, play/pause, volume) stays consistent no matter which page
    /// is active and switching pages never stops the stream.
    /// </summary>
    public class PlaybackViewModel : ViewModelBase
    {
        private readonly PlaybackService _playbackService;
        private readonly DispatcherTimer _timer;

        private Video _currentVideo;
        private List<Video> _playlist;
        private int _playlistIndex = -1;

        private double _position;
        private double _duration;
        private bool _isPlaying;
        private double _volume = 1d;
        private bool _isOpening;

        public bool IsOpening => _isOpening;

        public PlaybackViewModel(PlaybackService playbackService)
        {
            _playbackService =
                playbackService ??
                throw new ArgumentNullException(nameof(playbackService));

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public VideoPlayerInterface Player => _playbackService.Player;

        public Video CurrentVideo
        {
            get => _currentVideo;
            private set => SetProperty(ref _currentVideo, value);
        }

        public List<Video> Playlist
        {
            get => _playlist;
            private set => SetProperty(ref _playlist, value);
        }

        public int PlaylistIndex
        {
            get => _playlistIndex;
            private set => SetProperty(ref _playlistIndex, value);
        }

        public double Position
        {
            get => _position;
            private set => SetProperty(ref _position, value);
        }

        public double Duration
        {
            get => _duration;
            private set => SetProperty(ref _duration, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            private set => SetProperty(ref _isPlaying, value);
        }

        public double Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    if (Player != null)
                        Player.Volume = value;
                }
            }
        }

        public bool HasPrevious => Playlist != null && PlaylistIndex > 0;

        public bool HasNext =>
            Playlist != null && PlaylistIndex >= 0 && PlaylistIndex < Playlist.Count - 1;

        public async Task PlayAsync(Video video, List<Video> playlist = null)
        {
            if (video == null)
                throw new ArgumentNullException(nameof(video));

            if (_isOpening)
                return;
            _isOpening = true;
            try
            {
                await _playbackService.PlayAsync(video);
                Playlist = playlist;
                PlaylistIndex = playlist?.FindIndex(v => v.Id == video.Id) ?? -1;
                CurrentVideo = video;
                SubtitleCueLoader.Clear();
                Player.Volume = Volume;
                RefreshState();
                VisitedUrlCache.Save(video);
                await App.LibraryService.AddHistoryEntryAsync(new HistoryEntry
                {
                    VideoId = video.Id,
                    Title = video.Title,
                    ChannelId = video.ChannelId,
                    ChannelTitle = video.ChannelTitle,
                    ThumbnailUrl = video.ThumbnailUrl,
                    WebUrl = video.YtUrl ?? video.WebUrl,
                    WatchedAt = DateTime.Now
                });
            }
            finally
            {
                _isOpening = false;
            }
        }

        public async Task PlayPreviousAsync()
        {
            if (!HasPrevious)
                return;

            await PlayAsync(Playlist[PlaylistIndex - 1], Playlist);
        }

        public async Task PlayNextAsync()
        {
            if (!HasNext)
                return;

            await PlayAsync(Playlist[PlaylistIndex + 1], Playlist);
        }

        public async Task PlayFromPlaylistAsync(Video video)
        {
            if (Playlist == null)
                return;

            int index = Playlist.FindIndex(v => v.Id == video.Id);

            if (index < 0)
                return;

            await PlayAsync(video, Playlist);
        }

        public void TogglePlayPause()
        {
            if (Player == null || CurrentVideo == null || _isOpening)
                return;

            if (Player.IsPlaying)
            {
                _playbackService.Pause();
            }
            else
            {
                Player.Play();
            }

            RefreshState();
        }

        public void Stop()
        {
            _playbackService.Stop();
            SubtitleCueLoader.Clear();

            CurrentVideo = null;
            Playlist = null;
            PlaylistIndex = -1;

            RefreshState();
        }

        public void Seek(double position)
        {
            if (CurrentVideo != null && !_isOpening && !double.IsNaN(position) && !double.IsInfinity(position))
                _playbackService.Seek(Math.Max(0, Math.Min(Player.Duration, position)));
        }

        public void SeekRelative(double deltaSeconds)
        {
            if (Player == null)
                return;

            double target = Player.Position + deltaSeconds;

            target = Math.Max(0, Math.Min(Player.Duration, target));

            _playbackService.Seek(target);
        }

        public async Task SetAudioLanguageAsync(string language)
        {
            if (CurrentVideo == null || _isOpening)
                return;
            _isOpening = true;
            double position = Player.Position;
            bool playing = Player.IsPlaying;
            try
            {
                await _playbackService.PlayAsync(CurrentVideo, language == "Default" ? null : language);
                Player.Volume = Volume;
                _playbackService.Seek(position);
                if (!playing)
                    _playbackService.Pause();
            }
            finally { _isOpening = false; }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            RefreshState();
        }

        private void RefreshState()
        {
            if (Player == null)
            {
                Position = 0;
                Duration = 0;
                IsPlaying = false;
                return;
            }

            Duration = Player.Duration;
            Position = Player.Position;
            IsPlaying = Player.IsPlaying;
        }
    }
}
