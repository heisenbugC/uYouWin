using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using uYouWin.Models;

namespace uYouWin.Views
{
    public class AddToPlaylistButton : Button
    {
        public AddToPlaylistButton()
        {
            Content = "Add to playlist";
            HorizontalAlignment = HorizontalAlignment.Left;
            Margin = new Thickness(0, 6, 0, 0);
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1) e.Handled = true;
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override async void OnClick()
        {
            Video video = DataContext as Video;
            if (DataContext is HistoryEntry entry)
                video = new Video
                {
                    Id = entry.VideoId, Title = entry.Title, ChannelId = entry.ChannelId,
                    ChannelTitle = entry.ChannelTitle, ThumbnailUrl = entry.ThumbnailUrl,
                    WebUrl = entry.WebUrl, YtUrl = "https://www.youtube.com/watch?v=" + entry.VideoId
                };
            if (video == null || string.IsNullOrWhiteSpace(video.Id)) return;
            IsEnabled = false;
            try
            {
                var playlists = await App.LibraryService.GetPlaylistsAsync();
                var dialog = new Window
                {
                    Title = "Add to playlist", Width = 420, SizeToContent = SizeToContent.Height,
                    ResizeMode = ResizeMode.NoResize, Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
                };
                dialog.SetResourceReference(BackgroundProperty, "SystemControlPageBackgroundChromeLowBrush");
                dialog.SetResourceReference(ForegroundProperty, "SystemControlForegroundBaseHighBrush");
                var panel = new StackPanel { Margin = new Thickness(20) };
                panel.Children.Add(new TextBlock { Text = video.Title, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(new TextBlock { Text = "Choose a playlist", Margin = new Thickness(0, 12, 0, 4) });
                var choices = new ComboBox { ItemsSource = playlists, DisplayMemberPath = "Title", SelectedIndex = playlists.Count > 0 ? 0 : -1 };
                panel.Children.Add(choices);
                panel.Children.Add(new TextBlock { Text = "Or create a new playlist", Margin = new Thickness(0, 12, 0, 4) });
                var name = new TextBox();
                panel.Children.Add(name);
                var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
                panel.Children.Add(status);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var add = new Button { Content = "Add", IsDefault = true, MinWidth = 72 };
                var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 72, Margin = new Thickness(8, 0, 0, 0) };
                buttons.Children.Add(add);
                buttons.Children.Add(cancel);
                panel.Children.Add(buttons);
                dialog.Content = panel;
                add.Click += async (sender, args) =>
                {
                    add.IsEnabled = false;
                    try
                    {
                        bool create = !string.IsNullOrWhiteSpace(name.Text);
                        Playlist playlist = create ? new Playlist { Title = name.Text.Trim() } : choices.SelectedItem as Playlist;
                        if (playlist == null) { status.Text = "Choose a playlist or enter a name."; return; }
                        var current = await App.LibraryService.GetPlaylistsAsync();
                        if (create && current.Any(p => string.Equals(p.Title, playlist.Title, StringComparison.OrdinalIgnoreCase)))
                        { status.Text = "That playlist already exists. Select it above."; return; }
                        if (!create)
                        {
                            playlist = current.FirstOrDefault(p => p.Id == playlist.Id);
                            if (playlist == null) { status.Text = "The playlist no longer exists."; return; }
                        }
                        playlist.VideoIds = playlist.VideoIds ?? new List<string>();
                        playlist.Videos = playlist.Videos ?? new List<Video>();
                        if (!playlist.VideoIds.Contains(video.Id)) playlist.VideoIds.Add(video.Id);
                        if (!playlist.Videos.Any(v => v.Id == video.Id)) playlist.Videos.Add(video);
                        if (create) await App.LibraryService.AddPlaylistAsync(playlist);
                        else await App.LibraryService.SavePlaylistAsync(playlist);
                        dialog.DialogResult = true;
                    }
                    catch (Exception ex) { status.Text = ex.Message; }
                    finally { add.IsEnabled = true; }
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Add to playlist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsEnabled = true; }
        }
    }
}
