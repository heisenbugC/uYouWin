using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using uYouWin.Models;

namespace uYouWin.Services.Invidious
{
    internal class InvidiousService
    {
        private readonly InvidiousClient _client;

        public InvidiousService(InvidiousClient client)
        {
            _client = client;
        }

        public async Task<Video> GetVideoAsync(
            string videoId, 
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                throw new ArgumentException(
                    "Video ID cannot be null or empty.", 
                    nameof(videoId));

            JToken result = await _client.GetAsync(
                $"videos/{Uri.EscapeDataString(videoId)}",
                cancellationToken);

            return ParseVideo(result);
        }

        public async Task<List<Video>> SearchAsync(
            string query, 
            CancellationToken cancellationToken)
        {
            JToken result = await _client.GetAsync(
                $"search?q={Uri.EscapeDataString(query)}",
                cancellationToken);

            var videos = new List<Video>();

            foreach (JToken item in result)
            {
                string type = (string)item["type"];

                if (type != "video")
                    continue;

                videos.Add(ParseVideo(item));
            }

            return videos;
        }

        private Video ParseVideo(JToken item)
        {
            return new Video
            {
                Id = (string)item["videoId"],
                Title = (string)item["title"],
                Description = (string)item["description"],
                ChannelId = (string)item["authorId"],
                ChannelTitle = (string)item["author"],
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds((long)item["published"]).DateTime,
                Duration = item["lengthSeconds"] == null
                    ? 0d
                    : Convert.ToDouble(item["lengthSeconds"]),
                ThumbnailUrl = (string)item["videoThumbnails"]?[0]?["url"],
                WebUrl = $"https://www.youtube.com/watch?v={(string)item["videoId"]}"
            };
        }

        private string GetThumbnailUrl(JToken thumbnails)
        {
            if (thumbnails == null)
            {
                return null;
            }

            foreach (JToken thumbnail in thumbnails)
            {
                if ((string)thumbnail["quality"] == "high")
                {
                    return (string)thumbnail["url"];
                }
            }

            return (string)thumbnails[0]?["url"];
        }

        public Task<Channel> GetChannelAsync(string channelId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Video>> GetChannelVideosAsync(string channelId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Video>> GetPlaylistVideosAsync(string playlistId)
        {
            throw new NotImplementedException();
        }
    }
}
