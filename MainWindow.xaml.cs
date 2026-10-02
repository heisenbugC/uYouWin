using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ModernWpf.Controls;
using uYouWin.Views;

namespace uYouWin
{
    /// <summary>
    /// The main windows of the application, containing the navigation frame and the navigation view.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _playbackBarTimer;

        private WindowState _preFullScreenWindowState;
        private WindowStyle _preFullScreenWindowStyle;
        private ResizeMode _preFullScreenResizeMode;
        private bool _isFullScreen;
        private bool _isDraggingPlaybackScrubBar;

        private static readonly BitmapImage BottomPlayIconImage =
            new BitmapImage(new Uri("pack://application:,,,/Assets/icons/play.png"));

        private static readonly BitmapImage BottomPauseIconImage =
            new BitmapImage(new Uri("pack://application:,,,/Assets/icons/pause.png"));

        public static MainWindow Instance { get; private set; }

        public event Action<bool> FullScreenChanged;

        public MainWindow()
        {
            InitializeComponent();
            MediaScrubber.Attach(PlaybackScrubBar);
            ContentFrame.Navigate(new HomePage());

            Instance = this;

            _playbackBarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _playbackBarTimer.Tick += PlaybackBarTimer_Tick;
            _playbackBarTimer.Start();
        }

        public bool IsFullScreen => _isFullScreen;

        public void SetFullScreen(bool fullScreen)
        {
            if (fullScreen == _isFullScreen)
                return;

            _isFullScreen = fullScreen;

            if (fullScreen)
            {
                _preFullScreenWindowState = WindowState;
                _preFullScreenWindowStyle = WindowStyle;
                _preFullScreenResizeMode = ResizeMode;

                NavigationRow.Height = new GridLength(1, GridUnitType.Star);
                PlaybackBarRow.Height = new GridLength(0);
                NavigationView.IsPaneOpen = false;
                NavigationView.IsPaneVisible = false;
                NavigationView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
                PlaybackBarBorder.Visibility = Visibility.Collapsed;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
            else
            {
                NavigationRow.Height = new GridLength(1, GridUnitType.Star);
                PlaybackBarRow.Height = new GridLength(88);
                NavigationView.Visibility = Visibility.Visible;
                NavigationView.IsPaneVisible = true;
                NavigationView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
                PlaybackBarBorder.Visibility = Visibility.Visible;

                WindowStyle = _preFullScreenWindowStyle;
                ResizeMode = _preFullScreenResizeMode;
                WindowState = _preFullScreenWindowState;
            }

            FullScreenChanged?.Invoke(_isFullScreen);
        }

        private void PlaybackBarTimer_Tick(object sender, EventArgs e)
        {
            var playbackService = App.PlaybackService;

            if (playbackService == null)
                return;

            var player = playbackService.Player;
            var video = playbackService.CurrentVideo;
            var state = App.PlaybackViewModel;
            NowPlayingButton.IsEnabled = video != null && state?.IsOpening != true;
            BottomPlayPauseButton.IsEnabled = video != null && state?.IsOpening != true;
            BottomLastButton.IsEnabled = state?.HasPrevious == true;
            BottomNextButton.IsEnabled = state?.HasNext == true;
            BottomLastButton.Visibility = BottomNextButton.Visibility = state?.Playlist == null ? Visibility.Collapsed : Visibility.Visible;

            if (video == null)
            {
                NowPlayingTitleTextBlock.Text = "Nothing Playing";
                NowPlayingSubtitleTextBlock.Text = "Select a video to start playback";
                PlaybackScrubBar.Value = 0;
                BottomPlayPauseIcon.Glyph = "\uE768";
                BottomVideoInfoTextBlock.Text = string.Empty;
                return;
            }

            NowPlayingTitleTextBlock.Text = video.Title;
            NowPlayingSubtitleTextBlock.Text = video.ChannelTitle;

            BottomVideoInfoTextBlock.Text =
                video.Title + "\n" +
                video.ChannelTitle + "\n" +
                "Published: " + video.PublishedAt.ToShortDateString();

            if (player != null)
            {
                double duration = player.Duration;

                if (!PlaybackScrubBar.IsMouseCaptureWithin)
                {
                    PlaybackScrubBar.Maximum = duration > 0 ? duration : 100;
                    PlaybackScrubBar.Value = player.Position;
                }

                BottomPlayPauseIcon.Glyph = player.IsPlaying ? "\uE769" : "\uE768";
            }

            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel != null &&
                !BottomPlaylistListBox.IsMouseOver)
            {
                if (!ReferenceEquals(
                        BottomPlaylistListBox.ItemsSource,
                        playbackViewModel.Playlist))
                {
                    BottomPlaylistListBox.ItemsSource =
                        playbackViewModel.Playlist;
                }

                BottomPlaylistListBox.SelectedIndex =
                    playbackViewModel.PlaylistIndex;
            }
        }

