using SoulCore.Inference.Tooling;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: light tools only for R&amp;R. Host rejects (email/MT4/guest_shell/
/// settings/downloads/second browser) stay refused by existing tool code.
/// </summary>
public static class LeisureTools
{
    private static readonly HashSet<string> Allow = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop_screenshot",
        "desktop_snapshot",
        "browser_snapshot",
        "browser_health",
        "guest_screenshot",
        "guest_read_file",
        "guest_list_dir",
        "task_list",
        "task_get",
        "task_create",
        "sop_list",
        "sop_get",
        "memory_recall",
        "memory_search",
        "list_tools",
        "system_info"
    };

    private static readonly HashSet<string> Deny = new(StringComparer.OrdinalIgnoreCase)
    {
        "guest_shell",
        "email_send",
        "email_delete",
        "email_draft",
        "mt4_order",
        "mt4_close",
        "mt4_modify",
        "desktop_open_app",
        "browser_navigate",
        "workflow_execute",
        "task_update_status",
        "task_release",
        "task_suspend"
    };

    public static bool IsAllowed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (Deny.Contains(name))
            return false;
        if (Allow.Contains(name))
            return true;
        if (name.StartsWith("guest_read", StringComparison.OrdinalIgnoreCase))
            return true;
        return name.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("send", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ToolDefinition> FilterDefinitions(IReadOnlyList<ToolDefinition> all)
    {
        if (all is null || all.Count == 0)
            return Array.Empty<ToolDefinition>();
        return all.Where(d => IsAllowed(d.Name)).ToList();
    }

    public static IToolRegistry FilterRegistry(IToolRegistry inner) =>
        new FilteringRegistry(inner);

    private sealed class FilteringRegistry : IToolRegistry
    {
        private readonly IToolRegistry _inner;

        public FilteringRegistry(IToolRegistry inner) => _inner = inner;

        public IReadOnlyList<ToolDefinition> GetDefinitions() =>
            FilterDefinitions(_inner.GetDefinitions());

        public Task<ToolResult> ExecuteAsync(string name, System.Text.Json.JsonElement args, CancellationToken ct = default)
        {
            if (!IsAllowed(name))
            {
                return Task.FromResult(new ToolResult(
                    Success: false,
                    Content: "error: tool refused during R&R (PROP-17.3 light tools only).",
                    Data: null));
            }

            return _inner.ExecuteAsync(name, args, ct);
        }
    }
}
