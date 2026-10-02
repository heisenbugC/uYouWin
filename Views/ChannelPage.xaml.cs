using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using uYouWin.Models;

namespace uYouWin.Views
{
    public partial class ChannelPage : Page
    {
        private readonly string _channelId;
        private Channel _channel;
        private bool _subscribed;
        private CancellationTokenSource _load;
        private readonly System.Collections.ObjectModel.ObservableCollection<Video> _videos =
            new System.Collections.ObjectModel.ObservableCollection<Video>();
        private string _nextPageToken;

        public ChannelPage(string channelId)
        {
            InitializeComponent();
            _channelId = channelId;
            VideosList.ItemsSource = _videos;
            Loaded += LoadChannel;
            Unloaded += (s, e) => _load?.Cancel();
        }

        private async void LoadChannel(object sender, RoutedEventArgs e)
        {
            _load?.Dispose();
            _load = new CancellationTokenSource();
            var token = _load.Token;
            SubscribeButton.IsEnabled = false;
            LoadMoreButton.IsEnabled = false;
            _nextPageToken = null;
            _videos.Clear();
            StatusText.Text = "Loading channel¡K";
            try
            {
                _channel = await App.YouTubeApiService.GetChannelAsync(_channelId, token);
                token.ThrowIfCancellationRequested();
                if (_channel == null)
                {
                    StatusText.Text = "Channel unavailable.";
                    return;
                }
                ChannelName.Text = _channel.Name;
                Title = _channel.Name;
                ChannelHandle.Text = _channel.CustomUrl ?? "";
                ChannelAvatar.Source = Uri.TryCreate(_channel.ThumbnailUrl, UriKind.Absolute, out Uri avatarUri)
                    ? new System.Windows.Media.Imaging.BitmapImage(avatarUri) : null;
                DescriptionText.Text = string.IsNullOrWhiteSpace(_channel.Description) ? "No description provided." : _channel.Description;
                StatisticsText.Text = (_channel.HiddenSubscriberCount ? "Subscribers hidden" :
                    (_channel.SubscriberCount?.ToString("N0") ?? "Unavailable") + " subscribers") + " ¡P " +
                    (_channel.VideoCount?.ToString("N0") ?? "Unavailable") + " public videos";
                ChannelDetails.Text = "Created: " + (_channel.PublishedAt?.ToString("d") ?? "Unavailable") +
                    "\nCountry: " + (_channel.Country ?? "Not provided") +
                    "\nTotal public views: " + (_channel.ViewCount?.ToString("N0") ?? "Unavailable") +
                    "\nChannel ID: " + _channel.ChannelId;
                ChannelUrl.Text = _channel.WebUrl;
                _subscribed = (await App.LibraryService.GetSubscriptionsAsync()).Any(s => s.ChannelId == _channelId);
                SubscribeButton.Content = _subscribed ? "Unsubscribe" : "Subscribe";
                SubscribeButton.IsEnabled = true;
                var page = await App.YouTubeApiService.GetChannelVideosPageAsync(_channelId, null, token);
                token.ThrowIfCancellationRequested();
                foreach (Video video in page.Videos) _videos.Add(video);
                _nextPageToken = page.NextPageToken;
                LoadMoreButton.IsEnabled = !string.IsNullOrEmpty(_nextPageToken);
                StatusText.Text = _videos.Count == 0 ? "No public uploads available." : "";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private async void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_nextPageToken) || _load == null || _load.IsCancellationRequested) return;
            LoadMoreButton.IsEnabled = false;
            var token = _load.Token;
            try
            {
                string requested = _nextPageToken;
                var page = await App.YouTubeApiService.GetChannelVideosPageAsync(_channelId, requested, token);
                token.ThrowIfCancellationRequested();
                foreach (Video video in page.Videos)
                    if (!_videos.Any(v => v.Id == video.Id)) _videos.Add(video);
                _nextPageToken = page.NextPageToken == requested ? null : page.NextPageToken;
                StatusText.Text = "";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { StatusText.Text = ex.Message; }
            finally { LoadMoreButton.IsEnabled = !token.IsCancellationRequested && !string.IsNullOrEmpty(_nextPageToken); }
        }

        private async void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            if (_channel == null)
                return;
            try
            {
                if (_subscribed)
                    await App.LibraryService.RemoveSubscriptionAsync(_channelId);
                else
                    await App.LibraryService.AddSubscriptionAsync(new Subscription
                    {
                        ChannelId = _channelId,
                        ChannelName = _channel.Name,
                        ChannelUrl = _channel.WebUrl,
                        ThumbnailUrl = _channel.ThumbnailUrl
                    });
                _subscribed = !_subscribed;
                SubscribeButton.Content = _subscribed ? "Unsubscribe" : "Subscribe";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void VideosList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VideosList.SelectedItem is Video video)
                NavigationService?.Navigate(new VideoPage(video));
        }
    }
}
