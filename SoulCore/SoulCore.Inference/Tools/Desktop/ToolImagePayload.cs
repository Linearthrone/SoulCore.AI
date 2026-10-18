using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Helpers to put screenshot bytes onto the Ollama tool-loop as <c>images[]</c>
/// without serializing raw <see cref="ToolResult.Data"/> JSON to the model (BED-125).
/// Downscales + JPEG-compresses so vision rounds stay fast.
/// </summary>
public static class ToolImagePayload
{
    /// <summary>
    /// Longest edge for vision payloads. Guest frames around 1510px must stay
    /// 1:1 with <c>desktop_click</c> or the model clicks in the shrunk image
    /// and the pointer lands high and to the left.
    /// </summary>
    public const int DefaultMaxEdgePx = 2048;

    /// <summary>JPEG plus the guest-to-image scale the model must undo.</summary>
    public readonly record struct PreparedVisionImage(
        byte[] Jpeg,
        int SourceWidth,
        int SourceHeight,
        int VisionWidth,
        int VisionHeight);

    /// <summary>JPEG quality for vision (UI screenshots compress well).</summary>
    public const int DefaultJpegQuality = 72;

    /// <summary>Reject after compress if still larger than this.</summary>
    public const int DefaultMaxBytes = 1_500_000;

    /// <summary>Reject Kayleigh chat attaches larger than this before decode.</summary>
    public const int ChatAttachMaxRawBytes = 8_000_000;

    /// <summary>
    /// Prepare one Kayleigh chat-attach (raw file bytes or base64) into a vision JPEG base64.
    /// Null when undecodable / over size.
    /// </summary>
    public static string? TryPrepareChatAttachBase64(
        string? base64OrEmpty,
        int maxRawBytes = ChatAttachMaxRawBytes,
        int maxBytes = DefaultMaxBytes,
        int maxEdgePx = DefaultMaxEdgePx,
        int jpegQuality = DefaultJpegQuality)
    {
        if (string.IsNullOrWhiteSpace(base64OrEmpty))
            return null;

        byte[] raw;
        try
        {
            var s = base64OrEmpty.Trim();
            var comma = s.IndexOf(',');
            if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
                s = s[(comma + 1)..];
            raw = Convert.FromBase64String(s);
        }
        catch
        {
            return null;
        }

        if (raw.Length == 0 || raw.Length > maxRawBytes)
            return null;

        var prepared = TryPrepareVisionImage(raw, maxEdgePx, jpegQuality);
        if (prepared is null || prepared.Value.Jpeg.Length == 0 || prepared.Value.Jpeg.Length > maxBytes)
            return null;
        return Convert.ToBase64String(prepared.Value.Jpeg);
    }

    /// <summary>
    /// Extract a single vision-ready base64 image (JPEG preferred) from tool Data.
    /// </summary>
    public static List<string>? TryExtractBase64Images(
        object? data,
        int maxBytes = DefaultMaxBytes,
        int maxEdgePx = DefaultMaxEdgePx,
        int jpegQuality = DefaultJpegQuality)
    {
        var bytes = TryReadImageBytes(data);
        if (bytes is null || bytes.Length == 0)
            return null;

        var prepared = TryPrepareVisionImage(bytes, maxEdgePx, jpegQuality);
        var compact = prepared?.Jpeg;
        if (compact is null || compact.Length == 0)
        {
            // Undecodable or exotic format — attach original only if small enough.
            if (bytes.Length > maxBytes)
                return null;
            compact = bytes;
        }
        else if (compact.Length > maxBytes)
        {
            return null;
        }

        return new List<string> { Convert.ToBase64String(compact) };
    }

    /// <summary>
    /// Decode tool Data into a vision JPEG plus the framebuffer size the click grid uses.
    /// Null when the buffer is not a decodable image or the JPEG exceeds <paramref name="maxBytes"/>.
    /// </summary>
    public static PreparedVisionImage? TryPrepareFromToolData(
        object? data,
        int maxBytes = DefaultMaxBytes,
        int maxEdgePx = DefaultMaxEdgePx,
        int jpegQuality = DefaultJpegQuality)
    {
        var bytes = TryReadImageBytes(data);
        if (bytes is null || bytes.Length == 0)
            return null;

        var prepared = TryPrepareVisionImage(bytes, maxEdgePx, jpegQuality);
        if (prepared is null || prepared.Value.Jpeg.Length == 0 || prepared.Value.Jpeg.Length > maxBytes)
            return null;
        return prepared;
    }

