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
    /// HistoryPage.xaml 的互動邏輯
    /// </summary>
    public partial class HistoryPage : Page
    {
        public HistoryPage()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                try
                {
                    WatchList.ItemsSource = await App.LibraryService.GetHistoryAsync();
                    SearchList.ItemsSource = await App.LibraryService.GetSearchHistoryAsync();
                    StatusText.Text = WatchList.Items.Count == 0 ? "No watch history yet." : "Double-click an entry to open it.";
                }
                catch (Exception ex) { StatusText.Text = ex.Message; }
            };
        }

        private async void WatchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(WatchList.SelectedItem is Models.HistoryEntry entry))
                return;
            try
            {
                Models.Video video = Services.Cache.VisitedUrlCache.TryGet(entry.VideoId) ??
                    await App.YouTubeApiService.GetVideoAsync(entry.VideoId, System.Threading.CancellationToken.None);
                if (video != null)
                    NavigationService?.Navigate(new VideoPage(video));
                else
                    StatusText.Text = "This video is unavailable.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void SearchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SearchList.SelectedItem is Models.SearchHistoryEntry entry)
                NavigationService?.Navigate(new HomePage(entry.Query));
        }
    }
}
