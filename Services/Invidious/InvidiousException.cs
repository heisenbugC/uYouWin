using System;

namespace uYouWin.Services.Invidious
{
    internal class InvidiousException : Exception
    {
        public int StatusCode { get; private set; }

        public string ResponseBody { get; private set; }

        public InvidiousException(
            string message,
            int statusCode,
            string responseBody = null) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
