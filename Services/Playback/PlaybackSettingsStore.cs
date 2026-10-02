using System;
using System.IO;
using Newtonsoft.Json;
using uYouWin.Models;
using uYouWin.Services.Storage;

namespace uYouWin.Services.Playback
{
    /// <summary>
    /// Loads and persists <see cref="PlaybackSettings"/> to the Data folder.
    /// </summary>
    internal static class PlaybackSettingsStore
    {
        private static string FilePath
        {
            get
            {
                return DataPathProvider.GetFilePath(
                    "playback-settings.json");
            }
        }

        public static PlaybackSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new PlaybackSettings();

                string json = File.ReadAllText(FilePath);

                PlaybackSettings settings =
                    JsonConvert.DeserializeObject<PlaybackSettings>(json);

                return settings ?? new PlaybackSettings();
            }
            catch
            {
                return new PlaybackSettings();
            }
        }

        public static void Save(PlaybackSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string filePath = FilePath;

            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) &&
                !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(
                settings,
                Formatting.Indented);

            File.WriteAllText(filePath, json);
        }
    }
}
