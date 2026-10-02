using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using uYouWin.Models;
using uYouWin.Services.Playback;
using uYouWin.ViewModels;

namespace uYouWin.Views
{
    /// <summary>
    /// VideoPage.xaml 的互動邏輯
    /// </summary>
    public partial class VideoPage : Page
    {
        private Video _video;
        private List<Video> _playlist;

        private readonly PlaybackViewModel _playbackViewModel;
        private readonly DispatcherTimer _progressTimer;
        private bool _isDraggingScrubBar;
        private bool _subtitlesEnabled;
        private bool _isPlaylistOpen;
        private FullScreenOverlayWindow _overlay;
        private CancellationTokenSource _detailsCancellation;
        private static VideoPage _surfaceOwner;
        private bool _pageActive;
        private Windows.UI.Xaml.Controls.MediaPlayerElement _attachedSurface;

        private static readonly BitmapImage PlayIconImage =
            new BitmapImage(new Uri("pack://application:,,,/Assets/icons/play.png"));

        private static readonly BitmapImage PauseIconImage =
            new BitmapImage(new Uri("pack://application:,,,/Assets/icons/pause.png"));

        public VideoPage()
        {
            InitializeComponent();
            Loaded += VideoPage_Loaded;
            Unloaded += VideoPage_Unloaded;

            _playbackViewModel = App.PlaybackViewModel;

            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += ProgressTimer_Tick;
        }

        public VideoPage(Video video) : this()
        {
            _video = video;
        }

        public VideoPage(Video video, List<Video> playlist) : this(video)
        {
            _playlist = playlist;
        }

