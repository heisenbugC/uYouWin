using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Archive
{
    internal interface ArchiveImporterInterface
    {
        Task<List<Subscription>> ImportSubscriptionsAsync(
            string path,
            CancellationToken cancellationToken);

        Task<List<HistoryEntry>> ImportHistoryAsync(
            string path,
            CancellationToken cancellationToken);

        Task<List<Playlist>> ImportPlaylistsAsync(
            string path,
            CancellationToken cancellationToken);

        Task<ArchiveImportResult> ImportAsync(
            string path,
            CancellationToken cancellationToken);
    }
}
