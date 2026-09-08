using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using uYouWin.Models;
using uYouWin.Services.Playback;

namespace uYouWin.Views
{
    /// <summary>
    /// VideoPage.xaml 的互動邏輯
    /// </summary>
    public partial class VideoPage : Page
    {
        private Video _video;

        public VideoPage()
        {
     InitializeComponent();
     Loaded += VideoPage_Loaded;
     Unloaded += VideoPage_Unloaded;
        }

        public VideoPage(Video video) : this()
 {
     _video = video;
        }

        private async void VideoPage_Loaded(object sender, RoutedEventArgs e)
 {
     // 若建構函式未提供 Video，嘗試從 NavigationService 取得透過 Navigate(page, extraData) 傳遞的參數
     if (_video == null && NavigationService != null)
            {
         _video = NavigationService.Content as Video;
            }

     if (_video == null)
     {
         MessageBox.Show(
      "VideoPage was opened without a Video.",
      "Playback Error",
      MessageBoxButton.OK,
      MessageBoxImage.Error);

  return;
     }

     try
     {
         if (App.PlaybackService == null)
  {
      throw new InvalidOperationException(
   "PlaybackService has not been initialized.");
         }

         if (App.PlaybackService.MediaPlayer == null)
  {
      throw new InvalidOperationException(
   "PlaybackService is not using MediaPlayerVideoPlayer.");
  }

  VideoPlayerElement.SetMediaPlayer(
      App.PlaybackService.MediaPlayer.Player);

         await App.PlaybackService.PlayAsync(
      _video);
     }
     catch (Exception ex)
     {
  MessageBox.Show(
      "Playback failed:\n\n" +
      ex,
      "Playback Error",
      MessageBoxButton.OK,
      MessageBoxImage.Error);
     }
        }

        // 用 Unloaded 事件釋放資源
 private void VideoPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
     {
  App.PlaybackService?.Stop();
     }
     catch
            {
     }
        }
    }
}
