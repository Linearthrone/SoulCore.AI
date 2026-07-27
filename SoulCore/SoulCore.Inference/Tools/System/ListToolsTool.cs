using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

// Namespace is Meta (not System) so sibling tools under Tools.Body etc. can still
// resolve BCL System.* without colliding with SoulCore.Inference.Tools.System.
namespace SoulCore.Inference.Tools.Meta;

/// <summary>
/// <c>list_tools</c> self-discovery tool. Returns the names + descriptions of
/// every registered tool. No security gate — this is local introspection, no
/// secrets, no side effects. The model can ask "what can I do?" and get a
/// formatted manifest back.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the tool list <b>lazily</b> via <see cref="IServiceProvider"/> at
/// execution time, not via constructor injection of
/// <c>IEnumerable&lt;ITool&gt;</c>. This is mandatory to avoid a DI circular
/// dependency: <c>ToolRegistry</c> is a singleton built from
/// <c>IEnumerable&lt;ITool&gt;</c>, and <c>ListToolsTool</c> is itself one of
/// those <c>ITool</c> instances. If <c>ListToolsTool</c> took
/// <c>IEnumerable&lt;ITool&gt;</c> in its ctor, constructing the registry
/// would construct <c>ListToolsTool</c>, which would need the same enumerable
/// being built — a cycle that breaks every chat turn
/// (<c>ChatWebSocketHandler → IToolRegistry → IEnumerable&lt;ITool&gt;
/// → ListToolsTool → IEnumerable&lt;ITool&gt;</c>).
/// </para>
/// <para>
/// Taking <see cref="IServiceProvider"/> instead defers the
/// <c>IEnumerable&lt;ITool&gt;</c> resolve to <see cref="ExecuteAsync"/>, by
/// which time the registry singleton is already fully constructed. The
/// enumerable resolved then includes <c>ListToolsTool</c> itself (self-
/// inclusion is intended — the model should see <c>list_tools</c> in its own
/// manifest).
/// </para>
/// <para>
/// Only one public constructor exists (the DI ctor) so the container cannot
/// accidentally prefer an <c>IEnumerable&lt;ITool&gt;</c> overload. Unit tests
/// that aren't under DI use <see cref="CreateForTests"/>.
/// </para>
/// </remarks>
public sealed class ListToolsTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IServiceProvider? _provider;
    private readonly IEnumerable<ITool>? _explicitTools;

    /// <summary>
    /// DI ctor — resolves <c>IEnumerable&lt;ITool&gt;</c> lazily at execution
    /// time to break the singleton construction cycle (see remarks on the class).
    /// This is the only public constructor so MS.DI cannot pick a cycle-forming
    /// <c>IEnumerable&lt;ITool&gt;</c> overload.
    /// </summary>
    public ListToolsTool(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    private ListToolsTool(IEnumerable<ITool> tools)
    {
        _explicitTools = tools ?? throw new ArgumentNullException(nameof(tools));
    }

    /// <summary>
    /// Test factory — takes the tool enumerable directly. Use this in unit
    /// tests that aren't running under the DI container.
    /// </summary>
    public static ListToolsTool CreateForTests(IEnumerable<ITool> tools)
        => new(tools);

    public ToolDefinition Definition { get; } = new(
        Name: "list_tools",
        Description: "List all tools available to you with their descriptions.",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var tools = _explicitTools ?? _provider!.GetRequiredService<IEnumerable<ITool>>();

        var defs = tools
            .Where(t => t is not null)
            .Select(t => t.Definition)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        if (defs.Count == 0)
        {
            return Task.FromResult(new ToolResult(
                Success: true,
                Content: "no tools registered",
                Data: Array.Empty<string>()));
        }

        var sb = new StringBuilder("available tools:");
        sb.AppendLine();
        var names = new List<string>(defs.Count);
        foreach (var d in defs)
        {
            sb.Append(" - ").Append(d.Name).Append(": ").Append(d.Description).AppendLine();
            names.Add(d.Name);
        }

        return Task.FromResult(new ToolResult(
            Success: true,
            Content: sb.ToString().TrimEnd(),
            Data: names));
    }
}
