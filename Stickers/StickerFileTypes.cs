using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QStickerManager.Stickers
{
    public static class StickerFileTypes
    {
        public static readonly string[] ImageExtensions = [
            ".bmp", ".dib", ".gif", ".heic", ".heif",
            ".ico", ".jfif", ".jpeg", ".jpg", ".jpe",
            ".png", ".tif", ".tiff", ".wdp", ".webp", ".avif"
        ];

        public const string ZipExtension = ".zip";

        public static bool IsImageFile(string path)
            => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        public static bool IsZipFile(string path)
            => string.Equals(Path.GetExtension(path), ZipExtension, StringComparison.OrdinalIgnoreCase);

        public static bool IsSupportedImportFile(string path)
            => IsImageFile(path) || IsZipFile(path);
    }
}
