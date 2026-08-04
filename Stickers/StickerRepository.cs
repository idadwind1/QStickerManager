using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace QStickerManager.Stickers
{
    public class StickerRepository
    {
        private readonly Dictionary<string, Sticker> _stickers;
        private readonly string BasePath;
        private readonly string MetaFilePath;
        private readonly string StickerDirectoryPath;
        private const string StickersDirectoryName = "stickers";
        private const string MetaFileName = "meta.json";

        public StickerRepository(string basePath)
        {
            _stickers = [];
            BasePath = basePath;
            MetaFilePath = Path.Combine(BasePath, MetaFileName);
            StickerDirectoryPath = Path.Combine(BasePath, StickersDirectoryName);
            Stickers = [];
            imageProcessor = new ImageProcessor();

            // this also creates basePath
            Directory.CreateDirectory(StickerDirectoryPath);
        }

        public ObservableCollection<Sticker> Stickers;

        /// <summary>
        /// Load sticker list from meta.json
        /// </summary>
        public void LoadMetaFile()
        {
            if (!File.Exists(MetaFilePath))
            {
                File.Create(MetaFilePath);
                return;
            }

            string json = File.ReadAllText(MetaFilePath);
            try
            {
                List<Sticker>? stickers = JsonSerializer.Deserialize<List<Sticker>>(json);

                if (stickers is null) return;

                foreach (var sticker in stickers)
                    AddSticker(sticker);
            }
            catch (JsonException) { }
        }

        private readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        /// <summary>
        /// Write sticker list from memory to meta.json
        /// </summary>
        public void UpdateMetaFile()
        {
            string json = JsonSerializer.Serialize(Stickers.Reverse(), JsonSerializerOptions);

            File.WriteAllText(MetaFilePath, json);
        }

        private readonly ImageProcessor imageProcessor;

        /// <summary>
        /// add sticker to repository
        /// </summary>
        /// <param name="originalPath"></param>
        /// <param name="description"></param>
        /// <param name="keywords"></param>
        /// <param name="hash"></param>
        /// <returns>whether the sticker is not repeated and added successfully</returns>
        public bool Import(string originalPath, string description, IEnumerable<string> keywords, out string hash)
        {
            return TryPrepareImport(originalPath, description, keywords, out hash, out Sticker? sticker)
                && AddImportedSticker(sticker);
        }

        public bool TryPrepareImport(string originalPath, string description, IEnumerable<string> keywords, out string hash, out Sticker? sticker)
        {
            hash = GetFileHash(originalPath);
            sticker = null;

            if (_stickers.ContainsKey(hash))
                return false;

            string thumbnailPath = Path.Combine(StickerDirectoryPath, hash + ".thumbnail.png");
            string newPath = Path.Combine(StickerDirectoryPath, hash + Path.GetExtension(originalPath));

            if (File.Exists(thumbnailPath))
                File.Delete(thumbnailPath);
            if (File.Exists(newPath))
                File.Delete(newPath);

            imageProcessor.CreateThumbnail(originalPath, thumbnailPath, 256);
            imageProcessor.ConvertToGif(originalPath, newPath);

            sticker = new Sticker
            {
                Path = newPath,
                Description = description,
                Keywords = [.. keywords],
                Hash = hash,
                ThumbnailPath = thumbnailPath
            };

            return true;
        }

        public bool AddImportedSticker(Sticker? sticker)
        {
            if (sticker is null || _stickers.ContainsKey(sticker.Hash))
                return false;

            AddSticker(sticker);
            return true;
        }

        public string EnsureGif(Sticker sticker)
        {
            if (string.Equals(Path.GetExtension(sticker.Path), ".gif", StringComparison.OrdinalIgnoreCase))
                return sticker.Path;

            string gifPath = Path.Combine(StickerDirectoryPath, sticker.Hash + ".gif");
            if (!File.Exists(gifPath))
                imageProcessor.ConvertToGif(sticker.Path, gifPath);

            return gifPath;
        }

        /// <summary>
        /// Remove sticker from repository
        /// </summary>
        /// <param name="sticker">hash of sticker</param>
        /// <param name="removeFile">whether to remove file</param>
        public void Remove(Sticker sticker, bool removeFile)
        {
            //if (!_stickers.Remove(hash, out Sticker? sticker))
            //    throw new StickerNotFoundException(hash);
            _stickers.Remove(sticker.Hash);
            Stickers.Remove(sticker);
            if (removeFile && sticker is not null)
            {
                if (File.Exists(sticker.Path))
                    File.Delete(sticker.Path);
                if (File.Exists(sticker.ThumbnailPath))
                    File.Delete(sticker.ThumbnailPath);
            }
        }

        private void AddSticker(Sticker sticker)
        {
            Stickers.Insert(0, sticker);
            _stickers.Add(sticker.Hash, sticker);
        }

        //public bool TryGet(string hash, out Sticker? sticker)
        //    => _stickers.TryGetValue(hash, out sticker);

        /// <summary>
        /// Get sticker by hash
        /// </summary>
        /// <param name="hash">hash of sticker</param>
        /// <returns>sticker</returns>
        /// <exception cref="StickerNotFoundException"></exception>
        public Sticker Get(string hash)
             => (_stickers.TryGetValue(hash, out Sticker? sticker) && sticker is not null)
            ? sticker
            : throw new StickerNotFoundException(hash);

        public List<Sticker> GetAll()
            => [.. _stickers.Values];

        public static string GetFileHash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);

            return Convert.ToHexString(md5.ComputeHash(stream));
        }

        public static void CopyStickerFile(Sticker sticker, string destinationPath)
            => File.Copy(sticker.Path, Path.Combine(destinationPath, Path.GetFileName(sticker.Path)), true);

        public void MoveToFront(Sticker sticker)
        {
            Stickers.Remove(sticker);
            Stickers.Insert(0, sticker);
        }
    }
}
