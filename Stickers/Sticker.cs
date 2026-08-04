using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QStickerManager.Stickers
{
    public record class Sticker
    {
        public Sticker(string path, string description, IEnumerable<string> keywords, string hash, string thumbnailPath, int index)
        {
            Path = path;
            Description = description;
            Keywords = [.. keywords];
            Hash = hash;
            ThumbnailPath = thumbnailPath;
            Index = index;
            //Order = order;
        }

        public Sticker(Sticker sticker)
        {
            Path = sticker.Path;
            Description = sticker.Description;
            Keywords = [.. sticker.Keywords];
            Hash = sticker.Hash;
            ThumbnailPath = sticker.ThumbnailPath;
            Index = sticker.Index;
        }

        public Sticker()
        {
            Path = string.Empty;
            Description = string.Empty;
            Keywords = [];
            Hash = string.Empty;
            ThumbnailPath = string.Empty;
            Index = 0;
        }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }

        //[JsonPropertyName("order")]
        //public int Order { get; set; }

        [JsonPropertyName("thumbnail_path")]
        public string ThumbnailPath { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
}
