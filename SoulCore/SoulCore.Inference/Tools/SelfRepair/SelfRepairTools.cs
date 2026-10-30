using System.Text.Json;

namespace SoulCore.Inference.Tools.SelfRepair;

/// <summary>read_tool_code — return the source of one existing tool file.</summary>
public sealed class ReadToolCodeTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"tool_name":{"type":"string","description":"File stem of an existing tool, such as ReadFileTool. No path and no .cs."}},"required":["tool_name"]}
        """).RootElement.Clone();

    private readonly ToolSourceStore _store;

    public ReadToolCodeTool(ToolSourceStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public ToolDefinition Definition { get; } = new(
        Name: "read_tool_code",
        Description: "Read the source of one of Victoria's tool files so a fix can be written. tool_name is the file stem (ReadFileTool), not a path.",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        => Task.FromResult(_store.Read(SelfRepairArgs.String(args, "tool_name")));
}

/// <summary>write_tool_code — replace an existing tool file after writing a .bak beside it.</summary>
public sealed class WriteToolCodeTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"tool_name":{"type":"string","description":"File stem of an existing tool, such as ReadFileTool."},"code":{"type":"string","description":"Full replacement source for that one file."}},"required":["tool_name","code"]}
        """).RootElement.Clone();

    private readonly ToolSourceStore _store;

    public WriteToolCodeTool(ToolSourceStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public ToolDefinition Definition { get; } = new(
        Name: "write_tool_code",
        Description: "Replace one existing tool source file. A timestamped .bak is written first. Does not create new files, and the running host keeps the old assembly until rebuild and restart.",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        => Task.FromResult(_store.Write(SelfRepairArgs.String(args, "tool_name"), SelfRepairArgs.String(args, "code")));
}

/// <summary>run_test_sandbox — compile current tool source and run a snippet out of process.</summary>
public sealed class RunTestSandboxTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"test_code":{"type":"string","description":"C# top-level program. It is compiled against the current tool source and run in a separate process. The snippet itself must finish within 15 seconds."}},"required":["test_code"]}
        """).RootElement.Clone();

    private readonly ToolSourceStore _store;

    public RunTestSandboxTool(ToolSourceStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public ToolDefinition Definition { get; } = new(
        Name: "run_test_sandbox",
        Description: "Compile the current tool source and run test_code in a separate process so a bad fix cannot crash the host. test_code is a C# top-level program. The snippet is limited to 15 seconds.",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        => _store.RunTestSandboxAsync(SelfRepairArgs.String(args, "test_code"), ct);
}

/// <summary>reload_tools — tell the truth about whether a source edit is live.</summary>
public sealed class ReloadToolsTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""").RootElement.Clone();

    private readonly ToolSourceStore _store;

    public ReloadToolsTool(ToolSourceStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public ToolDefinition Definition { get; } = new(
        Name: "reload_tools",
        Description: "Report whether tool source edits are live. Compiled tools stay on the assemblies loaded at startup until SoulCore.Host is rebuilt and restarted.",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        => Task.FromResult(_store.Reload());
}

internal static class SelfRepairArgs
{
    public static string? String(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return null;
        if (!args.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return null;
        return property.GetString();
    }
}
