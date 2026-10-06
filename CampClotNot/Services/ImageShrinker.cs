using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace CampClotNot.Services;

public record ShrunkImage(byte[] Data, string ContentType);

/// Downscales uploaded raster images so admins can upload whatever file they have
/// (phone photos, print-resolution logos) while pages only ever serve a small one.
public static class ImageShrinker
{
    // What admins may pick. The stored result is far smaller.
    public const long MaxUploadBytes = 20 * 1024 * 1024;

    // Decoding a huge image costs ~4 bytes per pixel, so refuse anything absurd up front.
    private const long MaxPixels = 60_000_000;

    /// Logos: flat colours and transparency, so lossless WebP stays crisp and small.
    public static readonly WebpEncoder Lossless = new() { FileFormat = WebpFileFormatType.Lossless };

    /// Photos and flyers: lossy WebP (keeps transparency, much smaller than PNG).
    public static readonly WebpEncoder Lossy = new() { FileFormat = WebpFileFormatType.Lossy, Quality = 85 };

    /// Returns null when the bytes aren't a readable image (or are absurdly large).
    /// The original is kept when it already fits and is smaller than the re-encode.
    public static ShrunkImage? Shrink(byte[] data, string contentType, int maxSide, WebpEncoder encoder)
    {
        try
        {
            var info = Image.Identify(data);
            if ((long)info.Width * info.Height > MaxPixels) return null;

            using var image = Image.Load(data);
            // A rotated phone photo must be re-encoded, or it would display sideways.
            var rotated = image.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out var o) == true && o.Value > 1;
            image.Mutate(x => x.AutoOrient());
            var fits = image.Width <= maxSide && image.Height <= maxSide;
            if (!fits)
                image.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxSide, maxSide) }));

            using var ms = new MemoryStream();
            image.Save(ms, encoder);
            if (fits && !rotated && data.LongLength <= ms.Length)
                return new ShrunkImage(data, contentType);
            return new ShrunkImage(ms.ToArray(), "image/webp");
        }
        catch (Exception ex) when (ex is ImageFormatException or UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }
}
