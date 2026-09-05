using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-140: task_create / task_get / task_update_status / task_list tools +
/// SQLite <c>victoria_tasks</c> store round-trip.
/// </summary>
public class TaskToolsTests
{
    [Fact]
    public async Task CreateGetUpdateList_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var create = new TaskCreateTool(store);
            var get = new TaskGetTool(store);
            var update = new TaskUpdateStatusTool(store);
            var list = new TaskListTool(store);

            // create
            var createArgs = JsonDocument.Parse(
                """{"title":"Ship BED-140","description":"Wire task tools","priority":"high"}""")
                .RootElement.Clone();
            var created = await create.ExecuteAsync(createArgs);
            Assert.True(created.Success);
            Assert.Contains("created: id=", created.Content, StringComparison.Ordinal);
            Assert.NotNull(created.Data);

            var id = GetDataLong(created.Data!, "id");
            Assert.True(id > 0);
            Assert.Equal("todo", GetDataString(created.Data!, "status"));
            Assert.Equal("high", GetDataString(created.Data!, "priority"));

            // get
            var got = await get.ExecuteAsync(JsonDocument.Parse($"{{\"id\":{id}}}").RootElement.Clone());
            Assert.True(got.Success);
            var task = Assert.IsType<VictoriaTask>(got.Data);
            Assert.Equal(id, task.Id);
            Assert.Equal("Ship BED-140", task.Title);
            Assert.Equal("Wire task tools", task.Description);
            Assert.Equal("todo", task.Status);
            Assert.Equal("high", task.Priority);

            // update status
            var updated = await update.ExecuteAsync(
                JsonDocument.Parse($"{{\"id\":{id},\"status\":\"in_progress\"}}").RootElement.Clone());
            Assert.True(updated.Success);
            Assert.Contains("status=in_progress", updated.Content, StringComparison.Ordinal);

            var after = await store.GetAsync(id);
            Assert.NotNull(after);
            Assert.Equal("in_progress", after!.Status);
            Assert.True(string.CompareOrdinal(after.UpdatedAt, after.CreatedAt) >= 0);

            // list all + filtered
            var all = await list.ExecuteAsync(JsonDocument.Parse("{}").RootElement.Clone());
            Assert.True(all.Success);
            Assert.Contains("Ship BED-140", all.Content, StringComparison.Ordinal);
            Assert.Equal(1, GetDataInt(all.Data!, "count"));

            var filtered = await list.ExecuteAsync(
                JsonDocument.Parse("""{"status":"in_progress"}""").RootElement.Clone());
            Assert.True(filtered.Success);
            Assert.Equal(1, GetDataInt(filtered.Data!, "count"));

            var emptyFilter = await list.ExecuteAsync(
                JsonDocument.Parse("""{"status":"done"}""").RootElement.Clone());
            Assert.True(emptyFilter.Success);
            Assert.Equal(0, GetDataInt(emptyFilter.Data!, "count"));
            Assert.Contains("no tasks with status=done", emptyFilter.Content, StringComparison.Ordinal);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task TaskGet_MissingId_ReturnsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-miss-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var get = new TaskGetTool(store);

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
    public async Task TaskUpdateStatus_MissingId_ReturnsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-updmiss-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var update = new TaskUpdateStatusTool(store);

            var result = await update.ExecuteAsync(
                JsonDocument.Parse("""{"id":42,"status":"done"}""").RootElement.Clone());

            Assert.False(result.Success);
            Assert.Contains("not found", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("pending")]
    [InlineData("")]
    public async Task TaskUpdateStatus_InvalidStatus_ReturnsFailure(string status)
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-badstat-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var create = new TaskCreateTool(store);
            var update = new TaskUpdateStatusTool(store);

            var created = await create.ExecuteAsync(
                JsonDocument.Parse("""{"title":"x"}""").RootElement.Clone());
            var id = GetDataLong(created.Data!, "id");

            var json = status.Length == 0
                ? $"{{\"id\":{id},\"status\":\"\"}}"
                : $"{{\"id\":{id},\"status\":\"{status}\"}}";
            var result = await update.ExecuteAsync(JsonDocument.Parse(json).RootElement.Clone());

            Assert.False(result.Success);
            Assert.Contains("status", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Theory]
    [InlineData("todo")]
    [InlineData("in_progress")]
    [InlineData("done")]
    [InlineData("blocked")]
    [InlineData("DONE")]
    public async Task TaskUpdateStatus_AllowedStatuses_Succeed(string status)
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-okstat-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var id = await store.CreateAsync("t", null, null);
            var update = new TaskUpdateStatusTool(store);

            var result = await update.ExecuteAsync(
                JsonDocument.Parse($"{{\"id\":{id},\"status\":\"{status}\"}}").RootElement.Clone());

            Assert.True(result.Success);
            var row = await store.GetAsync(id);
            Assert.Equal(status.ToLowerInvariant(), row!.Status);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task TaskCreate_MissingTitle_ReturnsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-notitle-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var create = new TaskCreateTool(store);

            var result = await create.ExecuteAsync(JsonDocument.Parse("{}").RootElement.Clone());

