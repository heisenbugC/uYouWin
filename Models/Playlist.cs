using System.Collections.Generic;

namespace uYouWin.Models
{
    public class Playlist
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ThumbnailUrl { get; set; }

        public List<string> VideoIds { get; set; }

        public List<Video> Videos { get; set; }

        public Playlist()
        {
            VideoIds = new List<string>();
            Videos = new List<Video>();
        }
    }
}