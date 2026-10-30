using System.Text.Json;
using SoulCore.Inference.Tools.SelfRepair;

namespace SoulCore.Protocol.Tests;

public sealed class SelfRepairToolsTests : IDisposable
{
    private readonly string _root;
    private readonly ToolSourceStore _store;
    private readonly string _projectFile;

    public SelfRepairToolsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "victoria-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Nested"));
        File.WriteAllText(Path.Combine(_root, "Nested", "ReadFileTool.cs"), "original-source");
        _projectFile = Path.Combine(_root, "SoulCore.Inference.csproj");
        File.WriteAllText(_projectFile, "<Project />");
        _store = new ToolSourceStore(_root, _projectFile, new RecordingRunner());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // leftover sandbox handles are cleaned by the store; the fixture dir should still go
        }
    }

    [Fact]
    public async Task ReadToolCode_ReturnsSource()
    {
        var tool = new ReadToolCodeTool(_store);
        var result = await tool.ExecuteAsync(Json("{\"tool_name\":\"ReadFileTool\"}"));

        Assert.True(result.Success);
        Assert.Equal("original-source", result.Content);
    }

    [Fact]
    public async Task ReadToolCode_Missing_ReturnsNotFound()
    {
        var tool = new ReadToolCodeTool(_store);
        var result = await tool.ExecuteAsync(Json("{\"tool_name\":\"DoesNotExist\"}"));

        Assert.False(result.Success);
        Assert.Contains("not found", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadToolCode_RejectsPath()
    {
        var tool = new ReadToolCodeTool(_store);
        var result = await tool.ExecuteAsync(Json("{\"tool_name\":\"..\\\\secret\"}"));

        Assert.False(result.Success);
        Assert.Contains("file stem", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteToolCode_BacksUpThenReplaces()
    {
        var tool = new WriteToolCodeTool(_store);
        var result = await tool.ExecuteAsync(Json("{\"tool_name\":\"ReadFileTool.cs\",\"code\":\"fixed-source\"}"));

        Assert.True(result.Success);
        Assert.Contains("Backup created", result.Content, StringComparison.Ordinal);
        Assert.Equal("fixed-source", File.ReadAllText(Path.Combine(_root, "Nested", "ReadFileTool.cs")));

        var bak = Directory.EnumerateFiles(Path.Combine(_root, "Nested"), "*.bak").Single();
        Assert.Equal("original-source", File.ReadAllText(bak));
    }

    [Fact]
    public async Task WriteToolCode_RejectsEmptyAndUnknown()
    {
        var tool = new WriteToolCodeTool(_store);

        var empty = await tool.ExecuteAsync(Json("{\"tool_name\":\"ReadFileTool\",\"code\":\"\"}"));
        Assert.False(empty.Success);
        Assert.Contains("empty", empty.Content, StringComparison.OrdinalIgnoreCase);

        var missing = await tool.ExecuteAsync(Json("{\"tool_name\":\"NewTool\",\"code\":\"class NewTool {} \"}"));
        Assert.False(missing.Success);
        Assert.Contains("not found", missing.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("original-source", File.ReadAllText(Path.Combine(_root, "Nested", "ReadFileTool.cs")));
    }

    [Fact]
    public async Task WriteToolCode_AmbiguousName_DoesNotWrite()
    {
        var other = Path.Combine(_root, "Other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "ReadFileTool.cs"), "other");

        var tool = new WriteToolCodeTool(_store);
        var result = await tool.ExecuteAsync(Json("{\"tool_name\":\"ReadFileTool\",\"code\":\"nope\"}"));

        Assert.False(result.Success);
        Assert.Contains("more than one", result.Content, StringComparison.Ordinal);
        Assert.Equal("original-source", File.ReadAllText(Path.Combine(_root, "Nested", "ReadFileTool.cs")));
        Assert.Equal("other", File.ReadAllText(Path.Combine(other, "ReadFileTool.cs")));
    }

    [Fact]
    public async Task ReloadTools_SaysRestartIsRequired()
    {
        var result = await new ReloadToolsTool(_store).ExecuteAsync(default);

        Assert.True(result.Success);
        Assert.Contains("rebuilt and restarted", result.Content, StringComparison.Ordinal);
        Assert.Contains("not live", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTestSandbox_CompileTimeout_DoesNotClaimSuccess()
    {
        var runner = new RecordingRunner
        {
            Next = new ToolProcessResult(-1, "", "", TimedOut: true),
        };
        var store = new ToolSourceStore(_root, _projectFile, runner);
        var result = await new RunTestSandboxTool(store).ExecuteAsync(Json("{\"test_code\":\"Console.WriteLine(1);\"}"));

        Assert.False(result.Success);
        Assert.Equal("Error: Test compile timed out.", result.Content);
        Assert.Contains(runner.Calls, call => call.Arguments.Any(a => a.Contains("VictoriaToolSandbox=true", StringComparison.Ordinal)));
        var rootArg = runner.Calls[0].Arguments.Single(a => a.StartsWith("-p:VictoriaToolSandboxRoot=", StringComparison.Ordinal));
        Assert.DoesNotContain("SoulCore.Inference", rootArg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunTestSandbox_SnippetTimeout_ReportsInfiniteLoop()
    {
        var runner = new PlantingRunner();
        var store = new ToolSourceStore(_root, _projectFile, runner);
        try
        {
            var result = await store.RunTestSandboxAsync("while (true) {}", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Error: Test timed out (possible infinite loop created).", result.Content);
            Assert.Equal(3, runner.Calls);
        }
        finally
        {
            var graph = Path.Combine(Path.GetTempPath(), "victoria-tool-sandbox", "graph");
            if (Directory.Exists(graph))
                Directory.Delete(graph, recursive: true);
        }
    }

    [Fact]
    public void EnvScrub_DropsSecretsAndKeepsPath()
    {
        Assert.True(ToolProcessRunner.IsSensitiveEnvName("OPENAI_API_KEY"));
        Assert.True(ToolProcessRunner.IsSensitiveEnvName("ConnectionStrings__Memory"));
        Assert.False(ToolProcessRunner.IsSensitiveEnvName("PATH"));
        Assert.False(ToolProcessRunner.IsSensitiveEnvName("DOTNET_ROOT"));
    }

    [Fact(Timeout = 600000)]
    public async Task RunTestSandbox_LiveSnippet_PrintsAndLoadsToolType()
    {
        var live = ToolSourceStore.CreateDefault();
        var result = await live.RunTestSandboxAsync(
            "Console.WriteLine(typeof(SoulCore.Inference.Tools.FS.ReadFileTool).Name);",
            CancellationToken.None);

        Assert.True(result.Success, result.Content);
        Assert.Contains("ReadFileTool", result.Content, StringComparison.Ordinal);
    }

    private static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class RecordingRunner : IToolProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = new();
        public ToolProcessResult Next { get; set; } = new(0, "", "", false);
        public Func<IReadOnlyList<string>, ToolProcessResult>? OnCall { get; set; }

        public Task<ToolProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            bool scrubSecrets = false)
        {
            Calls.Add((fileName, arguments));
            var result = OnCall is null ? Next : OnCall(arguments);
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Drops dummy assemblies where the store looks after each successful compile,
    /// then reports a timeout on the snippet process itself.
    /// </summary>
    private sealed class PlantingRunner : IToolProcessRunner
    {
        public int Calls { get; private set; }

        public Task<ToolProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            bool scrubSecrets = false)
        {
            Calls++;
            if (Calls == 1)
            {
                var rootArg = arguments.Single(a => a.StartsWith("-p:VictoriaToolSandboxRoot=", StringComparison.Ordinal));
                var root = rootArg["-p:VictoriaToolSandboxRoot=".Length..];
                var bin = Path.Combine(root, "bin", "SoulCore.Inference", "Debug", "net8.0");
                Directory.CreateDirectory(bin);
                File.WriteAllText(Path.Combine(bin, "SoulCore.Inference.dll"), "not-a-real-dll");
                return Task.FromResult(new ToolProcessResult(0, "", "", false));
            }

            if (Calls == 2)
            {
                var args = arguments.ToArray();
                var outIndex = Array.IndexOf(args, "-o");
                var appDir = args[outIndex + 1];
                Directory.CreateDirectory(appDir);
                File.WriteAllText(Path.Combine(appDir, "VictoriaSandbox.dll"), "not-a-real-dll");
                return Task.FromResult(new ToolProcessResult(0, "", "", false));
            }

            return Task.FromResult(new ToolProcessResult(-1, "", "", TimedOut: true));
        }
    }
}
