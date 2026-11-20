using System.Text.Json;
using SoulCore.Inference.Tooling;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-20.5: SMS inbound tool allow/deny. Fail closed for unknown names.
/// Hostile SMS text must never execute denied / unlisted tools.
/// </summary>
public static class SmsToolGate
{
    /// <summary>Product intent: inbound SMS may run a filtered tool loop.</summary>
    public const bool InboundUsesToolLoop = true;

    private static readonly HashSet<string> Allow = new(StringComparer.OrdinalIgnoreCase)
    {
        "browser_navigate",
        "browser_snapshot",
        "browser_health",
        "browser_tabs",
        "browser_back",
        "browser_scroll",
        "browser_click_text",
        "desktop_screenshot",
        "memory_recall",
        "memory_search",
        "research_subject_list",
        "research_subject_open",
        "research_paper_list",
        "research_paper_get",
        "research_paper_write"
    };

    private static readonly HashSet<string> Deny = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop_open_app",
        "desktop_click",
        "desktop_type",
        "desktop_key",
        "desktop_drag",
        "browser_fill",
        "browser_type",
        "browser_key",
        "guest_shell",
        "guest_write_file",
        "email_send",
        "email_draft",
        "email_delete",
        "mt4_order",
        "mt4_close",
        "mt4_modify",
        "workflow_execute",
        "task_release",
        "task_delete",
        "task_update_status"
    };

    /// <summary>True only for the published SMS allow list (deny and unknown → false).</summary>
    public static bool IsAllowed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (IsDenied(name))
            return false;
        return Allow.Contains(name);
    }

    /// <summary>Explicit deny list, plus any guest write / guest_shell family.</summary>
    public static bool IsDenied(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;
        if (Deny.Contains(name))
            return true;
        if (name.StartsWith("guest_write", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(name, "guest_shell", StringComparison.OrdinalIgnoreCase))
            return true;
        // Any other guest_* is not on the allow list — treat as denied for clarity.
        if (name.StartsWith("guest_", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
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

        public FilteringRegistry(IToolRegistry inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public IReadOnlyList<ToolDefinition> GetDefinitions() =>
            FilterDefinitions(_inner.GetDefinitions());

        public Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            if (!IsAllowed(name))
            {
                return Task.FromResult(new ToolResult(
                    Success: false,
                    Content: "error: tool refused on SMS path (PROP-20.5 allow list only).",
                    Data: null));
            }

            return _inner.ExecuteAsync(name, args, ct);
        }
    }
}
