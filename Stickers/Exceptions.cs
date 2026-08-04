using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QStickerManager.Stickers
{
    public class StickerNotFoundException : Exception
    {
        public string Hash;
        public StickerNotFoundException(string hash)
            : base($"Sticker with hash '{hash}' not found.")
        {
            Hash = hash;
        }
    }
}
