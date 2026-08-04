using System;
using System.IO;
using ImageMagick;
using SkiaSharp;

namespace QStickerManager.Stickers
{
    public sealed class ImageProcessor
    {
        public ImageProcessor() { }

        // settings you can expand later
        public int JpegQuality { get; set; } = 90;
        public SKSamplingOptions ResizeSampling { get; set; } = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        public void Resize(string inputPath, string outputPath, uint width, uint height)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input image not found.", inputPath);

            using var image = new MagickImage(inputPath);
            image.Thumbnail(width, height);

            //using var input = File.OpenRead(inputPath);
            //using var codec = SKCodec.Create(input) ?? throw new IOException("Failed to decode image.");
            //using var original = SKBitmap.Decode(codec);
            //if (original is null)
            //    throw new IOException("Failed to decode image into bitmap.");

            //var info = new SKImageInfo(width, height, original.Info.ColorType, original.Info.AlphaType);
            //using var resized = original.Resize(info, ResizeSampling);
            //if (resized is null)
            //    throw new IOException("Failed to resize image.");

            //using var image = SKImage.FromBitmap(resized);
            //using var data = image.Encode(GetEncodedImageFormatFromPath(outputPath), JpegQuality);
            //using var outStream = File.Create(outputPath);
            //data.SaveTo(outStream);
        }

        public void CreateThumbnail(string inputPath, string outputPath, uint maxDimension)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input image not found.", inputPath);

            using var image = new MagickImage(inputPath);
            image.Thumbnail(maxDimension, maxDimension);
            image.Write(outputPath);

            //var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            //// Decode first frame for GIFs explicitly; SKBitmap.Decode(stream) will decode the first frame.
            //using var input = File.OpenRead(inputPath);
            //SKBitmap? original;
            //if (ext == ".gif")
            //{
            //    input.Position = 0;
            //    original = SKBitmap.Decode(input);
            //}
            //else
            //{
            //    using var codec = SKCodec.Create(input) ?? throw new IOException("Failed to decode image.");
            //    original = SKBitmap.Decode(codec);
            //}

            //if (original is null)
            //    throw new IOException("Failed to decode image into bitmap.");

            //int width = original.Width;
            //int height = original.Height;
            //float scale = Math.Min((float)maxDimension / width, (float)maxDimension / height);
            //if (scale >= 1f)
            //{
            //    using var image = SKImage.FromBitmap(original);
            //    using var data = image.Encode(GetEncodedImageFormatFromPath(outputPath), JpegQuality);
            //    using var outStream = File.Create(outputPath);
            //    data.SaveTo(outStream);
            //    return;
            //}

            //int newW = (int)Math.Round(width * scale);
            //int newH = (int)Math.Round(height * scale);

            //var info = new SKImageInfo(newW, newH, original.Info.ColorType, original.Info.AlphaType);
            //using var resized = original.Resize(info, ResizeSampling);
            //if (resized is null)
            //    throw new IOException("Failed to resize image.");

            //using var outImage = SKImage.FromBitmap(resized);
            //using var outData = outImage.Encode(GetEncodedImageFormatFromPath(outputPath), JpegQuality);
            //using var outStreamFinal = File.Create(outputPath);
            //outData.SaveTo(outStreamFinal);
        }

        private SKEncodedImageFormat GetEncodedImageFormatFromPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                ".bmp" => SKEncodedImageFormat.Bmp,
                ".gif" => SKEncodedImageFormat.Gif,
                _ => SKEncodedImageFormat.Jpeg,
            };
        }

        public void ConvertToGif(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input image not found.", inputPath);

            using var images = new MagickImageCollection(inputPath);
            images.Coalesce();
            images.Write(outputPath, MagickFormat.Gif);
        }
    }
}