    private static byte[]? TryReadImageBytes(object? data)
    {
        if (data is null)
            return null;

        try
        {
            switch (data)
            {
                case byte[] raw:
                    return raw.Length == 0 ? null : raw;
                case JsonElement je when je.ValueKind == JsonValueKind.Object
                                         && je.TryGetProperty("bytes", out var b)
                                         && b.ValueKind == JsonValueKind.String:
                    return Convert.FromBase64String(b.GetString()!);
                default:
                {
                    var prop = data.GetType().GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                               ?? data.GetType().GetProperty("Bytes", BindingFlags.Public | BindingFlags.Instance);
                    if (prop?.GetValue(data) is byte[] arr && arr.Length > 0)
                        return arr;
                    return null;
                }
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tell the model how to turn a point on the attached JPEG into
    /// <c>desktop_click</c> guest-framebuffer pixels.
    /// </summary>
    public static string FormatClickScaleHint(
        int sourceWidth,
        int sourceHeight,
        int visionWidth,
        int visionHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || visionWidth <= 0 || visionHeight <= 0)
            return "desktop_click uses guest framebuffer pixels (origin 0,0).";

        if (sourceWidth == visionWidth && sourceHeight == visionHeight)
        {
            return
                $"Attached image is {visionWidth}x{visionHeight}, the same grid as the guest framebuffer. " +
                "desktop_click x,y are those pixels (origin 0,0).";
        }

        var scaleX = sourceWidth / (double)visionWidth;
        var scaleY = sourceHeight / (double)visionHeight;
        return
            $"Attached image is {visionWidth}x{visionHeight}. desktop_click uses the full guest framebuffer " +
            $"{sourceWidth}x{sourceHeight} (origin 0,0). " +
            $"Multiply a point on the attached image before clicking: " +
            $"click_x = round(image_x * {scaleX.ToString("0.###", CultureInfo.InvariantCulture)}), " +
            $"click_y = round(image_y * {scaleY.ToString("0.###", CultureInfo.InvariantCulture)}).";
    }

    /// <summary>
    /// Downscale so max(width,height) ≤ <paramref name="maxEdgePx"/> and encode JPEG.
    /// Returns null if the buffer is not a decodable image.
    /// </summary>
    public static byte[]? TryCompressForVision(
        byte[] bytes,
        int maxEdgePx = DefaultMaxEdgePx,
        int jpegQuality = DefaultJpegQuality)
        => TryPrepareVisionImage(bytes, maxEdgePx, jpegQuality)?.Jpeg;

    /// <summary>
    /// Same encode as <see cref="TryCompressForVision"/> plus source and vision sizes.
    /// </summary>
    public static PreparedVisionImage? TryPrepareVisionImage(
        byte[] bytes,
        int maxEdgePx = DefaultMaxEdgePx,
        int jpegQuality = DefaultJpegQuality)
    {
        if (bytes is null || bytes.Length == 0)
            return null;

        maxEdgePx = Math.Clamp(maxEdgePx, 256, 4096);
        jpegQuality = Math.Clamp(jpegQuality, 40, 95);

        try
        {
            using var image = Image.Load(bytes);
            var w = image.Width;
            var h = image.Height;
            if (w <= 0 || h <= 0)
                return null;

            var longest = Math.Max(w, h);
            if (longest > maxEdgePx)
            {
                var scale = maxEdgePx / (double)longest;
                var nw = Math.Max(1, (int)Math.Round(w * scale));
                var nh = Math.Max(1, (int)Math.Round(h * scale));
                image.Mutate(x => x.Resize(nw, nh));
            }

            // ImageSharp round-trips these profiles through SaveAsJpeg, so the re-encode
            // above does not drop them. Screenshots and photos reach a possibly-remote
            // model from here, and EXIF carries GPS.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = jpegQuality });
            return new PreparedVisionImage(ms.ToArray(), w, h, image.Width, image.Height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True when the base64 payload starts with a JPEG SOI marker.</summary>
    public static bool LooksLikeJpegBase64(string? b64)
    {
        if (string.IsNullOrWhiteSpace(b64) || b64.Length < 4)
            return false;
        try
        {
            var head = Convert.FromBase64String(b64.Length >= 8 ? b64[..8] : b64);
            return head.Length >= 2 && head[0] == 0xFF && head[1] == 0xD8;
        }
        catch
        {
            return false;
        }
    }
}
