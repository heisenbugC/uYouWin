using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.YouTube
{
    internal interface YouTubeApiServiceInterface
    {
        Task<List<Video>> SearchAsync(
            string query,
            CancellationToken cancellationToken);

        Task<VideoSearchPage> SearchPageAsync(
            string query,
            string pageToken,
            CancellationToken cancellationToken);

        Task<Video> GetVideoAsync(
            string videoId,
            CancellationToken cancellationToken);

        Task<Channel> GetChannelAsync(
            string channelId,
            CancellationToken cancellationToken);

        Task<List<Video>> GetChannelVideosAsync(
            string channelId,
            CancellationToken cancellationToken);

        Task<VideoSearchPage> GetChannelVideosPageAsync(
            string channelId,
            string pageToken,
            CancellationToken cancellationToken);

        Task<List<Video>> GetRelatedVideosAsync(
            Video video,
            CancellationToken cancellationToken);

        Task<List<VideoComment>> GetCommentsAsync(
            string videoId,
            CancellationToken cancellationToken);

        Task<List<Video>> GetPlaylistVideosAsync(
            string playlistId,
            CancellationToken cancellationToken);

        Task<List<Video>> GetPopularVideosAsync(
            string regionCode,
            CancellationToken cancellationToken);

        Task<VideoSearchPage> GetPopularVideosPageAsync(
            string regionCode,
            string pageToken,
            CancellationToken cancellationToken);
    }
}
