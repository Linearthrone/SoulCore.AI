using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-141: workflow_create / workflow_get / workflow_execute tools +
/// SQLite <c>victoria_workflows</c> store round-trip.
/// </summary>
public class WorkflowToolsTests
{
    [Fact]
    public async Task CreateGetExecuteSingleExecuteAll_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            IVictoriaWorkflowStore workflows = store;
            var create = new WorkflowCreateTool(workflows);
            var get = new WorkflowGetTool(workflows);

            // Stub tool for step dispatch
            var echo = new EchoTool();
            var registry = new ToolRegistry(new ITool[] { echo });
            var execute = WorkflowExecuteTool.CreateForTests(workflows, registry);

            var createArgs = JsonDocument.Parse(
                """
                {
                  "name": "Ship BED-141",
                  "steps": [
                    { "description": "Describe only" },
                    { "description": "Call echo", "tool": "echo" },
                    { "description": "Final note" }
                  ]
                }
                """).RootElement.Clone();

            var created = await create.ExecuteAsync(createArgs);
            Assert.True(created.Success);
            Assert.Contains("created: id=", created.Content, StringComparison.Ordinal);
            var id = GetDataLong(created.Data!, "id");
            Assert.True(id > 0);
            Assert.Equal(0, GetDataInt(created.Data!, "current_step"));
            Assert.Equal(3, GetDataInt(created.Data!, "steps"));

            // get
            var got = await get.ExecuteAsync(JsonDocument.Parse($"{{\"id\":{id}}}").RootElement.Clone());
            Assert.True(got.Success);
            var wf = Assert.IsType<VictoriaWorkflow>(got.Data);
            Assert.Equal("Ship BED-141", wf.Name);
            Assert.Equal(3, wf.Steps.Count);
            Assert.Equal(0, wf.CurrentStep);
            Assert.Null(wf.Steps[0].Tool);
            Assert.Equal("echo", wf.Steps[1].Tool);

            // execute single (description-only step 0)
            var step0 = await execute.ExecuteAsync(JsonDocument.Parse($"{{\"id\":{id}}}").RootElement.Clone());
            Assert.True(step0.Success);
            Assert.Contains("step 0:", step0.Content, StringComparison.Ordinal);
            Assert.Equal(1, GetDataInt(step0.Data!, "current_step"));
            Assert.False(GetDataBool(step0.Data!, "complete"));

            var after0 = await workflows.GetAsync(id);
            Assert.Equal(1, after0!.CurrentStep);

            // execute all remaining (steps 1 + 2)
            var all = await execute.ExecuteAsync(
                JsonDocument.Parse($"{{\"id\":{id},\"all\":true}}").RootElement.Clone());
            Assert.True(all.Success);
            Assert.Contains("tool=echo", all.Content, StringComparison.Ordinal);
            Assert.Contains("Final note", all.Content, StringComparison.Ordinal);
            Assert.True(GetDataBool(all.Data!, "complete"));
            Assert.Equal(3, GetDataInt(all.Data!, "current_step"));
            Assert.True(echo.CallCount >= 1);

            var afterAll = await workflows.GetAsync(id);
            Assert.Equal(3, afterAll!.CurrentStep);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Execute_ToolCallStep_DispatchesViaRegistry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-tool-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            IVictoriaWorkflowStore workflows = store;
            var echo = new EchoTool();
            var registry = new ToolRegistry(new ITool[] { echo });
            var create = new WorkflowCreateTool(workflows);
            var execute = WorkflowExecuteTool.CreateForTests(workflows, registry);

            var created = await create.ExecuteAsync(JsonDocument.Parse(
                """{"name":"t","steps":[{"description":"run echo","tool":"echo"}]}""")
                .RootElement.Clone());
            var id = GetDataLong(created.Data!, "id");

            var result = await execute.ExecuteAsync(JsonDocument.Parse($"{{\"id\":{id}}}").RootElement.Clone());