            Assert.False(result.Success);
            Assert.Contains("title", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task TaskCreate_DefaultsStatusTodoAndPriorityMedium()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-defaults-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var create = new TaskCreateTool(store);

            var result = await create.ExecuteAsync(
                JsonDocument.Parse("""{"title":"only title"}""").RootElement.Clone());

            Assert.True(result.Success);
            Assert.Equal("todo", GetDataString(result.Data!, "status"));
            Assert.Equal("medium", GetDataString(result.Data!, "priority"));

            var id = GetDataLong(result.Data!, "id");
            var row = await store.GetAsync(id);
            Assert.Equal("todo", row!.Status);
            Assert.Equal("medium", row.Priority);
            Assert.Equal(string.Empty, row.Description);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void Migration003_IsIdempotent_CreateTableIfNotExists()
    {
        var asm = typeof(SqliteMemoryStore).Assembly;
        using var stream = asm.GetManifestResourceStream("SoulCore.Memory.Migrations.003_victoria_tasks.sql");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var sql = reader.ReadToEnd();
        Assert.Contains("CREATE TABLE IF NOT EXISTS victoria_tasks", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT OR IGNORE INTO schema_migrations", sql, StringComparison.Ordinal);
        Assert.Contains("'003'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration003_AppliedOnOpen_AndReopenIsIdempotent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-mig-{Guid.NewGuid():N}.db");
        try
        {
            await using (var first = new SqliteMemoryStore(path))
            {
                Assert.True(first.IsDatabaseOpen);
                var id = await first.CreateAsync("m", "d", "low");
                Assert.True(id > 0);
            }

            await using var second = new SqliteMemoryStore(path);
            var list = await second.ListAsync();
            Assert.Single(list);
            Assert.Equal("m", list[0].Title);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ToolDefinitions_MatchTicketSchemas()
    {
        IVictoriaTaskStore stub = new StubTaskStore();
        var tools = new ITool[]
        {
            new TaskCreateTool(stub),
            new TaskGetTool(stub),
            new TaskUpdateStatusTool(stub),
            new TaskListTool(stub)
        };

        Assert.Equal(
            new[] { "task_create", "task_get", "task_update_status", "task_list" },
            tools.Select(t => t.Definition.Name).ToArray());

        var create = tools[0].Definition.Parameters;
        Assert.True(create.TryGetProperty("required", out var req));
        Assert.Contains(req.EnumerateArray(), e => e.GetString() == "title");

        var get = tools[1].Definition.Parameters;
        Assert.True(get.TryGetProperty("required", out var getReq));
        Assert.Contains(getReq.EnumerateArray(), e => e.GetString() == "id");

        var upd = tools[2].Definition.Parameters;
        Assert.True(upd.TryGetProperty("required", out var updReq));
        var updRequired = updReq.EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Contains("id", updRequired);
        Assert.Contains("status", updRequired);
    }

    [Fact]
    public void Registry_RegistersAllFourTaskTools()
    {
        IVictoriaTaskStore stub = new StubTaskStore();
        var registry = new ToolRegistry(new ITool[]
        {
            new TaskCreateTool(stub),
            new TaskGetTool(stub),
            new TaskUpdateStatusTool(stub),
            new TaskListTool(stub)
        });

        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("task_create", names);
        Assert.Contains("task_get", names);
        Assert.Contains("task_update_status", names);
        Assert.Contains("task_list", names);
    }

    [Fact]
    public void HostDi_RegistersTaskStoreAndTools()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-tasks-di-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
                new SoulCore.Config.MemoryOptions { DbPath = path }));
            services.AddLogging();
            services.AddSingleton<SqliteMemorySession>();
            services.AddSingleton<SqliteVictoriaTaskRepository>();
            services.AddSingleton<IVictoriaTaskStore>(sp => sp.GetRequiredService<SqliteVictoriaTaskRepository>());
            services.AddSingleton<IToolRegistry, ToolRegistry>();
            services.AddSingleton<ITool, TaskCreateTool>();
            services.AddSingleton<ITool, TaskGetTool>();
            services.AddSingleton<ITool, TaskUpdateStatusTool>();
            services.AddSingleton<ITool, TaskListTool>();

            using var sp = services.BuildServiceProvider();
            var registry = sp.GetRequiredService<IToolRegistry>();
            var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("task_create", names);
            Assert.Contains("task_get", names);
            Assert.Contains("task_update_status", names);
            Assert.Contains("task_list", names);

            var store = sp.GetRequiredService<IVictoriaTaskStore>();
            Assert.Same(sp.GetRequiredService<SqliteVictoriaTaskRepository>(), store);
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

    private static string GetDataString(object data, string property)
    {
        var prop = data.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        Assert.NotNull(prop);
        return Assert.IsType<string>(prop!.GetValue(data));
    }

    private sealed class StubTaskStore : IVictoriaTaskStore
    {
        public Task<long> CreateAsync(string title, string? description, string? priority, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task<VictoriaTask?> GetAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult<VictoriaTask?>(null);

        public Task<bool> UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<VictoriaTask>> ListAsync(string? status = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VictoriaTask>>(Array.Empty<VictoriaTask>());
    }
}
