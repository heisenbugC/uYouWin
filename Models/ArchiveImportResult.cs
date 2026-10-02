using System.Collections.Generic;

namespace uYouWin.Models
{
    public class ArchiveImportResult
    {
        public List<Subscription> Subscriptions { get; set; }

        public List<Playlist> Playlists { get; set; }

        public List<HistoryEntry> History { get; set; }

        public List<SearchHistoryEntry> SearchHistory { get; set; }

        public ArchiveImportResult()
        {
            Subscriptions = new List<Subscription>();
            Playlists = new List<Playlist>();
            History = new List<HistoryEntry>();
            SearchHistory = new List<SearchHistoryEntry>();
        }
    }
}