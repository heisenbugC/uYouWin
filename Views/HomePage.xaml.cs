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
using uYouWin.Services.Invidious;

namespace uYouWin.Views
{
    /// <summary>
    /// HomePage.xaml 的互動邏輯
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private async void TestInvidious_Click(object sender, RoutedEventArgs e)
        {
            var httpClient = new HttpClient();

            var client = new InvidiousClient(
                httpClient,
                "https://yt.chocolatemoo53.com/");

            var service = new InvidiousService(client);

            var videos = await service.SearchAsync("大家车言论", CancellationToken.None);

            bool connected = await client.TestConnectionAsync(CancellationToken.None);

            MessageBox.Show(
                connected ? "Instance connected successfully!" : "Failed to connect instance.");
        }
    }
}
