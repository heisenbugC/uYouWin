using System;
using System.IO;
using System.Windows;
using uYouWin.Models;
using uYouWin.Services.Playback;

namespace uYouWin
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        public static PlaybackService PlaybackService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

         string ytDlpPath = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "yt-dlp", "yt-dlp.exe");

            var playBackSettings = new PlaybackSettings
          {
         MaxVideoHeight = 1080,
      TargetAudioBitrateKbps = 256,
        VideoCodec = "h264",
        AudioCodec = "aac"
            };

            var resolver = new YtDlpPlaybackResolver(ytDlpPath);

            var player = new MediaPlayerVideoPlayer();

            PlaybackService = new PlaybackService(
                resolver,
                player,
                playBackSettings);
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
