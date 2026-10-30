using System;
using System.IO;
using ImageMagick;

namespace GARbro.Imaging
{
    /// <summary>Eager single-frame decoding for historical providers using embedded raster streams.</summary>
    public sealed class RasterDecoder
    {
        public System.Collections.Generic.IReadOnlyList<PixelBuffer> Frames { get; }
        public RasterDecoder(Stream input) => Frames = Array.AsReadOnly(new[] { RasterCodec.Decode(input, true) });
    }

    public static class RasterCodec
    {
        public static PixelBuffer Decode(Stream stream) => Decode(stream, false);

        internal static PixelBuffer Decode(Stream stream, bool preserveColorType)
        {
            // Limit to the first frame; archive image previews are single images.
            using var image = new MagickImage(stream, new MagickReadSettings { FrameCount = 1 });
            using var pixels = image.GetPixels();
            if (preserveColorType && image.ColormapSize is > 0 and <= 256
                && pixels.GetChannelIndex(PixelChannel.Index) is uint indexChannel)
            {
                var colors = new Color[image.ColormapSize];
                for (int i = 0; i < colors.Length; i++)
                {
                    var color = image.GetColormapColor(i);
                    colors[i] = Color.FromArgb(color.A, color.R, color.G, color.B);
                }
                var values = pixels.GetValues();
                var indices = new byte[checked((int)image.Width * (int)image.Height)];
                for (int i = 0; i < indices.Length; i++)
                    indices[i] = values[checked(i * (int)image.ChannelCount + (int)indexChannel)];
                return PixelBuffer.Create((int)image.Width, (int)image.Height, 96, 96,
                    PixelFormats.Indexed8, new BitmapPalette(colors), indices, (int)image.Width);
            }
            if (preserveColorType && !image.HasAlpha)
            {
                var bgr = pixels.ToByteArray("BGR");
                return PixelBuffer.Create(checked((int)image.Width), checked((int)image.Height), 96, 96,
                    PixelFormats.Bgr24, null, bgr, checked((int)image.Width * 3));
            }
            var bytes = pixels.ToByteArray("BGRA");
            return PixelBuffer.Create(checked((int)image.Width), checked((int)image.Height), 96, 96, PixelFormats.Bgra32, null, bytes, checked((int)image.Width * 4));
        }

        public static void Encode(PixelBuffer buffer, Stream output, string format, int quality = 90)
        {
            using var image = new MagickImage();
            image.ReadPixels(buffer.ToBgra32(), new PixelReadSettings((uint)buffer.PixelWidth, (uint)buffer.PixelHeight, StorageType.Char, "BGRA"));
            image.Depth = 8;
            image.Quality = (uint)Math.Clamp(quality, 1, 100);
            image.Write(output, format.ToUpperInvariant() switch
            {
                "PNG" => MagickFormat.Png32,
                "BMP" => MagickFormat.Bmp,
                "JPEG" or "JPG" => MagickFormat.Jpeg,
                "TIFF" or "TIF" => MagickFormat.Tiff,
                _ => throw new NotSupportedException("Unknown output image format: " + format)});
        }
    }
}
