using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;

namespace QStickerManager.Localization
{
    internal static class Localizer
    {
        private static readonly ResourceLoader ResourceLoader = new();

        public static string Get(string key) => ResourceLoader.GetString(key);

        public static string Format(string key, params object[] arguments)
            => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

        public static string FormatCount(int count, string singularKey, string pluralKey)
            => Format(count == 1 ? singularKey : pluralKey, count);
    }
}
