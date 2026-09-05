using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.4: strip EXIF/metadata from outbound MMS JPEGs before carrier send.
/// </summary>
public static class SmsMmsImageSanitizer
{
    public static (byte[] Bytes, string ContentType) SanitizeForOutbound(byte[] imageBytes, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            return (imageBytes, NormalizeContentType(contentType));

        var ct = NormalizeContentType(contentType);
        if (!IsJpegContentType(ct) && !LooksLikeJpeg(imageBytes))
            return (imageBytes, ct);

        try
        {
            using var image = Image.Load(imageBytes);
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;
            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = 88 });
            return (ms.ToArray(), "image/jpeg");
        }
        catch
        {
            return (imageBytes, ct);
        }
    }

    internal static bool IsJpegContentType(string contentType) =>
        contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeJpeg(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    private static string NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
}
