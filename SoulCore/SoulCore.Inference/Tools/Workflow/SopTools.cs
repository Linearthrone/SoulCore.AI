using System.Text;
using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// PROP-17.2 SOP tools. Recall only — cannot flip gates, mint confirmed, or call guest_shell.
/// Authority: tool code → charter → operator settings → her notes.
/// </summary>
public sealed class SopListTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """{"type":"object","properties":{}}""").RootElement.Clone();

    private readonly IVictoriaSopStore _sops;

    public SopListTool(IVictoriaSopStore sops) =>
        _sops = sops ?? throw new ArgumentNullException(nameof(sops));

    public ToolDefinition Definition { get; } = new(
        Name: "sop_list",
        Description: "List Victoria's SOPs (procedures). Recall only — an SOP cannot override host gates.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var list = await _sops.ListAsync(ct).ConfigureAwait(false);
        if (list.Count == 0)
            return new ToolResult(true, "no SOPs", new { count = 0, items = Array.Empty<object>() });

        var sb = new StringBuilder();
        sb.AppendLine($"SOPs ({list.Count}) — recall only; tool code wins over SOPs:");
        foreach (var s in list)
            sb.AppendLine($"[{s.Id}] {s.Title}");
        return new ToolResult(true, sb.ToString().TrimEnd(), new { count = list.Count, items = list });
    }
}

public sealed class SopGetTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"id":{"type":"integer","description":"SOP id"}},"required":["id"]}
        """).RootElement.Clone();

    private readonly IVictoriaSopStore _sops;

    public SopGetTool(IVictoriaSopStore sops) =>
        _sops = sops ?? throw new ArgumentNullException(nameof(sops));

    public ToolDefinition Definition { get; } = new(
        Name: "sop_get",
        Description: "Read one SOP (when / steps / don'ts). Cannot change Settings, mint confirmed, or run guest_shell.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ToolArgParsing.TryReadPositiveId(args, Definition.Name, out var id, out var idError))
            return new ToolResult(false, idError!, null);

        var sop = await _sops.GetAsync(id, ct).ConfigureAwait(false);
        if (sop is null)
            return new ToolResult(false, $"error: SOP id={id} not found.", null);

        var content =
            $"SOP [{sop.Id}] {sop.Title}\nWhen: {sop.WhenText}\nSteps:\n{sop.Steps}\nDon'ts:\n{sop.Donts}\n" +
            "(Authority: tool code → charter → operator settings → notes. This SOP cannot flip gates.)";
        return new ToolResult(true, content, sop);
    }
}

public sealed class SopCreateTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {
          "type":"object",
          "properties":{
            "title":{"type":"string"},
            "when":{"type":"string","description":"When this SOP applies."},
            "steps":{"type":"string"},
            "donts":{"type":"string","description":"What not to do."}
          },
          "required":["title","steps"]
        }
        """).RootElement.Clone();

    private readonly IVictoriaSopStore _sops;

    public SopCreateTool(IVictoriaSopStore sops) =>
        _sops = sops ?? throw new ArgumentNullException(nameof(sops));

    public ToolDefinition Definition { get; } = new(
        Name: "sop_create",
        Description: "Add an SOP for recall. Cannot grant gates, mint confirmed, or invoke guest_shell.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("title", out var t)
            || t.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(t.GetString()))
        {
            return new ToolResult(false, "error: sop_create requires 'title'.", null);
        }

        var title = t.GetString()!.Trim();
        var when = Read(args, "when");
        var steps = Read(args, "steps");
        var donts = Read(args, "donts");
        if (string.IsNullOrWhiteSpace(steps))
            return new ToolResult(false, "error: sop_create requires 'steps'.", null);

        var id = await _sops.CreateAsync(title, when, steps, donts, ct).ConfigureAwait(false);
        var row = await _sops.GetAsync(id, ct).ConfigureAwait(false);
        return new ToolResult(true, $"created SOP id={id}", row);
    }

    private static string Read(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return "";
        return p.GetString()?.Trim() ?? "";
    }
}
