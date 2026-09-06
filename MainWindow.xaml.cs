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
using ModernWpf.Controls;
using uYouWin.Views;

namespace uYouWin
{
    /// <summary>
    /// The main windows of the application, containing the navigation frame and the navigation view.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ContentFrame.Navigate(new HomePage());
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

                case "Library":
                    ContentFrame.Navigate(new LibraryPage());
                    break;
            }
        }

    }
}

