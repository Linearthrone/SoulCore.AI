using System.Globalization;
using System.Text.Json;
using SoulCore.Core;
using SoulCore.Protocol;

namespace SoulCore.Adapters.Ws.Protocol;

/// <summary>
/// Maps SoulCore Unreal verb names + payloads to UE wire frames
/// (plain <c>speak</c> / <c>move_avatar_relative</c> for PlainArgs; JSON command envelopes for other supported verbs).
/// Does not invent UE verbs; unsupported SoulCore verbs return <see cref="UeWireMapKind.Unsupported"/>.
/// </summary>
public static class UeVerbWireMapper
{
    public enum UeWireMapKind
    {
        Send,
        Unsupported
    }

    public sealed record UeWireMapResult(
        UeWireMapKind Kind,
        string SoulVerb,
        string? UeCommandName,
        /// <summary>Wire frame text: UE JSON envelope, or plain verb frame (Speak / Loco PlainArgs).</summary>
        string? WireJson);

    /// <summary>
    /// Attempt to map a SoulCore verb to a UE wire frame string
    /// (JSON command envelope, or plain <c>speak</c> / <c>move_avatar_relative</c>).
    /// </summary>
    public static UeWireMapResult Map(string soulVerb, object? payload)
    {
        if (string.IsNullOrWhiteSpace(soulVerb))
        {
            return new UeWireMapResult(UeWireMapKind.Unsupported, soulVerb ?? string.Empty, null, null);
        }

        var verb = soulVerb.Trim();

        return verb switch
        {
            UnrealVerbTypes.Speak => MapSpeak(payload),
            UnrealVerbTypes.PlayAnimation => MapPlayAnimation(payload),
            UnrealVerbTypes.Look => MapLook(),
            UnrealVerbTypes.SetEmotion => MapSetEmotion(payload),
            UnrealVerbTypes.Loco => MapLoco(payload),
            _ => Unsupported(verb)
        };
    }

    /// <summary>
    /// MyProject BridgeServer <c>speak</c> reads text from PlainArgs, not JSON <c>payload.args.text</c>.
    /// Emit plain <c>speak &lt;text&gt;</c> so UE returns <c>success:true</c>.
    /// </summary>
    private static UeWireMapResult MapSpeak(object? payload)
    {
        var text = ExtractString(payload, "text") ?? string.Empty;
        // Collapse control whitespace so a single-line plain frame stays ParseWebSocketMessage-safe.
        var plain = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var wire = string.IsNullOrEmpty(plain) ? "speak" : $"speak {plain}";
        return new UeWireMapResult(UeWireMapKind.Send, UnrealVerbTypes.Speak, "speak", wire);
    }

    private static UeWireMapResult MapPlayAnimation(object? payload)
    {
        var name = ExtractString(payload, "name") ?? string.Empty;
        return Send(UnrealVerbTypes.PlayAnimation, "play_animation", new { name });
    }

    /// <summary>
    /// UE <c>command</c>/<c>set_emotion</c> with valence/arousal/dominance/label.
    /// Missing label is derived from valence/arousal via <see cref="EmotionInfluencePrompt.DescribeLabel"/>.
    /// </summary>
    private static UeWireMapResult MapSetEmotion(object? payload)
    {
        var valence = ExtractDouble(payload, "valence") ?? 0.0;
        var arousal = ExtractDouble(payload, "arousal") ?? 0.0;
        var dominance = ExtractDouble(payload, "dominance") ?? 0.0;
        var label = ExtractString(payload, "label");
        if (string.IsNullOrWhiteSpace(label))
            label = EmotionInfluencePrompt.DescribeLabel(valence, arousal);

        return Send(UnrealVerbTypes.SetEmotion, "set_emotion", new
        {
            valence,
            arousal,
            dominance,
            label
        });
    }

    /// <summary>
    /// Nearest documented UE path for look: autonomy / look_at_player.
    /// Caller payload is ignored; wire always sends fixed look_at_player.
    /// </summary>
    private static UeWireMapResult MapLook()
    {
        return Send(UnrealVerbTypes.Look, "autonomy", new { command = "look_at_player" });
    }

    /// <summary>
    /// <c>forward</c>/<c>right</c>/<c>up</c> map to Unreal local +X/+Y/+Z cm.
    /// Empty payload → default step forward=50. Emit plain <c>move_avatar_relative</c>
    /// (BridgeServer JSON path historically ignored offset; PlainArgs is reliable like speak).
    /// </summary>
    private static UeWireMapResult MapLoco(object? payload)
    {
        var forward = ExtractDouble(payload, "forward");
        var right = ExtractDouble(payload, "right");
        var up = ExtractDouble(payload, "up");

        double f;
        double r;
        double u;
        if (forward is null && right is null && up is null)
        {
            f = 50.0;
            r = 0.0;
            u = 0.0;
        }
        else
        {
            f = forward ?? 0.0;
            r = right ?? 0.0;
            u = up ?? 0.0;
        }

        var wire = string.Format(
            CultureInfo.InvariantCulture,
            "move_avatar_relative {0} {1} {2}",
            f,
            r,
            u);
        return new UeWireMapResult(
            UeWireMapKind.Send,
            UnrealVerbTypes.Loco,
            "move_avatar_relative",
            wire);
    }

    private static UeWireMapResult Send(string soulVerb, string ueName, object args)
    {
        var argsElement = JsonSerializer.SerializeToElement(args, SoulCoreFrame.SerializerOptions);
        var envelope = new UeCommandEnvelope
        {
            Type = "command",
            Payload = new UeCommandPayload
            {
                Name = ueName,
                Args = argsElement
            }
        };
        return new UeWireMapResult(UeWireMapKind.Send, soulVerb, ueName, envelope.ToJson());
    }

    private static UeWireMapResult Unsupported(string soulVerb) =>
        new(UeWireMapKind.Unsupported, soulVerb, null, null);

    private static JsonElement? AsObjectElement(object? payload)
    {
        if (payload is null)
            return null;

        if (payload is JsonElement el)
            return el.ValueKind == JsonValueKind.Object ? el : null;

        try
        {
            var element = JsonSerializer.SerializeToElement(payload, SoulCoreFrame.SerializerOptions);
            return element.ValueKind == JsonValueKind.Object ? element : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractString(object? payload, string propertyName)
    {
        if (payload is string s)
            return s;

        var element = AsObjectElement(payload);
        return element is null ? null : ReadStringProperty(element.Value, propertyName);
    }

    private static double? ExtractDouble(object? payload, string propertyName)
    {
        var element = AsObjectElement(payload);
        return element is null ? null : ReadDoubleProperty(element.Value, propertyName);
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();

        // camelCase already applied by SerializerOptions; also try PascalCase for anonymous-object edge cases
        var pascal = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(pascal, out prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();

        return null;
    }

    private static double? ReadDoubleProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (TryReadNumber(element, propertyName, out var value))
            return value;

        var pascal = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (TryReadNumber(element, pascal, out value))
            return value;

        return null;
    }

    private static bool TryReadNumber(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String
            && double.TryParse(prop.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }
}
