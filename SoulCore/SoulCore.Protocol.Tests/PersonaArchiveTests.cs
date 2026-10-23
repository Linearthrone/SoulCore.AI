using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Persona;
using SoulCore.Config;
using SoulCore.Host.Persona;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-16.3: episodic source <c>archive</c> stays archive, and
/// POST /api/personas/{personaId}/archive writes that persona's store
/// even when another persona is active.
/// </summary>
public class PersonaArchiveTests
{
    [Fact]
    public async Task WriteEpisodic_SourceArchive_PersistsAsArchive_AndIsNotQuarantined()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-src-archive-{Guid.NewGuid():N}.db");
        try
        {
            long archiveId;
            long importedId;
            await using (var store = new SqliteMemoryStore(path))
            {
                archiveId = await store.WriteEpisodicAsync("caller wrote this archive summary", "Archive");
                importedId = await store.WriteEpisodicAsync("imported row stays quarantined", "imported");
                Assert.True(archiveId > 0);

                var recent = await store.RecallRecentAsync(10);
                Assert.Contains(recent, t => t == "caller wrote this archive summary");
                Assert.DoesNotContain(recent, t => t.Contains("imported row"));
            }

            var archive = await ReadEpisodicAsync(path, archiveId);
            Assert.Equal("caller wrote this archive summary", archive.Content);
            Assert.Equal("archive", archive.Source);
            Assert.Equal(0, archive.Quarantined);

            var imported = await ReadEpisodicAsync(path, importedId);
            Assert.Equal("imported", imported.Source);
            Assert.Equal(1, imported.Quarantined);

            await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            await conn.OpenAsync();
            await using var ledger = conn.CreateCommand();
            ledger.CommandText = "SELECT name FROM schema_migrations WHERE version = '008';";
            Assert.Equal("episodic_source_archive", (string?)await ledger.ExecuteScalarAsync());
        }
        finally
        {
            TryDeleteFile(path);
        }
    }

    [Fact]
    public async Task ArchiveEndpoint_WritesPersonaA_WhileBIsActive_AndDoesNotTouchCharterOrFlags()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "archive-a";
        const string personaB = "archive-b";
        const string summary = "archive-summary-only-for-A";
        const string limits = "Companion voice.\n\n## LIMITS\nDo not edit this line.";
        const string charterA = "charter-anchor-only-A";
        try
        {
            await packs.UpsertAsync(Pack(personaA, limits, allowDesktop: false, allowBrowser: true, allowEmail: false));
            await packs.UpsertAsync(Pack(personaB, "Persona B blurb.", allowDesktop: true, allowBrowser: false, allowEmail: true));

            await hub.SwitchActiveAsync(personaA);
            await hub.GetCharter(personaA).SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "A", charterA, 10, true)
            });
            var anchorsBefore = await hub.GetCharter(personaA).GetAnchorsAsync();
            var packPathA = Path.Combine(root, personaA, "pack.json");
            var packBefore = await File.ReadAllTextAsync(packPathA);

            await hub.SwitchActiveAsync(personaB);
            Assert.Equal(personaB, hub.ActivePersonaId);

            await using var api = await ArchiveApi.StartAsync(packs, hub);
            var response = await api.Client.PostAsJsonAsync(
                $"/api/personas/{personaA}/archive",
                new { summary = "  " + summary + "  " });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(personaB, hub.ActivePersonaId);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(personaA, body.RootElement.GetProperty("personaId").GetString());
            Assert.Equal("archive", body.RootElement.GetProperty("source").GetString());
            var episodicId = body.RootElement.GetProperty("episodicId").GetInt64();

            var aRecent = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Contains(aRecent, t => t == summary);
            var bRecent = await hub.GetMemoryStore(personaB).RecallRecentAsync(20);
            Assert.DoesNotContain(bRecent, t => t.Contains(summary));
            Assert.Empty(bRecent);

            var row = await ReadEpisodicAsync(hub.ResolveMemoryDbPath(personaA), episodicId);
            Assert.Equal(summary, row.Content);
            Assert.Equal("archive", row.Source);
            Assert.Equal(0, row.Quarantined);
            Assert.NotEqual(hub.ResolveMemoryDbPath(personaA), hub.ResolveMemoryDbPath(personaB));

            Assert.Equal(packBefore, await File.ReadAllTextAsync(packPathA));
            var packA = await packs.GetAsync(personaA);
            Assert.NotNull(packA);
            Assert.Equal(limits, packA!.IdentityBlurb);
            Assert.False(packA.ToolPolicy.AllowDesktop);
            Assert.True(packA.ToolPolicy.AllowBrowser);
            Assert.False(packA.ToolPolicy.AllowEmail);

            await hub.SwitchActiveAsync(personaA);
            var anchorsAfter = await hub.GetCharter(personaA).GetAnchorsAsync();
            Assert.Equal(anchorsBefore, anchorsAfter);
            Assert.Contains(anchorsAfter, t => t.Contains(charterA));
            Assert.DoesNotContain(anchorsAfter, t => t.Contains(summary));
            var aAfterSwitch = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Contains(aAfterSwitch, t => t == summary);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ArchiveEndpoint_UnknownPersona_Is404()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        try
        {
            await hub.SwitchActiveAsync(PersonaPack.BlankPersonaId);
            await using var api = await ArchiveApi.StartAsync(packs, hub);
            var response = await api.Client.PostAsJsonAsync(
                "/api/personas/not-a-persona/archive",
                new { summary = "should not be stored" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var path = PersonaMemoryPaths.ResolveMemoryDbPath(root, "not-a-persona");
            Assert.False(File.Exists(path));
            Assert.Equal(PersonaPack.BlankPersonaId, hub.ActivePersonaId);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ArchiveEndpoint_BlankSummary_Is400()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "archive-blank";
        try
        {
            await packs.UpsertAsync(Pack(personaA, "blank pack", true, true, true));
            await hub.SwitchActiveAsync(PersonaPack.BlankPersonaId);
            await using var api = await ArchiveApi.StartAsync(packs, hub);

            foreach (var json in new[]
                     {
                         "{}",
                         "{\"summary\":\"\"}",
                         "{\"summary\":\"   \\n  \"}",
                         "{\"summary\":null}",
                         "not-json"
                     })
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await api.Client.PostAsync($"/api/personas/{personaA}/archive", content);
                var payload = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.BadRequest, payload);
            }

            Assert.False(File.Exists(PersonaMemoryPaths.ResolveMemoryDbPath(root, personaA)));
            Assert.Equal(PersonaPack.BlankPersonaId, hub.ActivePersonaId);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ArchiveEndpoint_Over2000_Is400_AndTrimmed2000_WritesArchiveOnA()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "archive-len-a";
        const string personaB = "archive-len-b";
        var exact = new string('a', 2000);
        try
        {
            await packs.UpsertAsync(Pack(personaA, "## LIMITS\nkeep", false, false, false));
            await packs.UpsertAsync(Pack(personaB, "other", true, true, true));
            await hub.SwitchActiveAsync(personaB);
            var packBefore = await File.ReadAllTextAsync(Path.Combine(root, personaA, "pack.json"));
            await using var api = await ArchiveApi.StartAsync(packs, hub);

            var tooLong = await api.Client.PostAsJsonAsync(
                $"/api/personas/{personaA}/archive",
                new { summary = "  " + new string('b', 2001) + "  " });
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            Assert.False(File.Exists(PersonaMemoryPaths.ResolveMemoryDbPath(root, personaA)));

            var ok = await api.Client.PostAsJsonAsync(
                $"/api/personas/{personaA}/archive",
                new { summary = "  " + exact + "  " });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Equal(personaB, hub.ActivePersonaId);

            using var body = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
            var episodicId = body.RootElement.GetProperty("episodicId").GetInt64();
            var row = await ReadEpisodicAsync(hub.ResolveMemoryDbPath(personaA), episodicId);
            Assert.Equal(exact, row.Content);
            Assert.Equal(2000, row.Content.Length);
            Assert.Equal("archive", row.Source);
            Assert.Equal(0, row.Quarantined);

            var bRecent = await hub.GetMemoryStore(personaB).RecallRecentAsync(10);
            Assert.Empty(bRecent);
            Assert.Equal(packBefore, await File.ReadAllTextAsync(Path.Combine(root, personaA, "pack.json")));
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    private static PersonaPack Pack(string id, string blurb, bool allowDesktop, bool allowBrowser, bool allowEmail) =>
        new()
        {
            PersonaId = id,
            DisplayName = id,
            HumanAddress = "Friend",
            ContactId = id,
            IdentityBlurb = blurb,
            ToolPolicy = new PersonaToolPolicy
            {
                AllowDesktop = allowDesktop,
                AllowBrowser = allowBrowser,
                AllowEmail = allowEmail
            }
        };

    private static async Task<(string Content, string Source, long Quarantined)> ReadEpisodicAsync(string path, long id)
    {
        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content, source, is_quarantined FROM episodic_memories WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1), reader.GetInt64(2));
    }

    private sealed class ArchiveApi : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Client { get; }

        private ArchiveApi(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public static async Task<ArchiveApi> StartAsync(IPersonaPackStore packs, IPersonaStoreHub hub)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "PersonaArchiveTests",
                EnvironmentName = "Development"
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton<IPersonaPackStore>(packs);
            builder.Services.AddSingleton<IPersonaStoreHub>(hub);
            // The rest of MapPersonaApi infers these as services. Archive does not call them.
            builder.Services.AddSingleton<IPersonaSession>(new IdlePersonaSession());
            builder.Services.AddSingleton<IPersonaToolPathsResolver>(new IdleToolPaths());
            builder.Services.AddSingleton<IOptions<InferenceOptions>>(Options.Create(new InferenceOptions()));
            var app = builder.Build();
            app.UseDeveloperExceptionPage();
            app.MapPersonaApi();
            await app.StartAsync();

            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var baseUrl = addresses!.Addresses.First();
            return new ArchiveApi(app, new HttpClient { BaseAddress = new Uri(baseUrl) });
        }

        private sealed class IdlePersonaSession : IPersonaSession
        {
            public string ActivePersonaId => PersonaPack.BlankPersonaId;
            public PersonaPack GetActive() => PersonaPack.CreateBlank();
            public bool TryGet(string personaId, out PersonaPack pack)
            {
                pack = GetActive();
                return string.Equals(personaId, ActivePersonaId, StringComparison.OrdinalIgnoreCase);
            }
            public IReadOnlyDictionary<string, PersonaPack> GetLoadedSnapshot() =>
                new Dictionary<string, PersonaPack> { [ActivePersonaId] = GetActive() };
            public Task SetActiveAsync(string personaId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task ReloadActiveAsync(CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class IdleToolPaths : IPersonaToolPathsResolver
        {
            public string ResolveDesktopTargetWindowTitle() => "";
            public string ResolvePlaywrightUserDataDir() => "";
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "soulcore-archive-" + Guid.NewGuid().ToString("N"));

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best-effort — SQLite may briefly hold handles on Windows
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
        try { File.Delete(path + "-wal"); } catch { /* best-effort */ }
        try { File.Delete(path + "-shm"); } catch { /* best-effort */ }
    }
}
