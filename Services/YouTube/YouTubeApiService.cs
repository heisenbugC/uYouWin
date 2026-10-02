using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using uYouWin.Models;
using uYouWin.Services.Cache;

namespace uYouWin.Services.YouTube
{
    /// <summary>
    /// Resolves search results, videos, channels and playlists via the
    /// YouTube Data API v3 REST surface (or a compatible third-party
    /// implementation configured by the user in Settings).
    /// </summary>
    internal class YouTubeApiService : YouTubeApiServiceInterface
    {
        private static readonly Regex Iso8601DurationRegex =
            new Regex(
                @"^PT(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?(?:(?<seconds>\d+)S)?$",
                RegexOptions.Compiled);

        private readonly YouTubeApiClient _client;

        public YouTubeApiService(YouTubeApiClient client)
        {
            _client =
                client ??
                throw new ArgumentNullException(nameof(client));
        }

        public Task<List<Video>> SearchAsync(
            string query,
            CancellationToken cancellationToken)
        {
            return SearchVideosAsync(query, null, cancellationToken);
        }

        public async Task<VideoSearchPage> SearchPageAsync(
            string query,
            string pageToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException(
                    "Query cannot be empty.",
                    nameof(query));

            string normalized = QueryNormalizer.Normalize(query);
            string cacheKey = _client.CacheScope + "\n" + QueryNormalizer.CacheKey(normalized, pageToken);

            cancellationToken.ThrowIfCancellationRequested();
            VideoSearchPage cached = _client.CacheEnabled
                ? await Task.Run(() => SearchResultCache.TryGet(cacheKey), cancellationToken) : null;

            if (cached != null)
                return cached;

            string queryString =
                "part=snippet&type=video&maxResults=25&q=" +
                Uri.EscapeDataString(normalized);

            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                queryString +=
                    "&pageToken=" +
                    Uri.EscapeDataString(pageToken);
            }

            JToken result = await _client.GetAsync(
                "search",
                queryString,
                cancellationToken);

            var page = new VideoSearchPage
            {
                NextPageToken = (string)result["nextPageToken"],
                Videos = ParseSearchItems(result["items"])
            };

            if (page.Videos.Count > 0)
            {
                var videoIds = new List<string>();

                foreach (Video video in page.Videos)
                    videoIds.Add(video.Id);

                await EnrichWithContentDetailsAsync(
                    page.Videos,
                    videoIds,
                    cancellationToken);
            }

            if (_client.CacheEnabled)
                await Task.Run(() => SearchResultCache.Save(cacheKey, page), cancellationToken);

            return page;
        }

        private async Task<List<Video>> SearchVideosAsync(
            string query,
            string pageToken,
            CancellationToken cancellationToken)
        {
            VideoSearchPage page = await SearchPageAsync(
                query,
                pageToken,
                cancellationToken);

            return page.Videos;
        }

        private static List<Video> ParseSearchItems(JToken items)
        {
            var videos = new List<Video>();

            if (items == null)
                return videos;

            foreach (JToken item in items)
            {
                JToken id = item["id"];
                string videoId = id is JObject ? (string)id["videoId"] : (string)id;

                if (string.IsNullOrEmpty(videoId))
                    continue;

                videos.Add(ParseVideoFromSnippet(videoId, item["snippet"]));
            }

            return videos;
        }

        public async Task<Video> GetVideoAsync(
            string videoId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                throw new ArgumentException(
                    "Video ID cannot be empty.",
                    nameof(videoId));

            JToken result = await _client.GetAsync(
                "videos",
                "part=snippet,contentDetails&id=" +
                Uri.EscapeDataString(videoId),
                cancellationToken);

            JToken item = result["items"]?.FirstOrDefault();

            if (item == null)
                return null;

            Video video =
                ParseVideoFromSnippet(
                    videoId,
                    item["snippet"]);

            string isoDuration =
                (string)item["contentDetails"]?["duration"];

            video.Duration =
                ParseIso8601Duration(isoDuration);
            VisitedUrlCache.Save(video);
            return video;
        }

