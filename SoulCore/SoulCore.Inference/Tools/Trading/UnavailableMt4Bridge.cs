using System.Text.Json;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Stub bridge used when <c>Tools.Mt4Backend</c> is not <c>hermes</c> (native
/// path is out of scope for BED-138). Always returns Success:false without
/// contacting any backend — used to prove gates never auto-fallback.
/// </summary>
public sealed class UnavailableMt4Bridge : IMt4Bridge
{
    private readonly string _reason;

    public UnavailableMt4Bridge(string reason)
    {
        _reason = string.IsNullOrWhiteSpace(reason)
            ? "mt4 backend unavailable"
            : reason.Trim();
    }

    public Task<ToolResult> InvokeAsync(string mcpToolName, JsonElement args, CancellationToken ct = default)
    {
        _ = mcpToolName;
        _ = args;
        _ = ct;
        return Task.FromResult(new ToolResult(false, _reason, null));
    }
}
