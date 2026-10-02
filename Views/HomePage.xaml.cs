using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using uYouWin.Models;
using uYouWin.Services.YouTube;
using uYouWin.ViewModels;

namespace uYouWin.Views
{
    /// <summary>
    /// HomePage.xaml 的互動邏輯
    /// </summary>
    public partial class HomePage : Page
    {
        private readonly HomePageViewModel _viewModel;
        private bool _initialized;
        private string _initialQuery;

        public HomePage()
        {
            InitializeComponent();

            _viewModel = new HomePageViewModel();
            DataContext = _viewModel;
            _viewModel.Videos.CollectionChanged += (s, e) =>
                ResultsListBox.ItemsSource = _viewModel.Videos;

            ResultsListBox.ItemsSource = _viewModel.Videos;

            Loaded += HomePage_Loaded;
        }

        public HomePage(string query) : this()
        {
            _initialQuery = query;
            QueryTextBox.Text = query;
        }

        private async void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadMoreAsync();
            ResultsHeaderTextBlock.Text = _viewModel.HeaderText;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;
            if (!string.IsNullOrWhiteSpace(_initialQuery))
            {
                await RunSearchAsync();
                return;
            }
            ResultsHeaderTextBlock.Text = _viewModel.HeaderText;

            await _viewModel.InitializeAsync();

            ResultsHeaderTextBlock.Text = _viewModel.HeaderText;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadRecommendationsAsync();

            ResultsHeaderTextBlock.Text = _viewModel.HeaderText;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await RunSearchAsync();
        }

        private async void QueryTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await RunSearchAsync();
            }
        }

        private async Task RunSearchAsync()
        {
            string query = QueryTextBox.Text;

            try
            {
                string videoId = Services.Cache.VisitedUrlCache.VideoIdFromUrl(query);
                if (videoId != null)
                {
                    var video = Services.Cache.VisitedUrlCache.TryGet(videoId) ??
                        await App.YouTubeApiService.GetVideoAsync(videoId, CancellationToken.None);
                    if (video == null)
                        throw new InvalidOperationException("Video unavailable.");
                    NavigationService?.Navigate(new VideoPage(video));
                    return;
                }
                await _viewModel.SearchAsync(query);

                ResultsHeaderTextBlock.Text = _viewModel.HeaderText;
            }
            catch (YouTubeApiKeyMissingException)
            {
                MessageBox.Show(
                    "No YouTube API key has been configured. " +
                    "Please add one on the Settings page to search for videos.",
                    "API key required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (YouTubeApiException ex)
            {
                MessageBox.Show(
                    "YouTube API request failed:\n" + ex.Message,
                    "Search error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unexpected error while searching:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(ResultsListBox.SelectedItem is Video video))
                return;

            if (NavigationService == null)
            {
                MessageBox.Show(
                    "HomePage is not hosted in a navigable frame.",
                    "Playback",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            NavigationService.Navigate(
                new VideoPage(video));
        }
    }
}
