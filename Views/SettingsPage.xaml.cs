using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
using Microsoft.Win32;
using uYouWin.Models;
using uYouWin.Services.Archive;
using uYouWin.Services.Playback;
using uYouWin.Services.Preferences;
using uYouWin.Services.YouTube;

namespace uYouWin.Views
{
    /// <summary>
    /// SettingsPage.xaml 的互動邏輯
    /// </summary>
    public partial class SettingsPage : Page
    {
        private readonly YouTubeApiSettings _settings;
        private readonly AppPreferences _preferences;
        private readonly PlaybackSettings _playbackSettings;

        public SettingsPage()
        {
            InitializeComponent();

            _settings = YouTubeApiSettingsStore.Load();

            ProviderNameTextBox.Text = _settings.ProviderName;
            BaseUrlTextBox.Text = _settings.BaseUrl;
            ApiKeyPasswordBox.Password = _settings.ApiKey ?? string.Empty;
            EnabledCheckBox.IsChecked = _settings.IsEnabled;

            _preferences = AppPreferencesStore.Load();

            RecommendationRegionTextBox.Text = _preferences.RecommendationRegion;

            _playbackSettings = PlaybackSettingsStore.Load();

            VideoQualityComboBox.SelectedValue = _playbackSettings.MaxVideoHeight.ToString();
            AudioQualityComboBox.SelectedValue = _playbackSettings.TargetAudioBitrateKbps.ToString();
            if (VideoQualityComboBox.SelectedIndex < 0) VideoQualityComboBox.SelectedIndex = 2;
            if (AudioQualityComboBox.SelectedIndex < 0) AudioQualityComboBox.SelectedIndex = 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.ProviderName = ProviderNameTextBox.Text;
            _settings.BaseUrl = BaseUrlTextBox.Text;
            _settings.ApiKey = ApiKeyPasswordBox.Password;
            _settings.IsEnabled = EnabledCheckBox.IsChecked == true;

            _preferences.RecommendationRegion = RecommendationRegionTextBox.Text;

            try
            {
                YouTubeApiSettingsStore.Save(_settings);
                AppPreferencesStore.Save(_preferences);

                App.RefreshYouTubeApiSettings();

                ShowSuccess(StatusTextBlock, "Settings saved.");
            }
            catch (Exception ex)
            {
                ShowError(StatusTextBlock, "Failed to save settings: " + ex.Message);
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Testing connection...";

            var testSettings = new YouTubeApiSettings
            {
                ProviderName = ProviderNameTextBox.Text,
                BaseUrl = BaseUrlTextBox.Text,
                ApiKey = ApiKeyPasswordBox.Password,
                IsEnabled = true
            };

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var client = new YouTubeApiClient(httpClient, testSettings, cacheEnabled: false);
                    var service = new YouTubeApiService(client);

                    var results = await service.GetPopularVideosAsync(
                        "US",
                        CancellationToken.None);

                    StatusTextBlock.Text =
                        "Connection succeeded. Received " +
                        results.Count +
                        " result(s).";

                    MessageBox.Show(
                        "Successfully connected to \"" + testSettings.ProviderName + "\".\n\n" +
                        "Received " + results.Count + " result(s) for a test search.",
                        "Test Connection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (YouTubeApiKeyMissingException)
            {
                StatusTextBlock.Text =
                    "Please enter an API key before testing the connection.";

                MessageBox.Show(
                    "Please enter an API key before testing the connection.",
                    "Test Connection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (YouTubeApiException ex)
            {
                StatusTextBlock.Text =
                    "Connection failed: " + ex.Message;

                MessageBox.Show(
                    "Connection failed:\n\n" + ex.Message,
                    "Test Connection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text =
                    "Unexpected error: " + ex.Message;

                MessageBox.Show(
                    "Unexpected error:\n\n" + ex.Message,
                    "Test Connection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SavePlaybackSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(VideoQualityComboBox.SelectedValue as string, out int maxHeight) || maxHeight < 0)
            {
                ShowError(PlaybackSettingsStatusTextBlock, "Maximum video height must be a positive number.");
                return;
            }

            if (!int.TryParse(AudioQualityComboBox.SelectedValue as string, out int bitrate) || bitrate <= 0)
            {
                ShowError(PlaybackSettingsStatusTextBlock, "Target audio bitrate must be a positive number.");
                return;
            }

            _playbackSettings.MaxVideoHeight = maxHeight;
            _playbackSettings.TargetAudioBitrateKbps = bitrate;


            try
            {
                PlaybackSettingsStore.Save(_playbackSettings);
                AppPreferencesStore.Save(_preferences);

                ShowSuccess(PlaybackSettingsStatusTextBlock, "Saved. Applies to the next video opened.");
            }
            catch (Exception ex)
            {
                ShowError(PlaybackSettingsStatusTextBlock, "Failed to save playback settings: " + ex.Message);
            }
        }

        private static void ShowSuccess(TextBlock textBlock, string message)
        {
            textBlock.Text = "\u2705 " + message;
            textBlock.Foreground = new SolidColorBrush(Colors.Green);
        }

        private static void ShowError(TextBlock textBlock, string message)
        {
            textBlock.Text = message;
            textBlock.Foreground = new SolidColorBrush(Colors.Red);
        }

        private async void ImportSubscriptions_Click(object sender, RoutedEventArgs e)
        {
            string path = PickArchiveFile();

            if (path == null)
                return;

            ArchiveImportStatusTextBlock.Text = "Importing subscriptions...";

            try
            {
                var importer = new GoogleTakeoutImporter();

                var items = await Task.Run(() => importer.ImportSubscriptionsAsync(path, CancellationToken.None));
                if (items.Count == 0) throw new System.IO.InvalidDataException("No subscription records found. Select a subscriptions CSV.");
                await App.LibraryService.ImportSubscriptionsAsync(items);
                ArchiveImportStatusTextBlock.Text = "Processed " + items.Count + " subscriptions.";
            }
            catch (Exception ex)
            {
                ArchiveImportStatusTextBlock.Text =
                    "Failed to import subscriptions: " + ex.Message;
            }

            await Task.CompletedTask;
        }

        private async void ImportHistory_Click(object sender, RoutedEventArgs e)
        {
            string path = PickArchiveFile();

            if (path == null)
                return;

            try
            {
                var items = await Task.Run(() => new GoogleTakeoutImporter().ImportHistoryAsync(path, CancellationToken.None));
                if (items.Count == 0) throw new System.IO.InvalidDataException("No watch records found. Select a watch-history CSV.");
                await App.LibraryService.ImportHistoryAsync(items);
                ArchiveImportStatusTextBlock.Text = "Processed " + items.Count + " watch-history entries.";
            }
            catch (Exception ex) { ArchiveImportStatusTextBlock.Text = ex.Message; }
        }

        private async void ImportPlaylists_Click(object sender, RoutedEventArgs e)
        {
            string path = PickArchiveFile();

            if (path == null)
                return;

            try
            {
                var items = await Task.Run(() => new GoogleTakeoutImporter().ImportPlaylistsAsync(path, CancellationToken.None));
                if (items.Count == 0) throw new System.IO.InvalidDataException("No playlist videos found. Select a playlist videos CSV.");
                await App.LibraryService.ImportPlaylistsAsync(items);
                ArchiveImportStatusTextBlock.Text = "Processed " + items.Count + " playlists.";
            }
            catch (Exception ex) { ArchiveImportStatusTextBlock.Text = ex.Message; }
        }

        private async void ImportSearchHistory_Click(object sender, RoutedEventArgs e)
        {
            string path = PickArchiveFile();

            if (path == null)
                return;

            try
            {
                var items = await Task.Run(() => new GoogleTakeoutImporter().ImportSearchHistoryAsync(path, CancellationToken.None));
                if (items.Count == 0) throw new System.IO.InvalidDataException("No search records found. Select a search-history CSV.");
                await App.LibraryService.ImportSearchHistoryAsync(items);
                ArchiveImportStatusTextBlock.Text = "Processed " + items.Count + " search-history entries.";
            }
            catch (Exception ex) { ArchiveImportStatusTextBlock.Text = ex.Message; }
        }

        private string PickArchiveFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Takeout CSV files (*.csv)|*.csv",
                Title = "Select an archive export file"
            };

            bool? result = dialog.ShowDialog();

            return result == true ? dialog.FileName : null;
        }
    }
}
