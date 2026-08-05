using System;
using System.IO;
using Windows.Storage;

namespace QStickerManager.Settings
{
    public sealed class AppSettings
    {
        private const string BasePathSettingName = "BasePath";

        public string DefaultBasePath => Path.GetFullPath(
            Path.Combine(ApplicationData.Current.LocalFolder.Path, "QStickerManager"));

        public string BasePath
            => ApplicationData.Current.LocalSettings.Values[BasePathSettingName] as string
                ?? DefaultBasePath;

        public void SetBasePath(string basePath)
        {
            string normalizedPath = Path.GetFullPath(basePath);
            if (string.Equals(normalizedPath, DefaultBasePath, StringComparison.OrdinalIgnoreCase))
                ApplicationData.Current.LocalSettings.Values.Remove(BasePathSettingName);
            else
                ApplicationData.Current.LocalSettings.Values[BasePathSettingName] = normalizedPath;
        }
    }
}
