using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Persona;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;

namespace SoulCore.Host.Persona;

/// <summary>
/// Thin persona CRUD + set-active surface for FED (PROP-15.3/15.4 wizard).
/// Live trait updates persist immediately and apply on the next chat turn.
/// </summary>
public static class PersonaApiEndpoints
{
    public static IEndpointRouteBuilder MapPersonaApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/personas");

        group.MapGet("/", async (
            IPersonaPackStore store,
            IPersonaSession session,
            IOptions<InferenceOptions> inferenceOpts,
            CancellationToken ct) =>
        {
            var packs = await store.ListAsync(ct).ConfigureAwait(false);
            var activeId = session.ActivePersonaId;
            var inf = inferenceOpts.Value;
            return Results.Json(new
            {
                activePersonaId = activeId,
                personas = packs.Select(p => ToDto(
                    p,
                    isActive: string.Equals(p.PersonaId, activeId, StringComparison.OrdinalIgnoreCase),
                    inferenceOptions: inf))
            });
        });

        group.MapGet("/active", (
            IPersonaSession session,
            IPersonaToolPathsResolver toolPaths,
            IOptions<InferenceOptions> inferenceOpts) =>
        {
            var pack = session.GetActive();
            return Results.Json(ToDto(pack, isActive: true, toolPaths, inferenceOpts.Value));
        });

        // PROP-16.6: register /mode before /{personaId} so "mode" is not a persona id.
        group.MapGet("/mode", (IHostChatMode mode) =>
            Results.Json(new { mode = mode.Current }));

