namespace uYouWin.Models
{
    /// <summary>
    /// Configuration for a YouTube Data API v3 compatible endpoint.
    ///
    /// This is intentionally generic so the user can point it at the
    /// official Google API, or at any drop-in / proxy-compatible
    /// third-party API that mimics the same REST surface.
    /// </summary>
    internal class YouTubeApiSettings
    {
        public string ApiKey { get; set; }

        public string BaseUrl { get; set; }

        public string ProviderName { get; set; }

        public bool IsEnabled { get; set; }

        public YouTubeApiSettings()
        {
            IsEnabled = true;
            ProviderName = "YouTube Data API v3";
            BaseUrl = "https://www.googleapis.com/youtube/v3/";
        }

        public bool HasApiKey
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ApiKey);
            }
        }
    }
}
