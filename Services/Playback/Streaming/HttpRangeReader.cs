using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace uYouWin.Services.Playback.Streaming
{
    public sealed class HttpRangeReader : IDisposable
    {
        private const int CacheSize = 256 * 1024;

        private readonly HttpClient _httpClient;
        private readonly Uri _uri;

        private readonly object _cacheLock =
            new object();

        private byte[] _cache;
        private long _cacheStart;
        private long _cacheEnd;

        private bool _disposed;

        public long? Length { get; private set; }

        public HttpRangeReader(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException(
                    "URL cannot be empty.",
                    nameof(url));

            _uri = new Uri(
                url,
                UriKind.Absolute);

            if (_uri.Scheme != Uri.UriSchemeHttp &&
                _uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    "Only HTTP and HTTPS URLs are supported.",
                    nameof(url));
            }

            var handler = new HttpClientHandler
            {
                AutomaticDecompression =
                    DecompressionMethods.None
            };

            _httpClient = new HttpClient(handler);

            _httpClient.Timeout =
                TimeSpan.FromSeconds(30);

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 " +
                "(KHTML, like Gecko) " +
                "Chrome/151.0.0.0 Safari/537.36");

            _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
                "*/*");
        }

        public async Task<byte[]> ReadAsync(
            long position,
            int count,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (position < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(position));

            if (count <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(count));

            if (TryReadCache(
                    position,
                    count,
                    out byte[] cached))
            {
                return cached;
            }

            if (count <= CacheSize)
            {
                int requestSize = CacheSize;

                if (Length.HasValue)
                {
                    long remaining =
                        Length.Value - position;

                    if (remaining <= 0)
                        return new byte[0];

                    if (remaining < requestSize)
                        requestSize = (int)remaining;
                }

                byte[] fetched =
                    await RequestRangeAsync(
                        position,
                        requestSize,
                        cancellationToken);

                lock (_cacheLock)
                {
                    _cache = fetched;
                    _cacheStart = position;
                    _cacheEnd =
                        position + fetched.Length;
                }

                if (fetched.Length < count)
                {
                    throw new EndOfStreamException(
                        "HTTP server returned fewer bytes than requested.");
                }

                byte[] result =
                    new byte[count];

                Buffer.BlockCopy(
                    fetched,
                    0,
                    result,
                    0,
                    count);

                return result;
            }

            return await RequestRangeAsync(
                position,
                count,
                cancellationToken);
        }

        private async Task<byte[]> RequestRangeAsync(
            long position,
            int count,
            CancellationToken cancellationToken)
        {
            long end =
                position + count - 1;

            if (end < position)
                throw new OverflowException(
                    "HTTP range exceeded Int64.");

            using (var request =
                   new HttpRequestMessage(
                       HttpMethod.Get,
                       _uri))
            {
                request.Headers.Range =
                    new RangeHeaderValue(
                        position,
                        end);

                using (HttpResponseMessage response =
                       await _httpClient.SendAsync(
                           request,
                           HttpCompletionOption.ResponseHeadersRead,
                           cancellationToken))
                {
                    if (response.StatusCode ==
                        HttpStatusCode.PartialContent)
                    {
                        if (response.Content.Headers.ContentRange != null &&
                            response.Content.Headers.ContentRange.Length.HasValue)
                        {
                            Length =
                                response.Content.Headers.ContentRange.Length.Value;
                        }

                        byte[] data =
                            await response.Content.ReadAsByteArrayAsync();

                        if (data.Length < count &&
                            !(Length.HasValue &&
                              position + data.Length >= Length.Value))
                        {
                            throw new InvalidOperationException(
                                "HTTP server returned an incomplete byte range.");
                        }

                        return data;
                    }

                    if (response.StatusCode ==
                        HttpStatusCode.OK)
                    {
                        byte[] data =
                            await response.Content.ReadAsByteArrayAsync();

                        /*
                         * If Range was ignored, accepting a huge complete
                         * response would defeat streaming.
                         */
                        if (position != 0)
                        {
                            throw new InvalidOperationException(
                                "The media server ignored the HTTP Range request.");
                        }

                        if (data.Length > count)
                        {
                            throw new InvalidOperationException(
                                "The media server ignored the HTTP Range request " +
                                "and returned the entire media resource.");
                        }

                        Length = data.Length;

                        return data;
                    }

                    throw new HttpRequestException(
                        "HTTP media request failed: " +
                        (int)response.StatusCode +
                        " " +
                        response.ReasonPhrase);
                }
            }
        }

        private bool TryReadCache(
            long position,
            int count,
            out byte[] result)
        {
            lock (_cacheLock)
            {
                if (_cache == null)
                {
                    result = null;
                    return false;
                }

                long end = position + count;

                if (position < _cacheStart ||
                    end > _cacheEnd)
                {
                    result = null;
                    return false;
                }

                int offset =
                    checked((int)(
                        position -
                        _cacheStart));

                result = new byte[count];

                Buffer.BlockCopy(
                    _cache,
                    offset,
                    result,
                    0,
                    count);

                return true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(HttpRangeReader));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _httpClient.Dispose();

            lock (_cacheLock)
            {
                _cache = null;
            }
        }

        private sealed class EndOfStreamException
            : InvalidOperationException
        {
            public EndOfStreamException(
                string message)
                : base(message)
            {
            }
        }
    }
}