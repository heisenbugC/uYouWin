using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using uYouWin.Models;
using uYouWin.Services.Playback;

namespace uYouWin.Views
{
    /// <summary>
    /// FullScreenOverlayWindow.xaml 的互動邏輯
    /// </summary>
    public partial class FullScreenOverlayWindow : Window
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private FrameworkElement _target;
        private Window _host;
        private bool _playlistOpen;
        private DateTime _lastInput = DateTime.UtcNow;
        private readonly BitmapImage _play = new BitmapImage(new Uri("pack://application:,,,/Assets/icons/play.png"));
        private readonly BitmapImage _pause = new BitmapImage(new Uri("pack://application:,,,/Assets/icons/pause.png"));

        public FullScreenOverlayWindow()
        {
            InitializeComponent();
            MediaScrubber.Attach(ScrubBar);
            _timer.Tick += Tick;
            Closed += (s, e) => Detach();
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    MainWindow.Instance?.SetFullScreen(false);
            };
        }

        public void Attach(FrameworkElement target)
        {
            Detach();
            _target = target;
            _host = Window.GetWindow(target);
            Owner = _host;
            target.LayoutUpdated += Align;
            if (_host != null)
            {
                _host.LocationChanged += Align;
                _host.StateChanged += Align;
            }
            _timer.Start();
            Align(null, EventArgs.Empty);
        }

        public void Detach()
        {
            _timer.Stop();
            LoadingRing.IsActive = false;
            LoadingIndicator.Visibility = Visibility.Collapsed;
            if (_target != null)
                _target.LayoutUpdated -= Align;
            if (_host != null)
            {
                _host.LocationChanged -= Align;
                _host.StateChanged -= Align;
            }
            _target = null;
            _host = null;
        }

        private void Align(object sender, EventArgs e)
        {
            if (_target == null || !_target.IsVisible || _target.ActualWidth <= 0 ||
                _target.ActualHeight <= 0 || _host?.WindowState == WindowState.Minimized)
            {
                Hide();
                return;
            }
            var source = PresentationSource.FromVisual(_target);
            if (source?.CompositionTarget == null)
                return;
            Point origin = source.CompositionTarget.TransformFromDevice.Transform(_target.PointToScreen(new Point()));
            Left = origin.X;
            Top = origin.Y;
            Width = _target.ActualWidth;
            Height = _target.ActualHeight;
            if (!IsVisible)
                Show();
        }

        private void Tick(object sender, EventArgs e)
        {
            var playback = App.PlaybackViewModel;
            if (playback == null)
                return;
            var state = App.PlaybackService?.MediaPlayer?.Player.PlaybackSession.PlaybackState;
            bool loading = playback.IsOpening || state == Windows.Media.Playback.MediaPlaybackState.Opening ||
                state == Windows.Media.Playback.MediaPlaybackState.Buffering;
            LoadingIndicator.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            LoadingRing.IsActive = loading && IsVisible;
            if (!ScrubBar.IsMouseCaptureWithin)
            {
                ScrubBar.Maximum = Math.Max(1, playback.Duration);
                ScrubBar.Value = playback.Position;
            }
            PlayPauseIcon.Source = playback.IsPlaying ? _pause : _play;
            PreviousButton.IsEnabled = playback.HasPrevious && !playback.IsOpening;
            NextButton.IsEnabled = playback.HasNext && !playback.IsOpening;
            PreviousButton.Visibility = NextButton.Visibility = playback.Playlist == null ? Visibility.Collapsed : Visibility.Visible;
            if (!ReferenceEquals(PlaylistListBox.ItemsSource, playback.Playlist))
                PlaylistListBox.ItemsSource = playback.Playlist;
            if (!PlaylistListBox.IsMouseOver)
                PlaylistListBox.SelectedIndex = playback.PlaylistIndex;
            FullScreenIcon.Glyph = MainWindow.Instance?.IsFullScreen == true ? "\uE73F" : "\uE740";
            SubtitleText.Text = SubtitleCueLoader.TextAt(playback.Position);
            var video = playback.CurrentVideo;
            var resource = App.PlaybackService.CurrentResource;
            VideoInfoTextBlock.Text = video == null ? "Nothing playing" :
                video.Title + "\n" + video.ChannelTitle + "\n" + resource?.Width + " × " + resource?.Height +
                "\nAudio: " + resource?.AudioBitrateKbps.ToString("0") + " kbps";
            if (!ControlsBar.IsMouseOver && !ScrubBar.IsMouseCaptureWithin && !_playlistOpen &&
                !VolumePopup.IsOpen && !InfoPopup.IsOpen && ContextMenu?.IsOpen != true &&
                DateTime.UtcNow - _lastInput > TimeSpan.FromSeconds(3))
                ControlsBar.Visibility = Visibility.Collapsed;
        }

        private void Root_MouseMove(object sender, MouseEventArgs e)
        {
            _lastInput = DateTime.UtcNow;
            ControlsBar.Visibility = Visibility.Visible;
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && ReferenceEquals(e.OriginalSource, Root))
                FullScreen_Click(sender, e);
        }

        private void ControlsBar_MouseEnter(object sender, MouseEventArgs e) => Root_MouseMove(sender, e);
        private void ControlsBar_MouseLeave(object sender, MouseEventArgs e) => _lastInput = DateTime.UtcNow;
        private void PlayPause_Click(object sender, RoutedEventArgs e) => App.PlaybackViewModel?.TogglePlayPause();
        private void SeekBack_Click(object sender, RoutedEventArgs e) => App.PlaybackViewModel?.SeekRelative(-5);
        private void SeekForward_Click(object sender, RoutedEventArgs e) => App.PlaybackViewModel?.SeekRelative(5);
        private async void Previous_Click(object sender, RoutedEventArgs e) => await RunPlaybackAsync(() => App.PlaybackViewModel.PlayPreviousAsync());
        private async void Next_Click(object sender, RoutedEventArgs e) => await RunPlaybackAsync(() => App.PlaybackViewModel.PlayNextAsync());

        public static async Task RunPlaybackAsync(Func<Task> action)
        {
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Playback", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Volume_Click(object sender, RoutedEventArgs e)
        {
            VolumeSlider.Value = App.PlaybackViewModel.Volume;
            VolumePopup.IsOpen = !VolumePopup.IsOpen;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (App.PlaybackViewModel == null || VolumeIcon == null)
                return;
            App.PlaybackViewModel.Volume = e.NewValue;
            VolumeIcon.Glyph = e.NewValue <= 0 ? "\uE74F" : "\uE995";
        }

        private void Subtitle_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var off = new MenuItem { Header = "Off", IsCheckable = true, IsChecked = SubtitleCueLoader.ActiveLanguage == null };
            off.Click += (s, args) => SubtitleCueLoader.Clear();
            menu.Items.Add(off);
            var tracks = App.PlaybackService.CurrentResource?.SubtitleTracks;
            if (tracks != null)
                foreach (SubtitleTrack track in tracks)
                {
                    var item = new MenuItem { Header = track.Label, IsCheckable = true, IsChecked = SubtitleCueLoader.ActiveLanguage == track.Language };
                    item.Click += async (s, args) => await RunPlaybackAsync(() => SubtitleCueLoader.LoadAsync(track));
                    menu.Items.Add(item);
                }
            ContextMenu = menu;
            menu.PlacementTarget = (UIElement)sender;
            menu.Placement = PlacementMode.Top;
            menu.IsOpen = true;
        }

        private void Info_Click(object sender, RoutedEventArgs e) => InfoPopup.IsOpen = !InfoPopup.IsOpen;

        private void Playlist_Click(object sender, RoutedEventArgs e)
        {
            _playlistOpen = !_playlistOpen;
            PlaylistPanel.Visibility = Visibility.Visible;
            var animation = new DoubleAnimation(_playlistOpen ? 0 : PlaylistPanel.ActualWidth, TimeSpan.FromMilliseconds(200));
            animation.Completed += (s, args) =>
            {
                if (!_playlistOpen)
                    PlaylistPanel.Visibility = Visibility.Collapsed;
            };
            PlaylistPanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private async void PlaylistListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistListBox.SelectedItem is Video video)
                await RunPlaybackAsync(() => App.PlaybackViewModel.PlayFromPlaylistAsync(video));
        }

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
                MainWindow.Instance.SetFullScreen(!MainWindow.Instance.IsFullScreen);
        }
    }

    internal static class MediaScrubber
    {
        public static void Attach(Slider slider)
        {
            Action<MouseEventArgs> move = e =>
            {
                var track = slider.Template.FindName("PART_Track", slider) as Track;
                if (track == null)
                    return;
                double thumb = track.Thumb?.ActualWidth ?? 0;
                double width = track.ActualWidth - thumb;
                if (width > 0)
                    slider.Value = slider.Minimum + Math.Max(0, Math.Min(1,
                        (e.GetPosition(track).X - thumb / 2) / width)) * (slider.Maximum - slider.Minimum);
            };
            slider.PreviewMouseLeftButtonDown += (s, e) =>
            {
                slider.Focus();
                slider.CaptureMouse();
                move(e);
                e.Handled = true;
            };
            slider.PreviewMouseMove += (s, e) =>
            {
                if (slider.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                    move(e);
            };
            slider.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (!slider.IsMouseCaptured)
                    return;
                move(e);
                App.PlaybackViewModel?.Seek(slider.Value);
                slider.ReleaseMouseCapture();
                e.Handled = true;
            };
            slider.PreviewKeyUp += (s, e) =>
            {
                if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
                    App.PlaybackViewModel?.Seek(slider.Value);
            };
        }
    }
}
