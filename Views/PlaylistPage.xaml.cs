using System;
using System.Collections.Generic;
using System.Linq;
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

namespace uYouWin.Views
{
    /// <summary>
    /// PlaylistPage.xaml 的互動邏輯
    /// </summary>
    public partial class PlaylistPage : Page
    {
        private Models.Video _pendingVideo;
        private Models.Playlist SelectedPlaylist => PlaylistsList.SelectedItem as Models.Playlist;

        public PlaylistPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await RefreshAsync();
        }

        public PlaylistPage(Models.Video video) : this()
        {
            _pendingVideo = video;
            AddVideoButton.Content = "Add selected video";
        }

        private async Task RefreshAsync(string selectedId = null)
        {
            try
            {
                var playlists = await App.LibraryService.GetPlaylistsAsync();
                PlaylistsList.ItemsSource = playlists;
                PlaylistsList.SelectedItem = playlists.FirstOrDefault(p => p.Id == selectedId);
                StatusText.Text = playlists.Count == 0 ? "Create a playlist or import a Takeout playlist in Settings." : "Double-click a video to play this playlist.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void PlaylistsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideosList.ItemsSource = SelectedPlaylist?.Videos;
            PlaylistName.Text = SelectedPlaylist?.Title ?? "";
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlaylistName.Text))
            {
                StatusText.Text = "Enter a playlist name.";
                return;
            }
            try
            {
                var playlist = new Models.Playlist { Title = PlaylistName.Text.Trim() };
                await App.LibraryService.AddPlaylistAsync(playlist);
                await RefreshAsync(playlist.Id);
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            var playlist = SelectedPlaylist;
            if (playlist == null || string.IsNullOrWhiteSpace(PlaylistName.Text))
                return;
            playlist.Title = PlaylistName.Text.Trim();
            await SaveAsync(playlist);
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPlaylist == null || MessageBox.Show("Delete this playlist?", "Playlists", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            try
            {
                await App.LibraryService.DeletePlaylistAsync(SelectedPlaylist.Id);
                await RefreshAsync();
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private async void AddVideo_Click(object sender, RoutedEventArgs e)
        {
            var video = _pendingVideo ?? App.PlaybackViewModel.CurrentVideo;
            var playlist = SelectedPlaylist;
            if (video == null || playlist == null)
            {
                StatusText.Text = "Select a playlist and a video first.";
                return;
            }
            if (!playlist.VideoIds.Contains(video.Id))
            {
                playlist.VideoIds.Add(video.Id);
                playlist.Videos.Add(video);
                await SaveAsync(playlist);
            }
        }

        private async void RemoveVideo_Click(object sender, RoutedEventArgs e)
        {
            var playlist = SelectedPlaylist;
            if (playlist != null && VideosList.SelectedItem is Models.Video video)
            {
                playlist.VideoIds.Remove(video.Id);
                playlist.Videos.Remove(video);
                await SaveAsync(playlist);
            }
        }

        private async void MoveUp_Click(object sender, RoutedEventArgs e) => await MoveAsync(-1);
        private async void MoveDown_Click(object sender, RoutedEventArgs e) => await MoveAsync(1);

        private async Task MoveAsync(int offset)
        {
            var playlist = SelectedPlaylist;
            int index = VideosList.SelectedIndex;
            if (playlist == null || index < 0 || index + offset < 0 || index + offset >= playlist.Videos.Count)
                return;
            var video = playlist.Videos[index];
            playlist.Videos.RemoveAt(index);
            playlist.Videos.Insert(index + offset, video);
            playlist.VideoIds = playlist.Videos.Select(v => v.Id).ToList();
            await SaveAsync(playlist);
            VideosList.SelectedIndex = index + offset;
        }

        private async Task SaveAsync(Models.Playlist playlist)
        {
            try
            {
                await App.LibraryService.SavePlaylistAsync(playlist);
                await RefreshAsync(playlist.Id);
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void VideosList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedPlaylist != null && VideosList.SelectedItem is Models.Video video)
                NavigationService?.Navigate(new VideoPage(video, SelectedPlaylist.Videos.ToList()));
        }
    }
}