        private async void VideoPage_Loaded(object sender, RoutedEventArgs e)
        {
            _pageActive = true;
            // 若建構函式未提供 Video，嘗試從 NavigationService 取得透過 Navigate(page, extraData) 傳遞的參數
            if (_video == null && NavigationService != null)
            {
                _video = NavigationService.Content as Video;
            }

            MainWindow mainWindow = MainWindow.Instance;

            if (mainWindow != null)
            {
                mainWindow.FullScreenChanged += MainWindow_FullScreenChanged;

                UpdateControlsForFullScreen(mainWindow.IsFullScreen);
            }

            UpdatePopupLayout();
            _overlay = new FullScreenOverlayWindow();
            _overlay.Attach(RootGrid);

            if (_video == null)
            {
                MessageBox.Show(
                    "VideoPage was opened without a Video.",
                    "Playback Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            if (_playlist != null)
            {
                PlaylistListBox.ItemsSource = _playlist;
            }

            VolumeSlider.Value = _playbackViewModel.Volume;

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

                AttachVideoSurface();

                // Avoid re-issuing playback if this video is already the
                // one currently playing (e.g. user navigated back to the
                // page for the same video); this prevents an unnecessary
                // stream restart/stall.
                if (_playbackViewModel.CurrentVideo?.Id != _video.Id)
                {
                    await _playbackViewModel.PlayAsync(_video, _playlist);
                }
                if (!_pageActive)
                    return;

                PlaylistListBox.SelectedIndex = _playbackViewModel.PlaylistIndex;

                UpdateVideoInfo();
                _progressTimer.Start();
                UpdatePlayPauseIcon();
                await LoadDetailsAsync();
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
            _pageActive = false;
            DetachVideoSurface();
            _progressTimer.Stop();
            _detailsCancellation?.Cancel();
            _overlay?.Close();
            _overlay = null;
            ControlsPopup.IsOpen = false;
            PlaylistPopup.IsOpen = false;
            VolumePopup.IsOpen = false;
            MorePopup.IsOpen = false;

            MainWindow mainWindow = MainWindow.Instance;

            if (mainWindow != null)
            {
                mainWindow.FullScreenChanged -= MainWindow_FullScreenChanged;
                mainWindow.SetFullScreen(false);
            }

            // Playback intentionally continues after navigating away so the
            // user can keep listening/watching via the bottom media bar.
        }

        private void MainWindow_FullScreenChanged(bool isFullScreen)
        {
            UpdateControlsForFullScreen(isFullScreen);
        }

        // The media control overlay (play/pause, seek, volume, subtitles,
        // playlist, fullscreen toggle, etc.) is only meant to be shown
        // while the app is in fullscreen playback mode; outside of
        // fullscreen, the shared bottom media bar in MainWindow provides
        // playback control instead.
        private void UpdateControlsForFullScreen(bool isFullScreen)
        {
            VideoRow.Height = new GridLength(isFullScreen ? 1 : 2, GridUnitType.Star);
            DetailsRow.Height = isFullScreen ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            DetailsView.Visibility = isFullScreen ? Visibility.Collapsed : Visibility.Visible;
            if (!isFullScreen)
            {
                ControlsPopup.IsOpen = false;
                VolumePopup.IsOpen = false;
                MorePopup.IsOpen = false;

                ClosePlaylistPanel();
            }
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePopupLayout();
        }

        private void VideoPlayerElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (_pageActive)
                AttachVideoSurface();
        }

        private void AttachVideoSurface()
        {
            if (!_pageActive || !VideoPlayerElement.IsLoaded || App.PlaybackService?.MediaPlayer == null)
                return;
            var nativeSurface = VideoPlayerElement.GetUwpInternalObject() as Windows.UI.Xaml.Controls.MediaPlayerElement;
            if (nativeSurface == null || ReferenceEquals(_attachedSurface, nativeSurface))
                return;
            _surfaceOwner?.DetachVideoSurface();
            VideoPlayerElement.Visibility = Visibility.Visible;
            nativeSurface.SetMediaPlayer(App.PlaybackService.MediaPlayer.Player);
            _attachedSurface = nativeSurface;
            _surfaceOwner = this;
        }

        private void DetachVideoSurface()
        {
            var nativeSurface = _attachedSurface;
            _attachedSurface = null;
            if (ReferenceEquals(_surfaceOwner, this))
                _surfaceOwner = null;
            try
            {
                // Toolkit 6.1.2 dereferences a null wrapper argument; detach through UWP instead.
                // Do not clear the shared player's source or stop background playback.
                nativeSurface?.SetMediaPlayer(null);
            }
            catch (ObjectDisposedException)
            {
                // The XAML Island may already have been destroyed during window shutdown.
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80000013))
            {
                // RO_E_CLOSED: the native surface has already detached during teardown.
            }
            finally
            {
                VideoPlayerElement.Visibility = Visibility.Hidden;
            }
        }

        // controls:MediaPlayerElement hosts a UWP XAML Island via HwndHost,
        // which always paints above every sibling WPF element in the same
        // window (an "airspace" restriction) regardless of z-order. Popups
        // are separate top-level windows and aren't subject to that
        // restriction, but unlike normal sibling elements they don't
        // automatically track the size/position of RootGrid, so it has to
        // be done manually here.
        private void UpdatePopupLayout()
        {
            if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
                return;

            ControlsPopup.Width = RootGrid.ActualWidth;

            ControlsOverlay.Measure(
                new Size(RootGrid.ActualWidth, double.PositiveInfinity));

            double overlayHeight =
                ControlsOverlay.DesiredSize.Height;

            ControlsPopup.HorizontalOffset = 0;
            ControlsPopup.VerticalOffset =
                RootGrid.ActualHeight - overlayHeight;

            PlaylistPanel.Height = RootGrid.ActualHeight;

            PlaylistPopup.HorizontalOffset =
                RootGrid.ActualWidth - PlaylistPanel.Width;

            PlaylistPopup.VerticalOffset = 0;
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (_playbackViewModel.CurrentVideo != null && _video?.Id != _playbackViewModel.CurrentVideo.Id)
            {
                _video = _playbackViewModel.CurrentVideo;
                _ = LoadDetailsAsync();
            }
            if (_isDraggingScrubBar)
                return;

            var player = App.PlaybackService?.Player;

            if (player == null)
                return;

            double duration = player.Duration;

            if (duration > 0)
            {
                ScrubBar.Maximum = duration;
                ScrubBar.Value = player.Position;
            }

            UpdatePlayPauseIcon();
        }

