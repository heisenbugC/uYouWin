using System;
using System.IO;

namespace uYouWin.Services.Storage
{
    /// <summary>
    /// Keeps user data separate from disposable cache and installed binaries.
    /// </summary>
    internal static class DataPathProvider
    {
        private static string _dataDirectory;

        public static string DataDirectory
        {
            get
            {
                if (_dataDirectory == null)
                    _dataDirectory = ResolveDataDirectory();

                return _dataDirectory;
            }
        }

        public static string GetFilePath(string fileName)
        {
            return Path.Combine(DataDirectory, fileName);
        }

        public static string CacheDirectory => Path.Combine(
            Path.GetDirectoryName(DataDirectory), "Cache");

        private static string ResolveDataDirectory()
        {
            string directory = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "uYouWin", "Data");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
