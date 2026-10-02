using System.Text.RegularExpressions;

namespace uYouWin.Services.Cache
{
    internal static class QueryNormalizer
    {
        private static readonly Regex Whitespace =
            new Regex(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            return Whitespace.Replace(query.Trim().ToLowerInvariant(), " ");
        }

        public static string CacheKey(string normalizedQuery, string pageToken)
        {
            return normalizedQuery + "|" + (pageToken ?? string.Empty);
        }
    }
}
