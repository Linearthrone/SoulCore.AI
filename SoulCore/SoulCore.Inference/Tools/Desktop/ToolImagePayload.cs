using System.Reflection;
using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Helpers to put screenshot bytes onto the Ollama tool-loop as <c>images[]</c>
/// without serializing raw <see cref="ToolResult.Data"/> JSON to the model (BED-125).
/// </summary>
public static class ToolImagePayload
{
    /// <summary>
    /// Extract base64 image payloads from tool Data when it carries a <c>bytes</c>
    /// property (desktop screenshot backends).
    /// </summary>
    public static List<string>? TryExtractBase64Images(object? data, int maxBytes = 4_000_000)
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

        if (bytes is null || bytes.Length == 0 || bytes.Length > maxBytes)
            return null;

        return new List<string> { Convert.ToBase64String(bytes) };
    }
}