            Assert.True(result.Success);
            Assert.Equal(1, echo.CallCount);
            Assert.Contains("tool=echo", result.Content, StringComparison.Ordinal);
            Assert.True(GetDataBool(result.Data!, "complete"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Execute_ReachedEnd_ReturnsWorkflowComplete()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-end-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            IVictoriaWorkflowStore workflows = store;
            var create = new WorkflowCreateTool(workflows);
            var registry = new ToolRegistry(Array.Empty<ITool>());
            var execute = WorkflowExecuteTool.CreateForTests(workflows, registry);

            var created = await create.ExecuteAsync(JsonDocument.Parse(
                """{"name":"one","steps":[{"description":"only"}]}""")
                .RootElement.Clone());
            var id = GetDataLong(created.Data!, "id");

            var first = await execute.ExecuteAsync(JsonDocument.Parse($"{{\"id\":{id}}}").RootElement.Clone());
            Assert.True(first.Success);
            Assert.True(GetDataBool(first.Data!, "complete"));

            var second = await execute.ExecuteAsync(JsonDocument.Parse($"{{\"id\":{id}}}").RootElement.Clone());
            Assert.False(second.Success);
            Assert.Equal("workflow complete", second.Content);
            Assert.True(GetDataBool(second.Data!, "complete"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WorkflowGet_MissingId_ReturnsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-miss-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var get = new WorkflowGetTool(store);

            var result = await get.ExecuteAsync(JsonDocument.Parse("""{"id":999}""").RootElement.Clone());

            Assert.False(result.Success);
            Assert.Contains("not found", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WorkflowCreate_MissingNameOrSteps_ReturnsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-bad-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var create = new WorkflowCreateTool(store);

            var noName = await create.ExecuteAsync(
                JsonDocument.Parse("""{"steps":[{"description":"x"}]}""").RootElement.Clone());
            Assert.False(noName.Success);
            Assert.Contains("name", noName.Content, StringComparison.OrdinalIgnoreCase);

            var noSteps = await create.ExecuteAsync(
                JsonDocument.Parse("""{"name":"n"}""").RootElement.Clone());
            Assert.False(noSteps.Success);
            Assert.Contains("steps", noSteps.Content, StringComparison.OrdinalIgnoreCase);

            var emptySteps = await create.ExecuteAsync(
                JsonDocument.Parse("""{"name":"n","steps":[]}""").RootElement.Clone());
            Assert.False(emptySteps.Success);
            Assert.Contains("steps", emptySteps.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task ExecuteAll_FromStart_RunsEveryStep()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-all-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            IVictoriaWorkflowStore workflows = store;
            var echo = new EchoTool();
            var registry = new ToolRegistry(new ITool[] { echo });
            var create = new WorkflowCreateTool(workflows);
            var execute = WorkflowExecuteTool.CreateForTests(workflows, registry);

            var created = await create.ExecuteAsync(JsonDocument.Parse(
                """
                {"name":"all",
                 "steps":[
                   {"description":"a"},
                   {"description":"b","tool":"echo"},
                   {"description":"c"}
                 ]}
                """).RootElement.Clone());
            var id = GetDataLong(created.Data!, "id");

            var result = await execute.ExecuteAsync(
                JsonDocument.Parse($"{{\"id\":{id},\"all\":true}}").RootElement.Clone());

            Assert.True(result.Success);
            Assert.Equal(1, echo.CallCount);
            Assert.True(GetDataBool(result.Data!, "complete"));
            Assert.Equal(3, GetDataInt(result.Data!, "current_step"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void Migration005_IsIdempotent_CreateTableIfNotExists()
    {
        var asm = typeof(SqliteMemoryStore).Assembly;
        using var stream = asm.GetManifestResourceStream("SoulCore.Memory.Migrations.005_victoria_workflows.sql");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var sql = reader.ReadToEnd();
        Assert.Contains("CREATE TABLE IF NOT EXISTS victoria_workflows", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT OR IGNORE INTO schema_migrations", sql, StringComparison.Ordinal);
        Assert.Contains("'005'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration005_AppliedOnOpen_AndReopenIsIdempotent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-mig-{Guid.NewGuid():N}.db");
        try
        {
            await using (var first = new SqliteMemoryStore(path))
            {
                IVictoriaWorkflowStore workflows = first;
                var id = await workflows.CreateAsync(
                    "m",
                    new[] { new WorkflowStep("d", null) });
                Assert.True(id > 0);
            }

            await using var second = new SqliteMemoryStore(path);
            IVictoriaWorkflowStore workflows2 = second;
            var row = await workflows2.GetAsync(1);
            Assert.NotNull(row);
            Assert.Equal("m", row!.Name);
            Assert.Single(row.Steps);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ToolDefinitions_MatchTicketSchemas()
    {
        IVictoriaWorkflowStore stub = new StubWorkflowStore();
        var tools = new ITool[]
        {
            new WorkflowCreateTool(stub),
            WorkflowExecuteTool.CreateForTests(stub, new ToolRegistry(Array.Empty<ITool>())),
            new WorkflowGetTool(stub)
        };

        Assert.Equal(
            new[] { "workflow_create", "workflow_execute", "workflow_get" },
            tools.Select(t => t.Definition.Name).ToArray());

        var create = tools[0].Definition.Parameters;
        Assert.True(create.TryGetProperty("required", out var req));
        var required = req.EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Contains("name", required);
        Assert.Contains("steps", required);

        var exec = tools[1].Definition.Parameters;
        Assert.True(exec.TryGetProperty("required", out var execReq));
        Assert.Contains(execReq.EnumerateArray(), e => e.GetString() == "id");
        Assert.True(exec.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("all", out _));

        var get = tools[2].Definition.Parameters;
        Assert.True(get.TryGetProperty("required", out var getReq));
        Assert.Contains(getReq.EnumerateArray(), e => e.GetString() == "id");
    }

    [Fact]
    public void Registry_RegistersAllThreeWorkflowTools()
    {
        IVictoriaWorkflowStore stub = new StubWorkflowStore();
        var registry = new ToolRegistry(new ITool[]
        {
            new WorkflowCreateTool(stub),
            WorkflowExecuteTool.CreateForTests(stub, new ToolRegistry(Array.Empty<ITool>())),
            new WorkflowGetTool(stub)
        });

        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("workflow_create", names);
        Assert.Contains("workflow_execute", names);
        Assert.Contains("workflow_get", names);
    }

    [Fact]
    public void HostDi_RegistersWorkflowStoreAndTools_WithoutCycle()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-di-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
                new SoulCore.Config.MemoryOptions { DbPath = path }));
            services.AddLogging();
            services.AddSingleton<SqliteMemoryStore>();
            services.AddSingleton<IVictoriaWorkflowStore>(sp => sp.GetRequiredService<SqliteMemoryStore>());
            services.AddSingleton<IToolRegistry, ToolRegistry>();
            services.AddSingleton<ITool, WorkflowCreateTool>();
            services.AddSingleton<ITool, WorkflowGetTool>();
            services.AddSingleton<ITool>(sp => new WorkflowExecuteTool(
                sp.GetRequiredService<IVictoriaWorkflowStore>(), sp));
            services.AddSingleton<ITool, EchoTool>();

            using var sp = services.BuildServiceProvider();
            var registry = sp.GetRequiredService<IToolRegistry>();
            var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("workflow_create", names);
            Assert.Contains("workflow_execute", names);
            Assert.Contains("workflow_get", names);
            Assert.Contains("echo", names);

            var store = sp.GetRequiredService<IVictoriaWorkflowStore>();
            Assert.Same(sp.GetRequiredService<SqliteMemoryStore>(), store);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task TaskTools_StillWork_AfterWorkflowMigration()
    {
        // Regression: BED-140 task tools must not break when mig 005 is present.
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-wf-taskreg-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var create = new TaskCreateTool(store);
            var result = await create.ExecuteAsync(
                JsonDocument.Parse("""{"title":"still works"}""").RootElement.Clone());
            Assert.True(result.Success);
            var id = GetDataLong(result.Data!, "id");
            var row = await store.GetAsync(id);
            Assert.Equal("still works", row!.Title);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    private static long GetDataLong(object data, string property)
    {
        var prop = data.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        Assert.NotNull(prop);
        return Convert.ToInt64(prop!.GetValue(data));
    }

    private static int GetDataInt(object data, string property)
    {
        var prop = data.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        Assert.NotNull(prop);
        return Convert.ToInt32(prop!.GetValue(data));
    }

    private static bool GetDataBool(object data, string property)
    {
        var prop = data.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        Assert.NotNull(prop);
        return Assert.IsType<bool>(prop!.GetValue(data));
    }

    private sealed class EchoTool : ITool
    {
        public int CallCount { get; private set; }

        public ToolDefinition Definition { get; } = new(
            Name: "echo",
            Description: "Test echo tool.",
            Parameters: JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new ToolResult(Success: true, Content: "echo-ok", Data: null));
        }
    }

    private sealed class StubWorkflowStore : IVictoriaWorkflowStore
    {
        public Task<long> CreateAsync(string name, IReadOnlyList<WorkflowStep> steps, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task<VictoriaWorkflow?> GetAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult<VictoriaWorkflow?>(null);

        public Task<bool> SetCurrentStepAsync(long id, int currentStep, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