        public async Task<Channel> GetChannelAsync(
            string channelId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                throw new ArgumentException(
                    "Channel ID cannot be empty.",
                    nameof(channelId));

            JToken result = await _client.GetAsync(
                "channels",
                "part=snippet,statistics,contentDetails&id=" +
                Uri.EscapeDataString(channelId),
                cancellationToken);

            JToken item = result["items"]?.FirstOrDefault();

            if (item == null)
                return null;

            JToken snippet = item["snippet"];

            var channel = new Channel
            {
                ChannelId = channelId,
                Name = (string)snippet?["title"],
                Description = (string)snippet?["description"],
                ThumbnailUrl = GetBestThumbnailUrl(snippet?["thumbnails"]),
                WebUrl = "https://www.youtube.com/channel/" + channelId,
                PublishedAt = (DateTime?)snippet?["publishedAt"],
                Country = (string)snippet?["country"],
                CustomUrl = (string)snippet?["customUrl"],
                SubscriberCount = (long?)item["statistics"]?["subscriberCount"],
                VideoCount = (long?)item["statistics"]?["videoCount"],
                ViewCount = (long?)item["statistics"]?["viewCount"],
                HiddenSubscriberCount = (bool?)item["statistics"]?["hiddenSubscriberCount"] ?? false,
                UploadsPlaylistId = (string)item["contentDetails"]?["relatedPlaylists"]?["uploads"],
                MetadataFetchedAtUtc = DateTime.UtcNow
            };
            VisitedUrlCache.SaveChannel(channel);
            return channel;
        }

        public async Task<List<Video>> GetChannelVideosAsync(
            string channelId,
            CancellationToken cancellationToken)
        {
            return (await GetChannelVideosPageAsync(channelId, null, cancellationToken)).Videos;
        }

        public async Task<VideoSearchPage> GetChannelVideosPageAsync(
            string channelId, string pageToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                throw new ArgumentException(
                    "Channel ID cannot be empty.",
                    nameof(channelId));

            Channel channel = await GetChannelAsync(channelId, cancellationToken);
            if (string.IsNullOrWhiteSpace(channel?.UploadsPlaylistId))
                return new VideoSearchPage();
            JToken result = await _client.GetAsync("playlistItems",
                "part=snippet&maxResults=25&playlistId=" + Uri.EscapeDataString(channel.UploadsPlaylistId) +
                (string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken)), cancellationToken);
            var videos = new List<Video>();
            foreach (JToken item in result["items"] ?? new JArray())
            {
                JToken snippet = item["snippet"];
                string id = (string)snippet?["resourceId"]?["videoId"];
                if (string.IsNullOrWhiteSpace(id)) continue;
                Video video = ParseVideoFromSnippet(id, snippet);
                video.ChannelId = (string)snippet?["videoOwnerChannelId"] ?? channelId;
                video.ChannelTitle = (string)snippet?["videoOwnerChannelTitle"] ?? channel.Name;
                videos.Add(video);
            }

            if (videos.Count > 0)
            {
                var ids = new List<string>();

                foreach (Video video in videos)
                    ids.Add(video.Id);

                await EnrichWithContentDetailsAsync(
                    videos,
                    ids,
                    cancellationToken);
            }

