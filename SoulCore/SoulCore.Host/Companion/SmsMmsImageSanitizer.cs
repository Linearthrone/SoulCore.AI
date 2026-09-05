using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.4: strip EXIF/metadata from outbound MMS JPEGs before carrier send.
/// Re-encode via ImageSharp (metadata is not copied on save).
/// </summary>
public static class SmsMmsImageSanitizer
{
    /// <summary>
    /// Re-encode JPEG payloads without metadata. Non-JPEG inputs pass through unchanged.
    /// </summary>
    public static (byte[] Bytes, string ContentType) SanitizeForOutbound(byte[] imageBytes, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            return (imageBytes, NormalizeContentType(contentType));

        var ct = NormalizeContentType(contentType);
        if (!IsJpegContentType(ct) && !LooksLikeJpeg(imageBytes))
            return (imageBytes, ct);

        // High edge cap — MMS carrier limits apply downstream; goal here is metadata removal.
        var stripped = ToolImagePayload.TryCompressForVision(imageBytes, maxEdgePx: 4096, jpegQuality: 88);
        if (stripped is { Length: > 0 })
            return (stripped, "image/jpeg");

        return (imageBytes, ct);
    }

    internal static bool IsJpegContentType(string contentType) =>
        contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeJpeg(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    private static string NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
}
