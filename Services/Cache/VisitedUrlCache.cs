using System;
using uYouWin.Models;

namespace uYouWin.Services.Cache
{
    /// <summary>
    /// Stores metadata for YouTube watch URLs the user has opened.
    /// Stream URLs are not cached because they expire.
    /// </summary>
    internal static class VisitedUrlCache
    {
        public static void Save(Video video)
        {
            if (video == null || string.IsNullOrWhiteSpace(video.Id))
                return;
            DiskCache.Save("visited-video", video.Id.Trim(), video);
        }

        public static Channel TryGetChannel(string channelId)
        {
            return DiskCache.TryGet<Channel>("visited-channel", channelId);
        }

        public static void SaveChannel(Channel channel)
        {
            if (channel == null || string.IsNullOrWhiteSpace(channel.ChannelId)) return;
            DiskCache.Save("visited-channel", channel.ChannelId, channel);
        }

        public static Video TryGet(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId))
                return null;

            return DiskCache.TryGet<Video>("visited-video", VideoIdFromUrl(urlOrId) ?? urlOrId.Trim());
        }

        public static string NormalizeUrl(Video video)
        {
            if (!string.IsNullOrWhiteSpace(video.YtUrl))
                return NormalizeKey(video.YtUrl);

            if (!string.IsNullOrWhiteSpace(video.WebUrl))
                return NormalizeKey(video.WebUrl);

            return "https://www.youtube.com/watch?v=" + video.Id.Trim();
        }

        public static string VideoIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;
            string value = url.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z0-9_-]{11}$"))
                return value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
                return null;
            string host = uri.Host.ToLowerInvariant();
            string id = null;
            if (host == "youtu.be")
                id = uri.AbsolutePath.Trim('/');
            else if (host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal))
            {
                foreach (string part in uri.Query.TrimStart('?').Split('&'))
                    if (part.StartsWith("v=", StringComparison.Ordinal))
                        id = Uri.UnescapeDataString(part.Substring(2));
                string[] segments = uri.AbsolutePath.Trim('/').Split('/');
                if (segments.Length == 2 && (segments[0] == "shorts" || segments[0] == "embed" || segments[0] == "live"))
                    id = segments[1];
            }
            return id != null && System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9_-]{11}$") ? id : null;
        }

        private static string NormalizeKey(string url)
        {
            string id = VideoIdFromUrl(url);
            return id == null ? url.Trim() : "https://www.youtube.com/watch?v=" + id;
        }

    }
}
