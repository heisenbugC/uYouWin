using System.Collections.Generic;

namespace uYouWin.Models
{
    internal class Playlist
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ThumbnailUrl { get; set; }

        public List<string> VideoIds { get; set; }

        public Playlist()
        {
            VideoIds = new List<string>();
        }
    }
}