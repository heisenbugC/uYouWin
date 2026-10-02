using System;

namespace uYouWin.Models
{
    public class HistoryEntry
    {
        public string VideoId { get; set; }

        public string Title { get; set; }

        public string ChannelId { get; set; }

        public string ChannelTitle { get; set; }

        public string ThumbnailUrl { get; set; }

        public string WebUrl { get; set; }

        public DateTime WatchedAt { get; set; }

        public double Position { get; set; }
    }
}