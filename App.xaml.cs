using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using uYouWin.Models;
using uYouWin.Services.Library;
using uYouWin.Services.Playback;
using uYouWin.Services.YouTube;
using uYouWin.ViewModels;

namespace uYouWin
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        public static PlaybackService PlaybackService { get; private set; }

        public static PlaybackViewModel PlaybackViewModel { get; private set; }

        internal static LibraryService LibraryService { get; private set; }

        private static readonly HttpClient _youTubeHttpClient =
            new HttpClient();

        internal static YouTubeApiServiceInterface YouTubeApiService
        {
            get;
            private set;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Services.Cache.DiskCache.ScheduleCleanup();

            string ytDlpPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "yt-dlp.exe");

            PlaybackSettings playBackSettings = PlaybackSettingsStore.Load();

            var resolver = new YtDlpPlaybackResolver(ytDlpPath);
            var player = new MediaPlayerVideoPlayer();

            PlaybackService = new PlaybackService(
                resolver,
                player,
                playBackSettings);

            PlaybackViewModel = new PlaybackViewModel(PlaybackService);
            LibraryService = new LibraryService();

            RefreshYouTubeApiSettings();
        }

        /// <summary>
        /// Rebuilds <see cref="YouTubeApiService"/> from the currently
        /// persisted <see cref="YouTubeApiSettings"/>. Call this after the
        /// user changes their API key/base URL in Settings.
        /// </summary>
        public static void RefreshYouTubeApiSettings()
        {
            YouTubeApiSettings settings =
                YouTubeApiSettingsStore.Load();

            var client =
                new YouTubeApiClient(
                    _youTubeHttpClient,
                    settings);

            YouTubeApiService =
                new YouTubeApiService(client);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (PlaybackService != null)
            {
                PlaybackService.Dispose();
                PlaybackService = null;
            }

            base.OnExit(e);
        }
    }
}
