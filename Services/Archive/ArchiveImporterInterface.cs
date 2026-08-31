using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Archive
{
    internal interface ArchiveImporterInterface
    {
        Task<List<Subscription>> ImportSubscriptionsAsync(string filePath);

        Task<List<HistoryEntry>> ImportHistoryAsync(string filePath);

        Task<List<Playlist>> ImportPlaylistsAsync(string filePath);

        Task<ArchiveImportResult> ImportAsync(string path);
    }
}