        private void UpdatePlayPauseIcon()
        {
            var player = App.PlaybackService?.Player;

            if (player == null)
                return;

            PlayPauseIcon.Source =
                player.IsPlaying ? PauseIconImage : PlayIconImage;
        }

        private void UpdateVideoInfo()
        {
            if (_video == null)
                return;

            VideoInfoTextBlock.Text =
                _video.Title + "\n" +
                _video.ChannelTitle + "\n" +
                "Published: " + _video.PublishedAt.ToShortDateString();

            AudioTrackComboBox.ItemsSource = new[] { "Default" };
            AudioTrackComboBox.SelectedIndex = 0;

            SubtitleTrackComboBox.ItemsSource = new[] { "Off" };
            SubtitleTrackComboBox.SelectedIndex = 0;
        }

        private void RootGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_overlay != null)
                return;
            MainWindow mainWindow = MainWindow.Instance;

            if (mainWindow == null || !mainWindow.IsFullScreen)
                return;

            if (!ControlsPopup.IsOpen)
            {
                UpdatePopupLayout();
            }

            ControlsPopup.IsOpen = true;
        }

        private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            // Don't hide the controls while a popup anchored to them is
            // open, otherwise the popup would immediately close too.
            if (VolumePopup.IsOpen || MorePopup.IsOpen)
                return;

            ControlsPopup.IsOpen = false;
        }

        private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            MainWindow mainWindow = MainWindow.Instance;

            if (mainWindow == null)
                return;

            mainWindow.SetFullScreen(!mainWindow.IsFullScreen);
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            _playbackViewModel.TogglePlayPause();
            UpdatePlayPauseIcon();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _playbackViewModel.Stop();
            UpdatePlayPauseIcon();
        }

        private void SeekBackButton_Click(object sender, RoutedEventArgs e)
        {
            _playbackViewModel.SeekRelative(-5);
        }

        private void SeekForwardButton_Click(object sender, RoutedEventArgs e)
        {
            _playbackViewModel.SeekRelative(5);
        }

        private void ScrubBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingScrubBar = true;
        }

        private void ScrubBar_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDraggingScrubBar = true;
        }

        private void ScrubBar_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDraggingScrubBar = false;

            _playbackViewModel.Seek(ScrubBar.Value);
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = !VolumePopup.IsOpen;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_playbackViewModel == null)
                return;

            _playbackViewModel.Volume = e.NewValue;

            VolumeIcon.Glyph = e.NewValue <= 0 ? "\uE74F" : "\uE995";
        }

        private void SubtitleButton_Click(object sender, RoutedEventArgs e)
        {
            _subtitlesEnabled = !_subtitlesEnabled;

            SubtitleIcon.Glyph = _subtitlesEnabled ? "\uED1E" : "\uED1A";

            // Subtitle track rendering is not yet implemented; this only
            // toggles the on/off indicator for now.
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MorePopup.IsOpen = !MorePopup.IsOpen;
        }

        private void PlaylistToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaylistOpen)
            {
                ClosePlaylistPanel();
            }
            else
            {
                OpenPlaylistPanel();
            }
        }

        private void OpenPlaylistPanel()
        {
            if (_isPlaylistOpen)
                return;

            _isPlaylistOpen = true;

            UpdatePopupLayout();

            PlaylistPopup.IsOpen = true;

            AnimatePlaylistPanel(toX: 0);
        }

        private void ClosePlaylistPanel()
        {
            if (!_isPlaylistOpen && !PlaylistPopup.IsOpen)
                return;

            _isPlaylistOpen = false;

            AnimatePlaylistPanel(
                toX: PlaylistPanel.Width,
                onCompleted: () => PlaylistPopup.IsOpen = false);
        }

        private void AnimatePlaylistPanel(double toX, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                To = toX,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }

            PlaylistPanelTransform.BeginAnimation(
                TranslateTransform.XProperty,
                animation);
        }

        private async System.Threading.Tasks.Task LoadDetailsAsync()
        {
            _detailsCancellation?.Cancel();
            _detailsCancellation?.Dispose();
            _detailsCancellation = new CancellationTokenSource();
            var token = _detailsCancellation.Token;
            Video video = _video;
            if (video == null)
                return;
            VideoTitle.Text = video.Title;
            ChannelButton.Content = video.ChannelTitle;
            ChannelButton.IsEnabled = !string.IsNullOrWhiteSpace(video.ChannelId);
            DescriptionText.Text = video.Description;
            CommentsList.ItemsSource = null;
            RelatedList.ItemsSource = null;
            CommentsStatus.Text = RelatedStatus.Text = "Loading…";
            try
            {
                var details = await App.YouTubeApiService.GetVideoAsync(video.Id, token);
                token.ThrowIfCancellationRequested();
                if (details != null)
                {
                    _video = details;
                    VideoTitle.Text = details.Title;
                    DescriptionText.Text = details.Description;
                    ChannelButton.Content = details.ChannelTitle;
                    ChannelButton.IsEnabled = !string.IsNullOrWhiteSpace(details.ChannelId);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { }
            try
            {
                var comments = await App.YouTubeApiService.GetCommentsAsync(video.Id, token);
                token.ThrowIfCancellationRequested();
                CommentsList.ItemsSource = comments;
                CommentsStatus.Text = comments.Count == 0 ? "No comments." : "";
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { CommentsStatus.Text = "Comments unavailable: " + ex.Message; }
            try
            {
                var related = await App.YouTubeApiService.GetRelatedVideosAsync(_video, token);
                token.ThrowIfCancellationRequested();
                RelatedList.ItemsSource = related;
                RelatedStatus.Text = related.Count == 0 ? "No related videos." : "";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { RelatedStatus.Text = "Related videos unavailable: " + ex.Message; }
        }

        private void ChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_video?.ChannelId))
                NavigationService?.Navigate(new ChannelPage(_video.ChannelId));
        }

        private void SaveToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_video != null)
                NavigationService?.Navigate(new PlaylistPage(_video));
        }

        private void RelatedList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RelatedList.SelectedItem is Video video)
                NavigationService?.Navigate(new VideoPage(video));
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = MainWindow.Instance;

            if (mainWindow == null)
                return;

            bool goingFullScreen = !mainWindow.IsFullScreen;

            mainWindow.SetFullScreen(goingFullScreen);

            FullScreenIcon.Glyph = goingFullScreen ? "\uE73F" : "\uE740";
        }

        private async void LastButton_Click(object sender, RoutedEventArgs e)
        {
            await _playbackViewModel.PlayPreviousAsync();
            SyncAfterPlaylistNavigation();
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await _playbackViewModel.PlayNextAsync();
            SyncAfterPlaylistNavigation();
        }

        private void SyncAfterPlaylistNavigation()
        {
            _video = _playbackViewModel.CurrentVideo;
            PlaylistListBox.SelectedIndex = _playbackViewModel.PlaylistIndex;
            UpdateVideoInfo();
            UpdatePlayPauseIcon();
        }

        private async void PlaylistListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(PlaylistListBox.SelectedItem is Video video))
                return;

            try
            {
                await _playbackViewModel.PlayFromPlaylistAsync(video);
                SyncAfterPlaylistNavigation();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Playback failed:\n\n" + ex,
                    "Playback Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
