using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace QStickerManager.Stickers
{
    public sealed class StickerArchiveService
    {
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

        private sealed class StickerArchiveEntry
        {
            public required string Path { get; init; }
            public required string Description { get; init; }
            public required IReadOnlyList<string> Keywords { get; init; }
        }

        public List<StickerImportSource> ReadArchive(string zipPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry? metaEntry = archive.Entries.FirstOrDefault(
                entry => string.Equals(entry.FullName, "meta.json", StringComparison.OrdinalIgnoreCase));

            return metaEntry is null
                ? ReadArchiveRootFiles(archive)
                : ReadArchiveMetaEntries(archive, metaEntry);
        }

        public void WriteArchive(string zipPath, IReadOnlyList<Sticker> stickers)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (Sticker sticker in stickers)
            {
                archive.CreateEntryFromFile(
                    sticker.Path,
                    Path.GetFileName(sticker.Path),
                    CompressionLevel.Optimal);
            }

            ZipArchiveEntry metaEntry = archive.CreateEntry("meta.json", CompressionLevel.Optimal);
            using Stream stream = metaEntry.Open();
            using StreamWriter writer = new(stream);
            writer.Write(JsonSerializer.Serialize(
                stickers.Select(sticker => new StickerArchiveEntry
                {
                    Path = Path.GetFileName(sticker.Path),
                    Description = sticker.Description,
                    Keywords = sticker.Keywords
                }).ToList(),
                jsonSerializerOptions));
        }

        private static List<StickerImportSource> ReadArchiveMetaEntries(ZipArchive archive, ZipArchiveEntry metaEntry)
        {
            using Stream stream = metaEntry.Open();
            using StreamReader reader = new(stream);
            string json = reader.ReadToEnd();
            List<StickerArchiveEntry>? stickers = JsonSerializer.Deserialize<List<StickerArchiveEntry>>(json);
            if (stickers is null)
                return [];

            List<StickerImportSource> candidates = [];
            foreach (StickerArchiveEntry sticker in stickers)
            {
                string normalizedPath = sticker.Path.Replace('\\', '/');
                ZipArchiveEntry? stickerEntry = archive.GetEntry(normalizedPath);
                if (stickerEntry is null || !StickerFileTypes.IsImageFile(stickerEntry.Name))
                    continue;

                candidates.Add(new StickerImportSource(
                    ExtractZipEntryToTempFile(stickerEntry),
                    sticker.Description,
                    sticker.Keywords,
                    true));
            }

            return candidates;
        }

        private static List<StickerImportSource> ReadArchiveRootFiles(ZipArchive archive)
        {
            return archive.Entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name))
                .Where(entry => !entry.FullName.Contains('/')
                    && !entry.FullName.Contains('\\')
                    && StickerFileTypes.IsImageFile(entry.Name))
                .Select(entry => new StickerImportSource(
                    ExtractZipEntryToTempFile(entry),
                    string.Empty,
                    [],
                    true))
                .ToList();
        }

        private static string ExtractZipEntryToTempFile(ZipArchiveEntry entry)
        {
            string extension = Path.GetExtension(entry.Name);
            string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
            entry.ExtractToFile(tempPath, true);
            return tempPath;
        }
    }

    public sealed class StickerImportSource(string path, string description, IReadOnlyList<string> keywords, bool deleteAfterImport = false)
    {
        public string Path { get; } = path;
        public string Description { get; } = description;
        public IReadOnlyList<string> Keywords { get; } = keywords;
        public bool DeleteAfterImport { get; } = deleteAfterImport;

        public void Cleanup()
        {
            if (!DeleteAfterImport || !File.Exists(Path))
                return;

            File.Delete(Path);
        }
    }
}
