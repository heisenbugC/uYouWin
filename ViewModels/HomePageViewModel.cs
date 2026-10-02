using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;
using uYouWin.Services.Preferences;
using uYouWin.Services.YouTube;

namespace uYouWin.ViewModels
{
    /// <summary>
    /// Backing view model for <see cref="Views.HomePage"/>. Owns the
    /// recommendations/search-results list and the commands used to
    /// refresh or search for videos.
    /// </summary>
    public class HomePageViewModel : ViewModelBase
    {
        private string _headerText = "Recommendations";
        private bool _isBusy;
        private bool _isShowingSearchResults;
        private string _query;
        private string _nextPageToken;
        private string _recommendationRegion;
        public bool HasMoreResults => !IsBusy && !string.IsNullOrEmpty(_nextPageToken);

        public ObservableCollection<Video> Videos { get; } =
            new ObservableCollection<Video>();

        public string HeaderText
        {
            get => _headerText;
            private set => SetProperty(ref _headerText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                SetProperty(ref _isBusy, value);
                OnPropertyChanged(nameof(HasMoreResults));
            }
        }

        public RelayCommand RefreshCommand { get; }

        public HomePageViewModel()
        {
            RefreshCommand = new RelayCommand(
                async () => await LoadRecommendationsAsync(),
                () => !IsBusy);
        }

        public async Task InitializeAsync()
        {
            YouTubeApiSettings settings = YouTubeApiSettingsStore.Load();

            if (settings.HasApiKey)
            {
                await LoadRecommendationsAsync();
            }
        }

        public async Task LoadRecommendationsAsync()
        {
            if (IsBusy) return;
            _nextPageToken = null;
            _isShowingSearchResults = false;
            HeaderText = "Recommendations";

            await RunLoadAsync(async token =>
            {
                AppPreferences preferences = AppPreferencesStore.Load();
                _recommendationRegion = preferences.RecommendationRegion;
                var page = await App.YouTubeApiService.GetPopularVideosPageAsync(_recommendationRegion, null, token);
                _nextPageToken = page.NextPageToken;
                return page.Videos;
            });
        }

        public async Task SearchAsync(string query)
        {
            if (IsBusy) return;
            if (string.IsNullOrWhiteSpace(query))
            {
                await LoadRecommendationsAsync();
                return;
            }

            _isShowingSearchResults = true;
            HeaderText = "Search results";
            _query = query.Trim();
            _nextPageToken = null;
            await RunLoadAsync(async token =>
            {
                var page = await App.YouTubeApiService.SearchPageAsync(_query, null, token);
                _nextPageToken = page.NextPageToken;
                await App.LibraryService.AddSearchHistoryAsync(_query);
                return page.Videos;
            });
        }

        public async Task LoadMoreAsync()
        {
            if (!HasMoreResults) return;
            IsBusy = true;
            try
            {
                string requestedToken = _nextPageToken;
                var page = _isShowingSearchResults
                    ? await App.YouTubeApiService.SearchPageAsync(_query, requestedToken, CancellationToken.None)
                    : await App.YouTubeApiService.GetPopularVideosPageAsync(_recommendationRegion, requestedToken, CancellationToken.None);
                foreach (Video video in page.Videos)
                    if (!Videos.Any(v => v.Id == video.Id))
                        Videos.Add(video);
                _nextPageToken = page.NextPageToken == requestedToken ? null : page.NextPageToken;
                HeaderText = _isShowingSearchResults ? "Search results" : "Recommendations";
            }
            catch (Exception ex) { HeaderText = "Could not load more: " + ex.Message; }
            finally { IsBusy = false; }
        }

        private async Task RunLoadAsync(
            Func<CancellationToken, Task<System.Collections.Generic.List<Video>>> loader)
        {
            IsBusy = true;

            try
            {
                var results = await loader(CancellationToken.None);

                Videos.Clear();

                foreach (Video video in results)
                {
                    Videos.Add(video);
                }
            }
            catch (YouTubeApiKeyMissingException)
            {
                Videos.Clear();
                HeaderText = _isShowingSearchResults
                    ? "Search results (no API key configured)"
                    : "Recommendations (no API key configured)";
            }
            catch (YouTubeApiException ex)
            {
                Videos.Clear();
                HeaderText = "Failed to load: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
