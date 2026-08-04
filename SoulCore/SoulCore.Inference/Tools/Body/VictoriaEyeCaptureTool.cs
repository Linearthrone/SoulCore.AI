using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Capture a frame from Victoria's eye-level SceneCapture (UE <c>eye_capture</c>).
/// Returns the same <c>{ bytes, format, width, height }</c> shape as desktop screenshots
/// so Ollama vision can consume it via <c>ToolImagePayload</c>.
/// </summary>
public sealed class VictoriaEyeCaptureTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """).RootElement.Clone();

    private readonly IUnrealVerbClient _unreal;

    public VictoriaEyeCaptureTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "victoria_eye_capture",
        Description: "Capture what Victoria sees from her eye-level camera in the Unreal Home. Use when she needs to look at the room, objects, or her hands while exploring or working.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_unreal.IsConnected)
        {
            return new ToolResult(
                Success: false,
                Content: "error: Unreal bridge not connected — cannot capture eye view.",
                Data: null);
        }

        if (_unreal is not IUnrealEyeCaptureClient eyes)
        {
            return new ToolResult(
                Success: false,
                Content: "error: eye capture not supported by this Unreal client yet.",
                Data: null);
        }

        try
        {
            var frame = await eyes.CaptureEyeAsync(ct).ConfigureAwait(false);
            if (frame is null || frame.Bytes.Length == 0)
            {
                return new ToolResult(
                    Success: false,
                    Content: "error: eye_capture returned no image (is SceneCapture attached?).",
                    Data: null);
            }

            return new ToolResult(
                Success: true,
                Content: $"eye frame {frame.Width}x{frame.Height} {frame.Format}",
                Data: new
                {
                    bytes = frame.Bytes,
                    format = frame.Format,
                    width = frame.Width,
                    height = frame.Height
                });
        }
        catch (Exception ex)
        {
            return new ToolResult(Success: false, Content: $"eye_capture failed: {ex.Message}", Data: null);
        }
    }
}
