using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using uYouWin.Models;
using uYouWin.Services.Cache;

namespace uYouWin.Services.YouTube
{
    /// <summary>
    /// Thin HTTP client for the YouTube Data API v3 REST surface.
    ///
    /// The base URL and API key are read from <see cref="YouTubeApiSettings"/>
    /// so the same client can talk to the official Google endpoint or to
    /// any third-party service that implements a compatible API shape
    /// (endpoint paths + "key" query parameter).
    /// </summary>
    internal class YouTubeApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly YouTubeApiSettings _settings;
        internal bool CacheEnabled { get; }
        internal string CacheScope { get; }

        public YouTubeApiClient(
            HttpClient httpClient,
            YouTubeApiSettings settings,
            bool cacheEnabled = true)
        {
            _httpClient =
                httpClient ??
                throw new ArgumentNullException(nameof(httpClient));

            _settings =
                settings ??
                throw new ArgumentNullException(nameof(settings));
            CacheEnabled = cacheEnabled;
            CacheScope = DiskCache.Hash((_settings.BaseUrl ?? "").TrimEnd('/') + "\n" + _settings.ApiKey);
        }

        public async Task<JToken> GetAsync(
            string endpoint,
            string query,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    "Endpoint cannot be empty.",
                    nameof(endpoint));

            if (!_settings.HasApiKey)
                throw new YouTubeApiKeyMissingException();

            cancellationToken.ThrowIfCancellationRequested();
            string cacheKey = CacheScope + "\n" + endpoint + "\n" + query;
            if (CacheEnabled)
            {
                JToken cached = await Task.Run(() => DiskCache.TryGet<JToken>("api", cacheKey), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (cached != null) return cached;
            }

            string baseUrl =
                string.IsNullOrWhiteSpace(_settings.BaseUrl)
                    ? "https://www.googleapis.com/youtube/v3/"
                    : _settings.BaseUrl;

            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
                baseUrl += "/";

            string separator =
                string.IsNullOrEmpty(query) ? "" : "&";

            string url =
                baseUrl +
                endpoint +
                "?" +
                query +
                separator +
                "key=" +
                Uri.EscapeDataString(_settings.ApiKey);

            using (var request =
                   new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Accept.ParseAdd("application/json");

                using (HttpResponseMessage response =
                       await _httpClient.SendAsync(
                           request,
                           cancellationToken))
                {
                    string body =
                        await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new YouTubeApiException(
                            "YouTube API request failed with HTTP " +
                            (int)response.StatusCode +
                            " (" + response.ReasonPhrase + ")",
                            (int)response.StatusCode,
                            body);
                    }

                    try
                    {
                        JToken result = JToken.Parse(body);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (CacheEnabled && result is JObject && result["error"] == null && result["items"] is JArray)
                            await Task.Run(() => DiskCache.Save("api", cacheKey, result), cancellationToken);
                        return result;
                    }
                    catch (Newtonsoft.Json.JsonException ex)
                    {
                        throw new YouTubeApiException(
                            "YouTube API returned invalid JSON: " +
                            ex.Message,
                            (int)response.StatusCode,
                            body);
                    }
                }
            }
        }
    }
}
