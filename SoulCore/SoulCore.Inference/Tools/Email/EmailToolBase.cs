using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Email;

/// <summary>Shared gate + bridge wiring for email <see cref="ITool"/>s.</summary>
public abstract class EmailToolBase : ITool
{
    private readonly IOptions<ToolsOptions> _options;
    private readonly IToolsAccessSettings? _access;

    protected EmailToolBase(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, access: null)
    {
    }

    protected EmailToolBase(
        IEmailBridge bridge,
        IOptions<ToolsOptions> options,
        IToolsAccessSettings? access)
    {
        Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _access = access;
    }

    protected IEmailBridge Bridge { get; }

    protected ToolsOptions Options => _options.Value;

    public abstract ToolDefinition Definition { get; }

    public abstract Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default);

    protected bool ReadAllowed =>
        _access is not null
            ? EmailToolSupport.IsReadAllowed(_access)
            : EmailToolSupport.IsReadAllowed(Options);

    protected bool SendAllowed =>
        _access is not null
            ? EmailToolSupport.IsSendAllowed(_access)
            : EmailToolSupport.IsSendAllowed(Options);

    protected bool DeleteAllowed =>
        _access is not null
            ? EmailToolSupport.IsDeleteAllowed(_access)
            : EmailToolSupport.IsDeleteAllowed(Options);

    protected static async Task<ToolResult> GuardedAsync(Func<Task<ToolResult>> work)
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                false,
                $"error: {ex.GetType().Name}: {ex.Message}",
                null);
        }
    }
}
