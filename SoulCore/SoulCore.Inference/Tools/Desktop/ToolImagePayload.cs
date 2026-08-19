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
    /// <summary>Longest edge for vision payloads (keeps clickable UI detail).</summary>
    public const int DefaultMaxEdgePx = 1024;

    /// <summary>JPEG quality for vision (UI screenshots compress well).</summary>
    public const int DefaultJpegQuality = 72;

    /// <summary>Reject after compress if still larger than this.</summary>
    public const int DefaultMaxBytes = 1_500_000;

    /// <summary>
    /// Extract a single vision-ready base64 image (JPEG preferred) from tool Data.
    /// </summary>
    public static List<string>? TryExtractBase64Images(
        object? data,
        int maxBytes = DefaultMaxBytes,
        int maxEdgePx = DefaultMaxEdgePx,
        int jpegQuality = DefaultJpegQuality)
    {
        if (data is null) return null;

        byte[]? bytes = null;
        try
        {
            switch (data)
            {
                case byte[] raw:
                    bytes = raw;
                    break;
                case JsonElement je when je.ValueKind == JsonValueKind.Object
                                         && je.TryGetProperty("bytes", out var b)
                                         && b.ValueKind == JsonValueKind.String:
                    bytes = Convert.FromBase64String(b.GetString()!);
                    break;
                default:
                {
                    var prop = data.GetType().GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                               ?? data.GetType().GetProperty("Bytes", BindingFlags.Public | BindingFlags.Instance);
                    if (prop?.GetValue(data) is byte[] arr)
                        bytes = arr;
                    break;
                }
            }
        }
        catch
        {
            return null;
        }

        if (bytes is null || bytes.Length == 0)
            return null;

        var compact = TryCompressForVision(bytes, maxEdgePx, jpegQuality);
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
    /// Downscale so max(width,height) ≤ <paramref name="maxEdgePx"/> and encode JPEG.
    /// Returns null if the buffer is not a decodable image.
    /// </summary>
    public static byte[]? TryCompressForVision(
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

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = jpegQuality });
            return ms.ToArray();
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
