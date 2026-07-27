using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Shared helpers for body tools (BED-132): bridge-down failure content and
/// verb invocation that never throws into the agent loop.
/// </summary>
public static class BodyToolBridge
{
    /// <summary>AC-3 content when UnrealBridge is disabled or the WS is closed.</summary>
    public const string UnavailableContent = "unreal bridge unavailable";

    /// <summary>AC-2 success content.</summary>
    public const string OkContent = "ok";

    public static ToolResult Unavailable(object? data = null) =>
        new(Success: false, Content: UnavailableContent, Data: data);

    public static ToolResult Ok(object? data = null) =>
        new(Success: true, Content: OkContent, Data: data);

    /// <summary>
    /// Invokes an <see cref="IUnrealVerbClient"/> method. Returns
    /// <see cref="Unavailable"/> when the verb returns false (bridge disabled,
    /// not connected, or send failed) or throws a non-cancel exception.
    /// </summary>
    public static async Task<ToolResult> InvokeAsync(
        Func<CancellationToken, Task<bool>> invoke,
        object? data,
        CancellationToken ct)
    {
        try
        {
            var ok = await invoke(ct).ConfigureAwait(false);
            return ok ? Ok(data) : Unavailable(data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Unavailable(data);
        }
    }
}
