using System.Text.Json;
using SoulCore.Core.Persona;
using SoulCore.Inference.Tooling;

namespace SoulCore.Host.Ws;

/// <summary>
/// Pack <see cref="PersonaToolPolicy"/> applied at dispatch (PROP-16.6).
/// A refused tool is omitted from the definitions and is not executed.
/// </summary>
public static class PersonaToolPolicyFilter
{
    public static bool IsDesktopTool(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Contains("desktop", StringComparison.OrdinalIgnoreCase);

    public static bool IsBrowserTool(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

    public static bool IsEmailTool(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.StartsWith("email_", StringComparison.OrdinalIgnoreCase);

    public static bool Allows(PersonaToolPolicy? policy, string? name)
    {
        policy ??= new PersonaToolPolicy();
        if (IsDesktopTool(name) && !policy.AllowDesktop)
            return false;
        if (IsBrowserTool(name) && !policy.AllowBrowser)
            return false;
        if (IsEmailTool(name) && !policy.AllowEmail)
            return false;
        return true;
    }
}

/// <summary>
/// Registry wrapper. <see cref="GetDefinitions"/> drops refused tools.
/// <see cref="ExecuteAsync"/> returns a refusal and does not call the inner tool.
/// </summary>
public sealed class PolicyFilteredToolRegistry : IToolRegistry
{
    private readonly IToolRegistry _inner;
    private readonly PersonaToolPolicy _policy;

    public PolicyFilteredToolRegistry(IToolRegistry inner, PersonaToolPolicy? policy)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? new PersonaToolPolicy();
    }

    public IReadOnlyList<ToolDefinition> GetDefinitions()
    {
        var all = _inner.GetDefinitions();
        var kept = new List<ToolDefinition>(all.Count);
        foreach (var definition in all)
        {
            if (PersonaToolPolicyFilter.Allows(_policy, definition.Name))
                kept.Add(definition);
        }

        return kept;
    }

    public Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
    {
        if (!PersonaToolPolicyFilter.Allows(_policy, name))
        {
            return Task.FromResult(new ToolResult(
                Success: false,
                Content: $"Tool '{name}' is refused by this persona's tool policy."));
        }

        return _inner.ExecuteAsync(name, args, ct);
    }
}
