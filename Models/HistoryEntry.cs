using System;

namespace uYouWin.Models
{
    internal class HistoryEntry
    {
        public string VideoId { get; set; }

        public string Title { get; set; }

        public DateTime WatchedAt { get; set; }

        public double Position { get; set; }
    }
}