        private void PlaybackScrubBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlaybackScrubBar = true;
        }

        private void PlaybackScrubBar_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDraggingPlaybackScrubBar = true;
        }

        private void PlaybackScrubBar_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _isDraggingPlaybackScrubBar = false;

            App.PlaybackViewModel?.Seek(PlaybackScrubBar.Value);
        }

        private void NowPlayingButton_Click(object sender, RoutedEventArgs e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel?.CurrentVideo == null)
                return;

            ContentFrame.Navigate(
                new Views.VideoPage(
                    playbackViewModel.CurrentVideo,
                    playbackViewModel.Playlist));
        }

        private void BottomPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            App.PlaybackViewModel?.TogglePlayPause();
        }

        private void BottomStopButton_Click(object sender, RoutedEventArgs e)
        {
            App.PlaybackViewModel?.Stop();
        }

        private async void BottomLastButton_Click(object sender, RoutedEventArgs e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel == null)
                return;

            await FullScreenOverlayWindow.RunPlaybackAsync(() => playbackViewModel.PlayPreviousAsync());
        }

        private async void BottomNextButton_Click(object sender, RoutedEventArgs e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel == null)
                return;

            await FullScreenOverlayWindow.RunPlaybackAsync(() => playbackViewModel.PlayNextAsync());
        }

        private void BottomVolumeButton_Click(object sender, RoutedEventArgs e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel != null)
            {
                BottomVolumeSlider.Value = playbackViewModel.Volume;
            }

            BottomVolumePopup.IsOpen = !BottomVolumePopup.IsOpen;
        }

        private void BottomVolumeSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel == null)
                return;

            playbackViewModel.Volume = e.NewValue;

            BottomVolumeIcon.Glyph = e.NewValue <= 0 ? "\uE74F" : "\uE995";
        }

        private void BottomPlaylistToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel != null)
            {
                BottomPlaylistListBox.ItemsSource = playbackViewModel.Playlist;
                BottomPlaylistListBox.SelectedIndex = playbackViewModel.PlaylistIndex;
            }

            BottomPlaylistPopup.IsOpen = !BottomPlaylistPopup.IsOpen;
        }

        private async void BottomPlaylistListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var playbackViewModel = App.PlaybackViewModel;

            if (playbackViewModel == null)
                return;

            if (!(BottomPlaylistListBox.SelectedItem is Models.Video video))
                return;

            await FullScreenOverlayWindow.RunPlaybackAsync(() => playbackViewModel.PlayFromPlaylistAsync(video));

            BottomPlaylistPopup.IsOpen = false;
        }

        private void BottomMoreButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            var subtitles = new System.Windows.Controls.MenuItem { Header = "Subtitles" };
            var off = new System.Windows.Controls.MenuItem { Header = "Off", IsCheckable = true,
                IsChecked = Services.Playback.SubtitleCueLoader.ActiveLanguage == null };
            off.Click += (s, args) => Services.Playback.SubtitleCueLoader.Clear();
            subtitles.Items.Add(off);
            var resource = App.PlaybackService?.CurrentResource;
            if (resource?.SubtitleTracks != null)
                foreach (var track in resource.SubtitleTracks)
                {
                    var item = new System.Windows.Controls.MenuItem { Header = track.Label, IsCheckable = true,
                        IsChecked = Services.Playback.SubtitleCueLoader.ActiveLanguage == track.Language };
                    item.Click += async (s, args) => await FullScreenOverlayWindow.RunPlaybackAsync(() => Services.Playback.SubtitleCueLoader.LoadAsync(track));
                    subtitles.Items.Add(item);
                }
            menu.Items.Add(subtitles);
            var audio = new System.Windows.Controls.MenuItem { Header = "Audio track", IsEnabled = resource != null };
            foreach (string language in resource?.AudioTrackLabels ?? new List<string>())
            {
                var item = new System.Windows.Controls.MenuItem { Header = language, IsCheckable = true,
                    IsChecked = language == (resource.Language ?? "Default") };
                item.Click += async (s, args) => await FullScreenOverlayWindow.RunPlaybackAsync(() => App.PlaybackViewModel.SetAudioLanguageAsync(language));
                audio.Items.Add(item);
            }
            menu.Items.Add(audio);
            var info = new System.Windows.Controls.MenuItem { Header = "Video info" };
            info.Click += (s, args) => MessageBox.Show(BottomVideoInfoTextBlock.Text +
                "\n" + resource?.Width + " × " + resource?.Height + "\nAudio: " + resource?.AudioBitrateKbps.ToString("0") + " kbps", "Video info");
            menu.Items.Add(info);
            BottomMoreButton.ContextMenu = menu;
            menu.PlacementTarget = BottomMoreButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }

        private void NavigationView_BackRequested(
            NavigationView sender,
            NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            NavigationView.IsBackEnabled = ContentFrame.CanGoBack;
        }

        private void NavigationView_SelectionChanged(
            NavigationView sender, 
            NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(new SettingsPage());
                return;
            }

            if (!(args.SelectedItem is NavigationViewItem item))
                return;

            string tag = item.Tag as string;

            switch (tag)
            {
                case "Home":
                    ContentFrame.Navigate(new HomePage());
                    break;

                case "Subscriptions":
                    ContentFrame.Navigate(new SubsPage());
                    break;

                case "History":
                    ContentFrame.Navigate(new HistoryPage());
                    break;

                case "Playlists":
                    ContentFrame.Navigate(new PlaylistPage());
                    break;

            }
        }

    }
}

