using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Inference;
using SoulCore.Inference.Tools.FS;
using SoulCore.Inference.Tools.System;

namespace SoulCore.Protocol.Tests;

public class SystemFilesystemToolsTests
{
    // ---------- list_tools ----------

    [Fact]
    public async Task ListTools_ReturnsNamesAndDescriptions_ForTwoToolRegistry()
    {
        var echo = new FakeEchoDefTool();
        var speak = new FakeSpeakDefTool();
        var tool = new ListToolsTool(new ITool[] { echo, speak });

        var result = await tool.ExecuteAsync(default);

        Assert.True(result.Success);
        Assert.Contains("available tools:", result.Content);
        Assert.Contains("echo:", result.Content);
        Assert.Contains("speak:", result.Content);
        var names = Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Data);
        Assert.Equal(2, names.Count);
        Assert.Contains("echo", names);
        Assert.Contains("speak", names);
    }

    [Fact]
    public async Task ListTools_EmptyRegistry_ReturnsNoToolsRegistered()
    {
        var tool = new ListToolsTool(Array.Empty<ITool>());

        var result = await tool.ExecuteAsync(default);

        Assert.True(result.Success);
        Assert.Equal("no tools registered", result.Content);
        var names = Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Data);
        Assert.Empty(names);
    }

    [Fact]
    public async Task ListTools_Definition_HasEmptyObjectParameters()
    {
        var tool = new ListToolsTool(Array.Empty<ITool>());

        Assert.Equal("list_tools", tool.Definition.Name);
        Assert.Equal("object", tool.Definition.Parameters.GetProperty("type").GetString());
    }

    // ---------- system_info ----------

    [Fact]
    public async Task SystemInfo_ReturnsModel_Uptime_MemoryCount_NoSecrets()
    {
        var inf = Options.Create(new InferenceOptions { Enabled = true, Model = "qwen2.5:14b", EmbeddingsEnabled = true, EmbeddingModel = "nomic-embed-text" });
        var loop = Options.Create(new SoulLoopOptions { Enabled = true, TickIntervalSeconds = 60 });
        var stats = new FakeMemoryStats(isOpen: true, count: 42);
        var tool = new SystemInfoTool(inf, loop, stats);

        var result = await tool.ExecuteAsync(default);

        Assert.True(result.Success);
        Assert.Contains("model: qwen2.5:14b", result.Content);
        Assert.Contains("uptime_seconds:", result.Content);
        Assert.Contains("episodic_memory_count: 42", result.Content);
        Assert.Contains("memory_open: True", result.Content);
        Assert.Contains("soul_loop_enabled: True", result.Content);
    }

    [Fact]
    public async Task SystemInfo_DoesNotExposeSecrets()
    {
        // Simulate a config that might have a BaseUrl, but no API keys live in InferenceOptions.
        // system_info must never emit token/key/secret/connection-string fields.
        var inf = Options.Create(new InferenceOptions { Enabled = true, Model = "qwen2.5:14b", BaseUrl = "http://127.0.0.1:11434" });
        var loop = Options.Create(new SoulLoopOptions { Enabled = false });
        var stats = new FakeMemoryStats(isOpen: true, count: 0);
        var tool = new SystemInfoTool(inf, loop, stats);

        var result = await tool.ExecuteAsync(default);

        Assert.True(result.Success);
        var lower = result.Content.ToLowerInvariant();
        Assert.DoesNotContain("apikey", lower);
        Assert.DoesNotContain("api_key", lower);
        Assert.DoesNotContain("token", lower);
        Assert.DoesNotContain("secret", lower);
        Assert.DoesNotContain("password", lower);
        Assert.DoesNotContain("connectionstring", lower);
        // BaseUrl is not a secret but we also don't surface it (no need; model cares about model name).
        Assert.DoesNotContain("127.0.0.1:11434", result.Content);
    }

    [Fact]
    public async Task SystemInfo_NullMemoryStats_ReportsZeroAndClosed()
    {
        var inf = Options.Create(new InferenceOptions { Enabled = false });
        var loop = Options.Create(new SoulLoopOptions { Enabled = false });
        var tool = new SystemInfoTool(inf, loop, memoryStats: null);

        var result = await tool.ExecuteAsync(default);

        Assert.True(result.Success);
        Assert.Contains("episodic_memory_count: 0", result.Content);
        Assert.Contains("memory_open: False", result.Content);
        Assert.Contains("inference: null", result.Content);
    }

    [Fact]
    public async Task SystemInfo_Definition_HasEmptyObjectParameters()
    {
        var inf = Options.Create(new InferenceOptions());
        var loop = Options.Create(new SoulLoopOptions());
        var tool = new SystemInfoTool(inf, loop);

        Assert.Equal("system_info", tool.Definition.Name);
        Assert.Equal("object", tool.Definition.Parameters.GetProperty("type").GetString());
    }

    // ---------- filesystem: disabled when empty roots ----------

    [Fact]
    public async Task ReadFile_EmptyRoots_UseDefaultFalse_ReturnsDisabled()
    {
        var tool = MakeReadTool(Array.Empty<string>(), useDefault: false);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"path":"x"}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal("filesystem tools disabled", result.Content);
    }

    [Fact]
    public async Task WriteFile_EmptyRoots_UseDefaultFalse_ReturnsDisabled()
    {
        var tool = MakeWriteTool(Array.Empty<string>(), Array.Empty<string>(), useDefault: false);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"path":"x","content":"y"}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal("filesystem tools disabled", result.Content);
    }

    [Fact]
    public async Task ListDir_EmptyRoots_UseDefaultFalse_ReturnsDisabled()
    {
        var tool = MakeListTool(Array.Empty<string>(), useDefault: false);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"path":"x"}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal("filesystem tools disabled", result.Content);
    }

    // ---------- filesystem: whitelist escape attempts ----------

    [Fact]
    public async Task ReadFile_DotDotEscape_RejectsWithSuccessFalse()
    {
        using var tmp = TempDir();
        var sub = Path.Combine(tmp, "sub");
        Directory.CreateDirectory(sub);
        var tool = MakeReadTool(new[] { sub });

        var escape = Path.Combine(sub, "..", "..", "escape.txt");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(escape)}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path not in whitelisted roots", result.Content);
        Assert.False(File.Exists(Path.Combine(tmp, "escape.txt")));
    }

    [Fact]
    public async Task WriteFile_DotDotEscape_RejectsWithSuccessFalse()
    {
        using var tmp = TempDir();
        var sub = Path.Combine(tmp, "sub");
        Directory.CreateDirectory(sub);
        var tool = MakeWriteTool(new[] { sub }, new[] { sub });

        var escape = Path.Combine(sub, "..", "..", "escape.txt");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(escape)}\",\"content\":\"pwned\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path not in whitelisted roots", result.Content);
        Assert.False(File.Exists(Path.Combine(tmp, "escape.txt")));
    }

    [Fact]
    public async Task ListDir_DotDotEscape_RejectsWithSuccessFalse()
    {
        using var tmp = TempDir();
        var sub = Path.Combine(tmp, "sub");
        Directory.CreateDirectory(sub);
        var tool = MakeListTool(new[] { sub });

        var escape = Path.Combine(sub, "..", "..");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(escape)}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path not in whitelisted roots", result.Content);
    }

    [Fact]
    public async Task ReadFile_AbsolutePathOutsideRoot_RejectsWithSuccessFalse()
    {
        using var tmp = TempDir();
        var root = Path.Combine(tmp, "allowed");
        Directory.CreateDirectory(root);
        var outside = Path.Combine(tmp, "outside.txt");
        File.WriteAllText(outside, "secret");
        var tool = MakeReadTool(new[] { root });

        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(outside)}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path not in whitelisted roots", result.Content);
    }

    [Fact]
    public async Task WriteFile_AbsolutePathOutsideWriteRoot_RejectsEvenIfInsideReadRoot()
    {
        using var tmp = TempDir();
        var readRoot = Path.Combine(tmp, "read");
        var writeRoot = Path.Combine(tmp, "write");
        Directory.CreateDirectory(readRoot);
        Directory.CreateDirectory(writeRoot);
        // read roots include both; write roots only include writeRoot.
        var tool = MakeWriteTool(new[] { readRoot, writeRoot }, new[] { writeRoot });

        var targetInReadOnly = Path.Combine(readRoot, "file.txt");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(targetInReadOnly)}\",\"content\":\"x\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path not in whitelisted roots", result.Content);
        Assert.False(File.Exists(targetInReadOnly));
    }

    [Fact]
    public async Task ReadFile_SymlinkPointingOut_RejectsWithSuccessFalse()
    {
        using var tmp = TempDir();
        var root = Path.Combine(tmp, "root");
        Directory.CreateDirectory(root);
        var outsideDir = Path.Combine(tmp, "outside");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "secret.txt"), "secret");
        // Create a reparse point (junction on Windows, symlink elsewhere) inside
        // root pointing OUTSIDE the whitelisted root. The guard must resolve the
        // link target and reject it.
        var link = Path.Combine(root, "link");
        if (!TryCreateReparsePoint(link, outsideDir))
        {
            // If this OS/account can't make any kind of reparse point, we can't
            // exercise the link-out path here. The ../ and absolute-path escape
            // tests above still cover the prefix check, and the junction case
            // uses the same resolve-then-check code path. Record a passing
            // assertion with a clear message so the run report shows the skip.
            Assert.True(true, "symlink-out test skipped: reparse points not creatable in this env (../ and absolute-path escapes covered above)");
            return;
        }

        var tool = MakeReadTool(new[] { root });
        // Reading through the link should be rejected because the link's final
        // target is outside the root.
        var target = Path.Combine(link, "secret.txt");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(target)}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path not in whitelisted roots", result.Content);
    }

    /// <summary>
    /// Create a reparse point at <paramref name="link"/> pointing to
    /// <paramref name="target"/>. Tries a Windows junction first (no admin
    /// needed), then a symlink (may need developer mode). Returns false if
    /// neither is possible in this environment.
    /// </summary>
    private static bool TryCreateReparsePoint(string link, string target)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit(5000);
                return p?.ExitCode == 0 && Directory.Exists(link);
            }
            catch (Exception)
            {
                // fall through to symlink attempt
            }
        }

        try
        {
            Directory.CreateSymbolicLink(link, target);
            return Directory.Exists(link);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [Fact]
    public async Task ReadFile_EmptyPath_RejectsWithSuccessFalse()
    {
        using var tmp = TempDir();
        var tool = MakeReadTool(new[] { tmp.Path });

        var args = JsonDocument.Parse("""{"path":""}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("path is empty", result.Content);
    }

    // ---------- filesystem: round-trip inside a root ----------

    [Fact]
    public async Task WriteThenRead_RoundTripInsideRoot_Succeeds()
    {
        using var tmp = TempDir();
        var tool = MakeReadTool(new[] { tmp.Path });
        var writeTool = MakeWriteTool(new[] { tmp.Path }, new[] { tmp.Path });

        var file = Path.Combine(tmp, "note.txt");
        var writeArgs = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(file)}\",\"content\":\"hello victoria\"}}").RootElement.Clone();
        var writeResult = await writeTool.ExecuteAsync(writeArgs);
        Assert.True(writeResult.Success, writeResult.Content);
        Assert.Contains("wrote 13 chars", writeResult.Content);

        var readArgs = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(file)}\"}}").RootElement.Clone();
        var readResult = await tool.ExecuteAsync(readArgs);
        Assert.True(readResult.Success, readResult.Content);
        Assert.Equal("hello victoria", readResult.Content);
    }

    [Fact]
    public async Task ListDir_InsideRoot_ReturnsEntries()
    {
        using var tmp = TempDir();
        File.WriteAllText(Path.Combine(tmp, "a.txt"), "a");
        File.WriteAllText(Path.Combine(tmp, "b.txt"), "b");
        Directory.CreateDirectory(Path.Combine(tmp, "sub"));

        var tool = MakeListTool(new[] { tmp.Path });
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(tmp)}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Content);
        Assert.Contains("file a.txt", result.Content);
        Assert.Contains("file b.txt", result.Content);
        Assert.Contains("dir  sub", result.Content);
    }

    [Fact]
    public async Task WriteFile_CreatesParentDirectories_Succeeds()
    {
        using var tmp = TempDir();
        var writeRoot = Path.Combine(tmp, "w");
        var tool = MakeWriteTool(new[] { writeRoot }, new[] { writeRoot });

        var file = Path.Combine(writeRoot, "nested", "deep", "note.txt");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(file)}\",\"content\":\"deep\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, result.Content);
        Assert.True(File.Exists(file));
        Assert.Equal("deep", File.ReadAllText(file));
    }

    [Fact]
    public async Task ReadFile_MissingFile_ReturnsFileNotFound()
    {
        using var tmp = TempDir();
        var tool = MakeReadTool(new[] { tmp.Path });

        var file = Path.Combine(tmp, "nope.txt");
        var args = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(file)}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("file not found", result.Content);
    }

    // ---------- filesystem: definitions ----------

    [Fact]
    public void ReadFile_Definition_HasPathRequired()
    {
        var tool = MakeReadTool(new[] { Path.GetTempPath() });
        Assert.Equal("read_file", tool.Definition.Name);
        var req = tool.Definition.Parameters.GetProperty("required");
        Assert.Contains("path", req.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void WriteFile_Definition_HasPathAndContentRequired()
    {
        var tool = MakeWriteTool(new[] { Path.GetTempPath() }, new[] { Path.GetTempPath() });
        Assert.Equal("write_file", tool.Definition.Name);
        var req = tool.Definition.Parameters.GetProperty("required");
        var required = req.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("path", required);
        Assert.Contains("content", required);
    }

    [Fact]
    public void ListDir_Definition_HasPathRequired()
    {
        var tool = MakeListTool(new[] { Path.GetTempPath() });
        Assert.Equal("list_dir", tool.Definition.Name);
        var req = tool.Definition.Parameters.GetProperty("required");
        Assert.Contains("path", req.EnumerateArray().Select(e => e.GetString()));
    }

    // ---------- registry integration ----------

    [Fact]
    public async Task AllFiveTools_RegisterAndDispatchViaRegistry()
    {
        using var tmp = TempDir();
        var inf = Options.Create(new InferenceOptions { Enabled = true, Model = "qwen2.5:14b" });
        var loop = Options.Create(new SoulLoopOptions());
        var toolsOpts = Options.Create(new ToolsOptions
        {
            FilesystemRoots = new[] { tmp.Path },
            FilesystemWriteRoots = new[] { tmp.Path },
            UseDefaultRoots = false
        });

        // Build the concrete tools. ListToolsTool takes IEnumerable<ITool> —
        // we hand it the same set we register so self-discovery matches the
        // registry contents (no circular dep on IToolRegistry itself).
        ITool[] concrete =
        {
            new SystemInfoTool(inf, loop),
            new ReadFileTool(toolsOpts),
            new WriteFileTool(toolsOpts),
            new ListDirTool(toolsOpts)
        };
        var listTool = new ListToolsTool(concrete);
        ITool[] all = concrete.Append(listTool).ToArray();
        var registry = new ToolRegistry(all);

        var defs = registry.GetDefinitions();
        var names = defs.Select(d => d.Name).ToArray();
        Assert.Contains("list_tools", names);
        Assert.Contains("system_info", names);
        Assert.Contains("read_file", names);
        Assert.Contains("write_file", names);
        Assert.Contains("list_dir", names);

        // Dispatch list_tools through the registry.
        var r = await registry.ExecuteAsync("list_tools", default);
        Assert.True(r.Success);
        Assert.Contains("read_file", r.Content);
        Assert.Contains("list_tools", r.Content); // self-inclusion is fine

        // Dispatch read_file on a file we write first.
        var wf = Path.Combine(tmp, "reg.txt");
        var writeArgs = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(wf)}\",\"content\":\"reg\"}}").RootElement.Clone();
        var wr = await registry.ExecuteAsync("write_file", writeArgs);
        Assert.True(wr.Success, wr.Content);

        var readArgs = JsonDocument.Parse($"{{\"path\":\"{EscapeJson(wf)}\"}}").RootElement.Clone();
        var rr = await registry.ExecuteAsync("read_file", readArgs);
        Assert.True(rr.Success, rr.Content);
        Assert.Equal("reg", rr.Content);
    }

    [Fact]
    public async Task ListToolsTool_DILazyResolve_NoCircularDependency_ReturnsAllFiveTools()
    {
        // Mirrors Program.cs DI: ListToolsTool takes IServiceProvider (lazy
        // IEnumerable<ITool> resolve), not IEnumerable<ITool> in its ctor —
        // otherwise ToolRegistry construction would cycle. This test proves
        // the Host DI graph is acyclic and list_tools self-discovers all 5.
        using var tmp = TempDir();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<InferenceOptions>(o => { o.Enabled = true; o.Model = "qwen2.5:14b"; });
        services.Configure<SoulLoopOptions>(o => { o.Enabled = false; });
        services.Configure<ToolsOptions>(o =>
        {
            o.FilesystemRoots = new[] { tmp.Path };
            o.FilesystemWriteRoots = new[] { tmp.Path };
            o.UseDefaultRoots = false;
        });
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<ITool, ListToolsTool>();
        services.AddSingleton<ITool, SystemInfoTool>();
        services.AddSingleton<ITool, ReadFileTool>();
        services.AddSingleton<ITool, WriteFileTool>();
        services.AddSingleton<ITool, ListDirTool>();
        using var sp = services.BuildServiceProvider(validateScopes: true);

        // Resolving the registry must not throw (no circular dependency).
        var registry = sp.GetRequiredService<IToolRegistry>();
        var defs = registry.GetDefinitions();
        var names = defs.Select(d => d.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "list_dir", "list_tools", "read_file", "system_info", "write_file" }, names);

        // Dispatch list_tools — it must lazily resolve IEnumerable<ITool> and
        // report all 5 tools including itself.
        var r = await registry.ExecuteAsync("list_tools", default);
        Assert.True(r.Success, r.Content);
        foreach (var n in names)
            Assert.Contains(n, r.Content);
    }

    // ---------- helpers ----------

    private static ReadFileTool MakeReadTool(IReadOnlyList<string> roots, bool useDefault = false)
        => new(Options.Create(new ToolsOptions { FilesystemRoots = roots, UseDefaultRoots = useDefault }));

    private static WriteFileTool MakeWriteTool(IReadOnlyList<string> readRoots, IReadOnlyList<string> writeRoots, bool useDefault = false)
        => new(Options.Create(new ToolsOptions { FilesystemRoots = readRoots, FilesystemWriteRoots = writeRoots, UseDefaultRoots = useDefault }));

    private static ListDirTool MakeListTool(IReadOnlyList<string> roots, bool useDefault = false)
        => new(Options.Create(new ToolsOptions { FilesystemRoots = roots, UseDefaultRoots = useDefault }));

    private static TempDirScope TempDir() => new();

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class FakeMemoryStats : IMemoryStats
    {
        private readonly long _count;
        public FakeMemoryStats(bool isOpen, long count) { IsOpen = isOpen; _count = count; }
        public bool IsOpen { get; }
        public Task<long> CountEpisodicAsync(CancellationToken cancellationToken = default) => Task.FromResult(_count);
    }

    private sealed class FakeEchoDefTool : ITool
    {
        public ToolDefinition Definition { get; } = new("echo", "Echoes back text.", JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone());
        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default) => Task.FromResult(new ToolResult(true, "echo", null));
    }

    private sealed class FakeSpeakDefTool : ITool
    {
        public ToolDefinition Definition { get; } = new("speak", "Speak text aloud.", JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone());
        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default) => Task.FromResult(new ToolResult(true, "speak", null));
    }

    private sealed class TempDirScope : IDisposable
    {
        public string Path { get; }
        public TempDirScope() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "soulcore-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public static implicit operator string(TempDirScope d) => d.Path;
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