        group.MapPost("/mode", async (HttpRequest request, IHostChatMode mode, CancellationToken ct) =>
        {
            string? value;
            try
            {
                value = await ReadHostModeAsync(request, ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON: " + ex.Message });
            }

            if (value is not (IHostChatMode.One or IHostChatMode.Many))
                return Results.BadRequest(new { error = "mode must be \"one\" or \"many\"." });

            await mode.SetAsync(value, ct).ConfigureAwait(false);
            return Results.Json(new { mode = value });
        });

        group.MapGet("/{personaId}", async (
            string personaId,
            IPersonaPackStore store,
            IPersonaSession session,
            IOptions<InferenceOptions> inferenceOpts,
            CancellationToken ct) =>
        {
            var pack = await store.GetAsync(personaId, ct).ConfigureAwait(false);
            if (pack is null)
                return Results.NotFound(new { error = $"Unknown personaId '{personaId}'." });
            var active = string.Equals(pack.PersonaId, session.ActivePersonaId, StringComparison.OrdinalIgnoreCase);
            return Results.Json(ToDto(pack, active, inferenceOptions: inferenceOpts.Value));
        });

        group.MapPost("/", async (
            HttpRequest request,
            IPersonaPackStore store,
            IOptions<InferenceOptions> inferenceOpts,
            CancellationToken ct) =>
        {
            PersonaPack? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<PersonaPack>(
                    request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON: " + ex.Message });
            }

            if (body is null || string.IsNullOrWhiteSpace(body.PersonaId))
                return Results.BadRequest(new { error = "personaId is required." });

            try
            {
                var saved = await store.UpsertAsync(body, ct).ConfigureAwait(false);
                return Results.Json(ToDto(saved, isActive: false, inferenceOptions: inferenceOpts.Value));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{personaId}", async (
            string personaId,
            HttpRequest request,
            IPersonaPackStore store,
            IPersonaSession session,
            IOptions<InferenceOptions> inferenceOpts,
            CancellationToken ct) =>
        {
            // PROP-15.19: sparse PUT merges onto existing pack — never deserialize
            // a partial body into a full PersonaPack (that wiped IdentityBlurb/traits).
            JsonElement patch;
            try
            {
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                    .ConfigureAwait(false);
                patch = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON: " + ex.Message });
            }

            try
            {
                var existing = await store.GetAsync(personaId, ct).ConfigureAwait(false);
                if (existing is null)
                    return Results.NotFound(new { error = $"Unknown personaId '{personaId}'." });

                if (!PersonaPackMerge.TryApplySparsePatch(existing, patch, out var merged, out var mergeError))
                    return Results.BadRequest(new { error = mergeError });

                var saved = await store.UpsertAsync(merged, ct).ConfigureAwait(false);
                // Live adjust = next turn: reload session cache when editing the active pack.
                if (string.Equals(saved.PersonaId, session.ActivePersonaId, StringComparison.OrdinalIgnoreCase))
                    await session.ReloadActiveAsync(ct).ConfigureAwait(false);

                var active = string.Equals(saved.PersonaId, session.ActivePersonaId, StringComparison.OrdinalIgnoreCase);
                return Results.Json(ToDto(saved, active, inferenceOptions: inferenceOpts.Value));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{personaId}/activate", async (
            string personaId,
            IPersonaSession session,
            IPersonaToolPathsResolver toolPaths,
            IOptions<InferenceOptions> inferenceOpts,
            CancellationToken ct) =>
        {
            try
            {
                await session.SetActiveAsync(personaId, ct).ConfigureAwait(false);
                var pack = session.GetActive();
                var inf = inferenceOpts.Value ?? new InferenceOptions();
                var resolvedModel = InferenceModelRouting.ResolveChatModel(inf, pack.InferenceModel);
                return Results.Json(new
                {
                    activePersonaId = pack.PersonaId,
                    note = "Active pack switched; identity/directives/model apply on the next chat turn. PROP-15.5 tool paths + PROP-15.11 InferenceModel follow this pack.",
                    persona = ToDto(pack, isActive: true, toolPaths, inf),
                    resolvedDesktopTargetWindowTitle = toolPaths.ResolveDesktopTargetWindowTitle(),
                    resolvedPlaywrightUserDataDir = toolPaths.ResolvePlaywrightUserDataDir(),
                    resolvedInferenceModel = resolvedModel
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // PROP-16.3: caller-supplied summary into that persona's episodic store.
        // Does not call a model. Does not follow the active facade.
        group.MapPost("/{personaId}/archive", async (
            string personaId,
            HttpRequest request,
            IPersonaPackStore store,
            IPersonaStoreHub hub,
            CancellationToken ct) =>
        {
            string? summary;
            try
            {
                summary = await ReadArchiveSummaryAsync(request, ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON: " + ex.Message });
            }

            if (string.IsNullOrWhiteSpace(summary))
                return Results.BadRequest(new { error = "summary is required." });

            summary = summary.Trim();
            if (summary.Length > ArchiveSummaryMaxLength)
                return Results.BadRequest(new { error = "summary must be at most 2000 characters." });

            PersonaPack? pack;
            try
            {
                pack = await store.GetAsync(personaId, ct).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            if (pack is null)
                return Results.NotFound(new { error = $"Unknown personaId '{personaId}'." });

            var memory = hub.GetMemoryStore(pack.PersonaId);
            var episodicId = await memory.WriteEpisodicAsync(summary, "archive", ct).ConfigureAwait(false);
            return Results.Json(new
            {
                personaId = pack.PersonaId,
                episodicId,
                source = "archive"
            });
        });

        // PROP-16.4: local appearance draft. CompleteAsync only. Does not save.
        group.MapPost("/{personaId}/description-draft", async (
            string personaId,
            HttpRequest request,
            IPersonaPackStore store,
            IInferenceClient inference,
            IOptions<InferenceOptions> inferenceOpts,
            ILoggerFactory logs,
            CancellationToken ct) =>
        {
            string? notes;
            try
            {
                notes = await ReadDraftNotesAsync(request, ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON: " + ex.Message });
            }

            PersonaPack? pack;
            try
            {
                pack = await store.GetAsync(personaId, ct).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            if (pack is null)
                return Results.NotFound(new { error = $"Unknown personaId '{personaId}'." });

            var options = inferenceOpts.Value ?? new InferenceOptions();
            var model = InferenceModelRouting.ResolveChatModel(options, pack.InferenceModel);
            var prompt = BuildDescriptionDraftPrompt(pack, notes);
            string text;
            try
            {
                text = await inference.CompleteAsync(
                    prompt,
                    "You draft physical appearance text for a companion. The operator reviews and saves it. Do not give instructions, do not change limits, and do not request tools.",
                    ct,
                    maxTokens: null,
                    model).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logs.CreateLogger(nameof(PersonaApiEndpoints)).LogWarning(
                    ex,
                    "Description draft model down for {PersonaId}",
                    pack.PersonaId);
                return Results.Json(
                    new { error = "chat.model_down" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            text ??= string.Empty;
            if (text.Length > PersonaPack.PhysicalDescriptionMaxLength)
                text = text[..PersonaPack.PhysicalDescriptionMaxLength];

            return Results.Json(new
            {
                personaId = pack.PersonaId,
                text
            });
        });

        group.MapDelete("/{personaId}", async (string personaId, IPersonaPackStore store, CancellationToken ct) =>
        {
            try
            {
                var deleted = await store.DeleteAsync(personaId, ct).ConfigureAwait(false);
                return deleted
                    ? Results.Json(new { deleted = true, personaId })
                    : Results.NotFound(new { error = $"Unknown personaId '{personaId}'." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }

    private const int ArchiveSummaryMaxLength = 2000;

    /// <summary>
    /// JSON object <c>{"mode":"one"}</c> or <c>{"mode":"many"}</c>.
    /// The value is trimmed. Any other value is rejected by the caller.
    /// </summary>
    private static async Task<string?> ReadHostModeAsync(HttpRequest request, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals("mode", StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.String)
                return null;
            return prop.Value.GetString()?.Trim();
        }

        return null;
    }

    /// <summary>
    /// Body is the summary the caller already wrote. JSON object <c>{"summary":"..."}</c>
    /// or a JSON string. No model call.
    /// </summary>
    private static async Task<string?> ReadArchiveSummaryAsync(HttpRequest request, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.String)
            return root.GetString();

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                continue;
            return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
        }

        return null;
    }

    /// <summary>
    /// Optional JSON object <c>{"notes":"..."}</c> or a JSON string. Empty body means no notes.
    /// The draft route does not write this text anywhere.
    /// </summary>
    private static async Task<string?> ReadDraftNotesAsync(HttpRequest request, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.String)
            return root.GetString();

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals("notes", StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind == JsonValueKind.String)
                return prop.Value.GetString();
            if (prop.Value.ValueKind is JsonValueKind.Null)
                return null;
            throw new JsonException("notes must be a string when provided.");
        }

        return null;
    }

    private static string BuildDescriptionDraftPrompt(PersonaPack pack, string? notes)
    {
        var sb = new StringBuilder();
        sb.Append("Write a physical description of ").Append(pack.DisplayName).Append('.');
        sb.Append(" Appearance only. At most ");
        sb.Append(PersonaPack.PhysicalDescriptionMaxLength);
        sb.Append(" characters.");
        sb.Append(" Do not mention tools, policy, or memory.");
        if (!string.IsNullOrWhiteSpace(pack.PhysicalDescription))
        {
            sb.Append("\nCurrent description:\n");
            sb.Append(pack.PhysicalDescription.Trim());
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            var hint = notes.Trim();
            if (hint.Length > 2000)
                hint = hint[..2000];
            sb.Append("\nOperator notes:\n");
            sb.Append(hint);
        }

        return sb.ToString();
    }

    private static object ToDto(
        PersonaPack pack,
        bool isActive,
        IPersonaToolPathsResolver? toolPaths = null,
        InferenceOptions? inferenceOptions = null)
    {
        string? resolvedTitle = null;
        string? resolvedProfile = null;
        if (isActive && toolPaths is not null)
        {
            resolvedTitle = toolPaths.ResolveDesktopTargetWindowTitle();
            resolvedProfile = toolPaths.ResolvePlaywrightUserDataDir();
        }

        string? resolvedInferenceModel = null;
        if (inferenceOptions is not null)
            resolvedInferenceModel = InferenceModelRouting.ResolveChatModel(inferenceOptions, pack.InferenceModel);

        return new
        {
            personaId = pack.PersonaId,
            displayName = pack.DisplayName,
            humanAddress = pack.HumanAddress,
            contactId = pack.ContactId,
            charterSeedPath = pack.CharterSeedPath,
            playwrightProfileDir = pack.PlaywrightProfileDir,
            vmWindowTitle = pack.VmWindowTitle,
            inferenceModel = pack.InferenceModel,
            resolvedInferenceModel,
            resolvedDesktopTargetWindowTitle = resolvedTitle,
            resolvedPlaywrightUserDataDir = resolvedProfile,
            identityBlurb = pack.IdentityBlurb,
            physicalDescription = pack.PhysicalDescription ?? string.Empty,
            toolPolicy = new
            {
                allowDesktop = pack.ToolPolicy.AllowDesktop,
                allowBrowser = pack.ToolPolicy.AllowBrowser,
                allowEmail = pack.ToolPolicy.AllowEmail
            },
            traits = pack.Traits ?? new PersonaTraitScales(),
            // Compiled preview helps FED show band effects without a chat turn.
            compiledDirectives = PersonaTraitCompiler.Compile(pack),
            isActive,
            updatedAtUtc = pack.UpdatedAtUtc
        };
    }
}
