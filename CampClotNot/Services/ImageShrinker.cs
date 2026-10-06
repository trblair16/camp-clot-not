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
    private static readonly WebpEncoder LosslessEncoder = new() { FileFormat = WebpFileFormatType.Lossless };

    /// Photos and flyers: lossy WebP (keeps transparency, much smaller than PNG).
    private static readonly WebpEncoder LossyEncoder = new() { FileFormat = WebpFileFormatType.Lossy, Quality = 85 };

    /// Returns null when the bytes aren't a readable image (or are absurdly large).
    /// The original is kept when it already fits and is smaller than the re-encode.
    public static ShrunkImage? Shrink(byte[] data, string contentType, int maxSide, WebpMode mode)
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

            var encoded = Encode(image, mode);
            if (fits && !rotated && data.LongLength <= encoded.LongLength)
                return new ShrunkImage(data, contentType);
            return new ShrunkImage(encoded, "image/webp");
        }
        catch (Exception ex) when (ex is ImageFormatException or UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }

    private static byte[] Encode(Image image, WebpMode mode)
    {
        byte[] Save(WebpEncoder enc) { using var ms = new MemoryStream(); image.Save(ms, enc); return ms.ToArray(); }
        if (mode == WebpMode.Lossy) return Save(LossyEncoder);
        var lossless = Save(LosslessEncoder);
        if (mode == WebpMode.Lossless) return lossless;
        // Either kind of picture: lossless keeps text and edges sharp, but a photo
        // compresses far better lossy.
        var lossy = Save(LossyEncoder);
        return lossless.LongLength <= lossy.LongLength * 2 ? lossless : lossy;
    }
}

public enum WebpMode
{
    Lossless,  // logos and artwork
    Lossy,     // photos and flyers
    Smallest   // could be either (sponsor logos are sometimes photos): lossless unless it's much bigger
}
