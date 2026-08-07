using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Relative walk step via Unreal <c>move_avatar_relative</c> (NavMesh path-follow).
/// Use for exploring Home when absolute world coords are unknown.
/// </summary>
public sealed class LocoTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IUnrealVerbClient _unreal;

    public LocoTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "loco",
        Description:
            "Walk a relative step in Home (cm): forward (+X), right (+Y), up (+Z). " +
            "Default forward=100 when omitted. Use with victoria_eye_capture between steps " +
            "to look around while exploring (outside, rooms, finding Kurt's avatar).",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        double? forward = null;
        double? right = null;
        double? up = null;

        if (args.ValueKind == JsonValueKind.Object)
        {
            if (TryReadNumber(args, "forward", out var f)) forward = f;
            if (TryReadNumber(args, "right", out var r)) right = r;
            if (TryReadNumber(args, "up", out var u)) up = u;
        }

        if (forward is null && right is null && up is null)
            forward = 100.0;

        var payload = new
        {
            forward = forward ?? 0.0,
            right = right ?? 0.0,
            up = up ?? 0.0
        };

        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.LocoAsync(payload, ct2),
            data: payload,
            ct).ConfigureAwait(false);
    }

    private static bool TryReadNumber(JsonElement args, string name, out double value)
    {
        value = 0;
        if (!args.TryGetProperty(name, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String
            && double.TryParse(
                prop.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
            return true;

        return false;
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "forward": {
              "type": "number",
              "description": "Forward step in cm (Unreal local +X). Default 100 when all omitted."
            },
            "right": {
              "type": "number",
              "description": "Strafe right in cm (Unreal local +Y)."
            },
            "up": {
              "type": "number",
              "description": "Vertical offset in cm (Unreal local +Z). Prefer 0 for walking."
            }
          }
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
