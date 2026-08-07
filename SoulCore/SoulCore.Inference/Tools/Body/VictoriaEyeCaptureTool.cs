using System.Text.Json;
using SoulCore.Adapters.Ws;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Capture a frame from Victoria's eye-level SceneCapture (UE <c>eye_capture</c>).
/// Returns the same <c>{ bytes, format, width, height }</c> shape as desktop screenshots
/// so Ollama vision can consume it via <c>ToolImagePayload</c>, and updates Presence
/// “What she saw” via <see cref="IDesktopViewHub"/>.
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
    private readonly IDesktopViewHub? _view;

    public VictoriaEyeCaptureTool(IUnrealVerbClient unreal, IDesktopViewHub? view = null)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
        _view = view;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "victoria_eye_capture",
        Description:
            "Capture what Victoria sees from her eye-level camera in the Unreal Home. " +
            "REQUIRED before claiming you looked at the room, outside, objects, or Kurt's avatar. " +
            "Presence shows this frame as 'What she saw'.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_unreal.IsConnected)
        {
            return new ToolResult(
                Success: false,
                Content: "error: Unreal bridge not connected — cannot capture eye view. Do not invent what you see.",
                Data: null);
        }

        if (_unreal is not IUnrealEyeCaptureClient eyes)
        {
            return new ToolResult(
                Success: false,
                Content: "error: eye capture not supported by this Unreal client yet. Do not invent what you see.",
                Data: null);
        }

        try
        {
            var frame = await eyes.CaptureEyeAsync(ct).ConfigureAwait(false);
            if (frame is null || frame.Bytes.Length == 0)
            {
                return new ToolResult(
                    Success: false,
                    Content: "error: eye_capture returned no image (is SceneCapture attached?). Do not invent what you see.",
                    Data: null);
            }

            _view?.RecordScreenshot(
                frame.Bytes,
                frame.Format,
                frame.Width,
                frame.Height,
                path: null,
                source: DesktopViewHub.SourceEyes,
                action: $"eye_capture {frame.Width}x{frame.Height}");

            return new ToolResult(
                Success: true,
                Content: $"eye frame {frame.Width}x{frame.Height} {frame.Format} — this is what you actually see now",
                Data: new
                {
                    bytes = frame.Bytes,
                    format = frame.Format,
                    width = frame.Width,
                    height = frame.Height,
                    source = DesktopViewHub.SourceEyes
                });
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"eye_capture failed: {ex.Message}. Do not invent what you see.",
                Data: null);
        }
    }
}
