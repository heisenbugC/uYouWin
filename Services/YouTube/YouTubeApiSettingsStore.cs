using System;
using System.IO;
using Newtonsoft.Json;
using uYouWin.Models;
using uYouWin.Services.Storage;

namespace uYouWin.Services.YouTube
{
    /// <summary>
    /// Loads and persists <see cref="YouTubeApiSettings"/> to a small JSON
    /// file under the application's Data folder (see
    /// <see cref="DataPathProvider"/>).
    ///
    /// This intentionally avoids storing the API key in App.config /
    /// Settings.settings so that the key is not accidentally bundled with
    /// the application or checked into source control.
    /// </summary>
    internal static class YouTubeApiSettingsStore
    {
        private static string SettingsFilePath
        {
            get
            {
                return DataPathProvider.GetFilePath(
                    "youtube-api-settings.json");
            }
        }

        public static YouTubeApiSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return new YouTubeApiSettings();

                string json =
                    File.ReadAllText(SettingsFilePath);

                YouTubeApiSettings settings =
                    JsonConvert.DeserializeObject<YouTubeApiSettings>(json);

                return settings ?? new YouTubeApiSettings();
            }
            catch
            {
                /*
                 * A corrupted or unreadable settings file should not
                 * prevent the application from starting. Fall back to
                 * defaults (no API key configured).
                 */
                return new YouTubeApiSettings();
            }
        }

        public static void Save(YouTubeApiSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string filePath = SettingsFilePath;

            string directory =
                Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) &&
                !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json =
                JsonConvert.SerializeObject(
                    settings,
                    Formatting.Indented);

            File.WriteAllText(filePath, json);
        }
    }
}
