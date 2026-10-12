using System.Text.Json;

namespace SoulCore.Inference.Presence;

/// <summary>
/// Shared arg scrubbing for host-side operator consent (PROP-17.1).
/// Model-minted <c>confirmed</c> is never consent.
/// </summary>
public static class ConsentArgs
{
    public static JsonElement StripModelConfirmed(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return args;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in args.EnumerateObject())
            {
                if (string.Equals(prop.Name, "confirmed", StringComparison.OrdinalIgnoreCase))
                    continue;
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }
}
