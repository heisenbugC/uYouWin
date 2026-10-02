using System;

namespace uYouWin.Services.YouTube
{
    /// <summary>
    /// Thrown when no YouTube API key has been configured in Settings.
    /// Callers should catch this specifically to show a friendly
    /// "please configure an API key" message instead of a generic error.
    /// </summary>
    internal class YouTubeApiKeyMissingException : Exception
    {
        public YouTubeApiKeyMissingException()
            : base(
                "No YouTube API key has been configured. " +
                "Please add one on the Settings page.")
        {
        }
    }

    /// <summary>
    /// Thrown when the configured YouTube Data API v3 (or compatible
    /// third-party) endpoint returns an error response.
    /// </summary>
    internal class YouTubeApiException : Exception
    {
        public int StatusCode { get; private set; }

        public string ResponseBody { get; private set; }

        public YouTubeApiException(
            string message,
            int statusCode,
            string responseBody = null) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
