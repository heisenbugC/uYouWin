using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Invidious
{
    internal interface InvidiousServiceInterface
    {
        Task<Video> GetVideoAsync(
            string videoId,
            CancellationToken cancellationToken);

        Task<Channel> GetChannelAsync(
            string channelId,
            CancellationToken cancellationToken);

        Task<List<Video>> SearchAsync(
            string query,
            CancellationToken cancellationToken);

        Task<List<Video>> GetChannelVideoAsync(
            string channelId,
            CancellationToken cancellationToken);

        Task<List<Video>> GetPlaylistVideoAsync(
            string playlistId,
            CancellationToken cancellationToken);
    }
}