            return new VideoSearchPage { Videos = videos, NextPageToken = (string)result["nextPageToken"] };
        }

        public async Task<List<Video>> GetRelatedVideosAsync(
            Video video,
            CancellationToken cancellationToken)
        {
            if (video == null)
                throw new ArgumentNullException(nameof(video));

            string query = string.IsNullOrWhiteSpace(video.Title)
                ? video.ChannelTitle
                : video.Title;

            if (string.IsNullOrWhiteSpace(query))
                return new List<Video>();

            VideoSearchPage page = await SearchPageAsync(
                query,
                null,
                cancellationToken);

            var related = new List<Video>();

            foreach (Video item in page.Videos)
            {
                if (item == null)
                    continue;

                if (string.Equals(item.Id, video.Id, StringComparison.Ordinal))
                    continue;

                related.Add(item);
            }

            return related;
        }

        public async Task<List<VideoComment>> GetCommentsAsync(
            string videoId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                throw new ArgumentException(
                    "Video ID cannot be empty.",
                    nameof(videoId));

            JToken result = await _client.GetAsync(
                "commentThreads",
                "part=snippet&textFormat=plainText&maxResults=20&videoId=" +
                Uri.EscapeDataString(videoId),
                cancellationToken);

            var comments = new List<VideoComment>();
            JToken items = result["items"];

            if (items == null)
                return comments;

            foreach (JToken item in items)
            {
                JToken snippet =
                    item["snippet"]?["topLevelComment"]?["snippet"];

                if (snippet == null)
                    continue;

                DateTime published = default(DateTime);

                if (snippet["publishedAt"] != null)
                    published = (DateTime)snippet["publishedAt"];

                comments.Add(new VideoComment
                {
                    Author = (string)snippet["authorDisplayName"],
                    Text = (string)snippet["textDisplay"],
                    ThumbnailUrl = (string)snippet["authorProfileImageUrl"],
                    PublishedText = published == default(DateTime)
                        ? string.Empty
                        : published.ToShortDateString()
                });
            }

            return comments;
        }

        public async Task<List<Video>> GetPlaylistVideosAsync(string playlistId, CancellationToken cancellationToken)
        {
            var videos = new List<Video>();
            string next = null;
            do
            {
                JToken result = await _client.GetAsync("playlistItems",
                    "part=snippet&maxResults=50&playlistId=" + Uri.EscapeDataString(playlistId) +
                    (next == null ? "" : "&pageToken=" + Uri.EscapeDataString(next)), cancellationToken);
                foreach (JToken item in result["items"] ?? new JArray())
                {
                    JToken snippet = item["snippet"];
                    string id = (string)snippet?["resourceId"]?["videoId"];
                    if (!string.IsNullOrEmpty(id))
                        videos.Add(ParseVideoFromSnippet(id, snippet));
                }
                next = (string)result["nextPageToken"];
            } while (!string.IsNullOrEmpty(next));
            return videos;
        }

        public async Task<List<Video>> GetPopularVideosAsync(string regionCode, CancellationToken cancellationToken)
        {
            return (await GetPopularVideosPageAsync(regionCode, null, cancellationToken)).Videos;
        }

        public async Task<VideoSearchPage> GetPopularVideosPageAsync(string regionCode, string pageToken, CancellationToken cancellationToken)
        {
            JToken result = await _client.GetAsync("videos",
                "part=snippet,contentDetails&chart=mostPopular&maxResults=25" +
                (string.IsNullOrWhiteSpace(regionCode) ? "" : "&regionCode=" + Uri.EscapeDataString(regionCode)) +
                (string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken)),
                cancellationToken);
            var videos = new List<Video>();
            foreach (JToken item in result["items"] ?? new JArray())
            {
                Video video = ParseVideoFromSnippet((string)item["id"], item["snippet"]);
                video.Duration = ParseIso8601Duration((string)item["contentDetails"]?["duration"]);
                videos.Add(video);
            }
            return new VideoSearchPage { Videos = videos, NextPageToken = (string)result["nextPageToken"] };
        }

        private async Task EnrichWithContentDetailsAsync(
            List<Video> videos,
            List<string> videoIds,
            CancellationToken cancellationToken)
        {
            JToken detailsResult = await _client.GetAsync(
                "videos",
                "part=contentDetails&id=" +
                Uri.EscapeDataString(string.Join(",", videoIds)),
                cancellationToken);

            JToken detailItems = detailsResult["items"];

            if (detailItems == null)
                return;

            var durationById =
                new Dictionary<string, double>();

            foreach (JToken detailItem in detailItems)
            {
                string id = (string)detailItem["id"];

                if (string.IsNullOrEmpty(id))
                    continue;

                durationById[id] =
                    ParseIso8601Duration(
                        (string)detailItem["contentDetails"]?["duration"]);
            }

            foreach (Video video in videos)
            {
                double duration;

                if (durationById.TryGetValue(video.Id, out duration))
                    video.Duration = duration;
            }
        }

        private static Video ParseVideoFromSnippet(
            string videoId,
            JToken snippet)
        {
            return new Video
            {
                Id = videoId,
                Title = (string)snippet?["title"],
                Description = (string)snippet?["description"],
                ChannelId = (string)snippet?["channelId"],
                ChannelTitle = (string)snippet?["channelTitle"],
                ThumbnailUrl = GetBestThumbnailUrl(snippet?["thumbnails"]),
                PublishedAt =
                    snippet?["publishedAt"] != null
                        ? (DateTime)snippet["publishedAt"]
                        : default(DateTime),
                WebUrl = "https://www.youtube.com/watch?v=" + videoId,
                YtUrl = "https://www.youtube.com/watch?v=" + videoId
            };
        }

        private static string GetBestThumbnailUrl(JToken thumbnails)
        {
            if (thumbnails == null)
                return null;

            foreach (string quality in
                     new[] { "high", "medium", "default" })
            {
                string url =
                    (string)thumbnails[quality]?["url"];

                if (!string.IsNullOrEmpty(url))
                    return url;
            }

            return null;
        }

        private static double ParseIso8601Duration(string isoDuration)
        {
            if (string.IsNullOrWhiteSpace(isoDuration))
                return 0d;

            Match match =
                Iso8601DurationRegex.Match(isoDuration);

            if (!match.Success)
                return 0d;

            double hours =
                match.Groups["hours"].Success
                    ? double.Parse(match.Groups["hours"].Value)
                    : 0d;

            double minutes =
                match.Groups["minutes"].Success
                    ? double.Parse(match.Groups["minutes"].Value)
                    : 0d;

            double seconds =
                match.Groups["seconds"].Success
                    ? double.Parse(match.Groups["seconds"].Value)
                    : 0d;

            return (hours * 3600) + (minutes * 60) + seconds;
        }
    }
}
