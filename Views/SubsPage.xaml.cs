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
    /// SubsPage.xaml 的互動邏輯
    /// </summary>
    public partial class SubsPage : Page
    {
        public SubsPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            try
            {
                SubscriptionsList.ItemsSource = await App.LibraryService.GetSubscriptionsAsync();
                StatusText.Text = SubscriptionsList.Items.Count == 0 ? "Subscribe from a channel page or import Takeout subscriptions in Settings." : "";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void OpenChannel_Click(object sender, RoutedEventArgs e)
        {
            if (SubscriptionsList.SelectedItem is Models.Subscription subscription)
                NavigationService?.Navigate(new ChannelPage(subscription.ChannelId));
        }

        private async void Unsubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (!(SubscriptionsList.SelectedItem is Models.Subscription subscription))
                return;
            try
            {
                await App.LibraryService.RemoveSubscriptionAsync(subscription.ChannelId);
                await RefreshAsync();
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }
    }
}
