using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Persona;
using SoulCore.Host.Persona;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-16.4: description draft calls CompleteAsync only and does not save.
/// </summary>
public class PersonaDescriptionDraftTests
{
    [Fact]
    public async Task DescriptionDraft_PackModel_ReturnsTruncatedText_WritesNothing()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaId = "draft-a";
        const string limits = "Companion voice.\n\n## LIMITS\nDo not edit this line.";
        const string charter = "charter-anchor-draft";
        const string episodic = "episodic-row-before-draft";
        var reply = "DRAFT-" + new string('q', 1200);
        var inference = new DraftInference { Reply = reply };
        try
        {
            var pack = Pack(personaId, limits, "pack-draft:7b");
            await packs.UpsertAsync(pack);
            await hub.SwitchActiveAsync(personaId);
            await hub.GetCharter(personaId).SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "A", charter, 10, true)
            });
            await hub.GetMemoryStore(personaId).WriteEpisodicAsync(episodic, "system");
            var anchorsBefore = await hub.GetCharter(personaId).GetAnchorsAsync();
            var episodicBefore = await hub.GetMemoryStore(personaId).RecallRecentAsync(20);
            var packPath = Path.Combine(root, personaId, "pack.json");
            var packBefore = await File.ReadAllTextAsync(packPath);

            await using var api = await DraftApi.StartAsync(packs, inference, hostModel: "host-draft:latest");
            var response = await api.Client.PostAsJsonAsync(
                $"/api/personas/{personaId}/description-draft",
                new { notes = "green eyes" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var text = body.RootElement.GetProperty("text").GetString();
            Assert.Equal(personaId, body.RootElement.GetProperty("personaId").GetString());
            Assert.NotNull(text);
            Assert.Equal(1000, text!.Length);
            Assert.Equal(reply[..1000], text);
            Assert.Equal(1, inference.CompleteCalls);
            Assert.Equal(0, inference.ToolCalls);
            Assert.Equal("pack-draft:7b", inference.LastModel);
            Assert.Contains("green eyes", inference.LastPrompt, StringComparison.Ordinal);
            Assert.Contains("Draft", inference.LastPrompt, StringComparison.Ordinal);

            Assert.Equal(packBefore, await File.ReadAllTextAsync(packPath));
            var reloaded = await packs.GetAsync(personaId);
            Assert.Equal(string.Empty, reloaded!.PhysicalDescription);
            Assert.Equal(limits, reloaded.IdentityBlurb);
            Assert.False(reloaded.ToolPolicy.AllowDesktop);
            Assert.True(reloaded.ToolPolicy.AllowBrowser);
            Assert.False(reloaded.ToolPolicy.AllowEmail);
            Assert.Equal("pack-draft:7b", reloaded.InferenceModel);
            Assert.Equal(anchorsBefore, await hub.GetCharter(personaId).GetAnchorsAsync());
            Assert.Equal(episodicBefore, await hub.GetMemoryStore(personaId).RecallRecentAsync(20));
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DescriptionDraft_BlankPackModel_UsesHostModel_WritesNothing()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var inference = new DraftInference { Reply = "silver hair, green eyes" };
        const string personaId = "draft-host";
        try
        {
            await packs.UpsertAsync(Pack(personaId, "No limits here.", inferenceModel: "   "));
            var packPath = Path.Combine(root, personaId, "pack.json");
            var packBefore = await File.ReadAllTextAsync(packPath);

            await using var api = await DraftApi.StartAsync(packs, inference, hostModel: "host-draft:latest");
            var response = await api.Client.PostAsJsonAsync(
                $"/api/personas/{personaId}/description-draft",
                new { notes = "" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("silver hair, green eyes", body.RootElement.GetProperty("text").GetString());
            Assert.Equal("host-draft:latest", inference.LastModel);
            Assert.Equal(0, inference.ToolCalls);
            Assert.Equal(packBefore, await File.ReadAllTextAsync(packPath));
            var reloaded = await packs.GetAsync(personaId);
            Assert.Equal(string.Empty, reloaded!.PhysicalDescription);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DescriptionDraft_ModelDown_Is503_WritesNothing()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaId = "draft-down";
        const string episodic = "row-stays-when-model-down";
        var inference = new DraftInference
        {
            Throw = new HttpRequestException("Connection refused (127.0.0.1:11434)")
        };
        try
        {
            await packs.UpsertAsync(Pack(personaId, "## LIMITS\nStay.", "pack-draft:7b"));
            await hub.SwitchActiveAsync(personaId);
            await hub.GetCharter(personaId).SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "A", "charter-stays", 10, true)
            });
            await hub.GetMemoryStore(personaId).WriteEpisodicAsync(episodic, "system");
            var anchorsBefore = await hub.GetCharter(personaId).GetAnchorsAsync();
            var episodicBefore = await hub.GetMemoryStore(personaId).RecallRecentAsync(20);
            var packPath = Path.Combine(root, personaId, "pack.json");
            var packBefore = await File.ReadAllTextAsync(packPath);

            await using var api = await DraftApi.StartAsync(packs, inference, hostModel: "host-draft:latest");
            var response = await api.Client.PostAsJsonAsync(
                $"/api/personas/{personaId}/description-draft",
                new { notes = "do not save" });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("chat.model_down", body.RootElement.GetProperty("error").GetString());
            Assert.Equal(1, inference.CompleteCalls);
            Assert.Equal(0, inference.ToolCalls);
            Assert.Equal(packBefore, await File.ReadAllTextAsync(packPath));
            Assert.Equal(anchorsBefore, await hub.GetCharter(personaId).GetAnchorsAsync());
            Assert.Equal(episodicBefore, await hub.GetMemoryStore(personaId).RecallRecentAsync(20));
            var reloaded = await packs.GetAsync(personaId);
            Assert.Equal(string.Empty, reloaded!.PhysicalDescription);
            Assert.Equal("## LIMITS\nStay.", reloaded.IdentityBlurb);
            Assert.False(reloaded.ToolPolicy.AllowDesktop);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DescriptionDraft_UnknownPersona_Is404_AndDoesNotCallModel()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var inference = new DraftInference();
        try
        {
            await using var api = await DraftApi.StartAsync(packs, inference, hostModel: "host-draft:latest");
            var response = await api.Client.PostAsJsonAsync(
                "/api/personas/missing-persona/description-draft",
                new { notes = "x" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(0, inference.CompleteCalls);
            Assert.Equal(0, inference.ToolCalls);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static PersonaPack Pack(string id, string blurb, string? inferenceModel) => new()
    {
        PersonaId = id,
        DisplayName = "Draft",
        HumanAddress = "Friend",
        ContactId = id,
        IdentityBlurb = blurb,
        InferenceModel = inferenceModel,
        ToolPolicy = new PersonaToolPolicy
        {
            AllowDesktop = false,
            AllowBrowser = true,
            AllowEmail = false
        }
    };

    private sealed class DraftInference : IInferenceClient
    {
        public string Reply { get; set; } = "blue eyes";
        public Exception? Throw { get; set; }
        public int CompleteCalls { get; private set; }
        public int ToolCalls { get; private set; }
        public string? LastModel { get; private set; }
        public string? LastPrompt { get; private set; }

        public Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null)
            => CompleteAsync(prompt, systemPreamble, cancellationToken, maxTokens, model: null);

        public Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble,
            CancellationToken cancellationToken,
            int? maxTokens,
            string? model)
        {
            CompleteCalls++;
            LastModel = model;
            LastPrompt = prompt;
            if (Throw is not null)
                throw Throw;
            return Task.FromResult(Reply);
        }

        public Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null)
        {
            ToolCalls++;
            throw new InvalidOperationException("description draft must not enter the tool loop");
        }
    }

    private sealed class DraftApi : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Client { get; }

        private DraftApi(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public static async Task<DraftApi> StartAsync(
            IPersonaPackStore packs,
            IInferenceClient inference,
            string hostModel)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "PersonaDescriptionDraftTests",
                EnvironmentName = "Development"
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton<IPersonaPackStore>(packs);
            builder.Services.AddSingleton<IInferenceClient>(inference);
            builder.Services.AddSingleton<IPersonaSession>(new IdlePersonaSession());
            builder.Services.AddSingleton<IPersonaToolPathsResolver>(new IdleToolPaths());
            builder.Services.AddSingleton<IOptions<InferenceOptions>>(Options.Create(new InferenceOptions
            {
                Model = hostModel
            }));
            var app = builder.Build();
            app.MapPersonaApi();
            await app.StartAsync();

            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var baseUrl = addresses!.Addresses.First();
            return new DraftApi(app, new HttpClient { BaseAddress = new Uri(baseUrl) });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
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
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "soulcore-desc-draft-" + Guid.NewGuid().ToString("N"));

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
}
