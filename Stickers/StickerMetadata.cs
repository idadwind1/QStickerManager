using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QStickerManager.Stickers
{
    public sealed class StickerMetadata
    {
        [JsonPropertyName("keywords")]
        public Dictionary<string, int> Keywords { get; set; } = [];

        [JsonPropertyName("stickers")]
        public List<Sticker> Stickers { get; set; } = [];
    }
}
