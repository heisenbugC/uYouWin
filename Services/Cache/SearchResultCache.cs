using uYouWin.Models;

namespace uYouWin.Services.Cache
{
    /// <summary>
    /// Persists search pages independently on disk. Keys ignore
    /// capitalization and repeated whitespace.
    /// </summary>
    internal static class SearchResultCache
    {
        public static VideoSearchPage TryGet(string cacheKey)
        {
            return DiskCache.TryGet<VideoSearchPage>("search", cacheKey);
        }

        public static void Save(string cacheKey, VideoSearchPage page)
        {
            DiskCache.Save("search", cacheKey, page);
        }
    }
}
