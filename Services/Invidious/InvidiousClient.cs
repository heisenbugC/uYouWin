using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace uYouWin.Services.Invidious
{
    internal class InvidiousClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public InvidiousClient(
            HttpClient httpClient, string baseUrl)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException(
                    "Base URL cannot be null or whitespace.", nameof(baseUrl));
            }

            _httpClient = httpClient;
            _baseUrl = baseUrl;
        }

        public async Task<JToken> GetAsync(
            string endpoint,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentNullException(
                    "Endpoint cannot be empty.",
                    nameof(endpoint));

            string url = _baseUrl + "api/v1/" + endpoint;

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 6.1; Win64; x64) " + 
                    "AppleWebKit/537.36 (KHTML, like Gecko) " + 
                    "Chrome/109.0.0.0 Safari/537.36 " + 
                    "Edg/109.0.1518.12"); 

                request.Headers.Accept.ParseAdd("application/json"); 

                System.Diagnostics.Debug.WriteLine("Request URL: " + url); 

                System.Diagnostics.Debug.WriteLine("Request headers:"); 

                foreach (var header in request.Headers)
                { 
                    System.Diagnostics.Debug.WriteLine(
                    header.Key + ": " + string.Join(", ", header.Value));

                }

                using (
                    HttpResponseMessage response = await _httpClient.SendAsync(
                        request, cancellationToken))
                {
                    string body = await response.Content.ReadAsStringAsync(); 
                    
                    System.Diagnostics.Debug.WriteLine(
                        "Status: " + (int)response.StatusCode + " " + 
                        response.ReasonPhrase); 
                    
                    System.Diagnostics.Debug.WriteLine("Response headers:"); 
                    
                    foreach (var header in response.Headers) 
                    { 
                        System.Diagnostics.Debug.WriteLine(
                        header.Key + ": " + string.Join(", ", header.Value));
                    } System.Diagnostics.Debug.WriteLine("Response body: " + body);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvidiousException(
                            "Invidious request failed with HTTP" + 
                            (int)response.StatusCode + 
                            " (" + response.ReasonPhrase + ")",
                            (int)response.StatusCode, body);
                    }

                    try
                    {
                        return JToken.Parse(body);

                    }
                    catch (Exception e)
                    {
                        throw new InvidiousException(
                            "Invidious returned invalid JSON: " +
                            e.Message,
                            (int)response.StatusCode,
                            body);
                    }
                    
                }
            }
        }


        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                JToken result = await GetAsync("stats", cancellationToken);

                return result != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Invidious connection failed: " + ex.Message);

                return false;
            }
        }

        public string ResolveUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                return uri.ToString();

            return _baseUrl + url.TrimStart('/');
        }
    }
}
