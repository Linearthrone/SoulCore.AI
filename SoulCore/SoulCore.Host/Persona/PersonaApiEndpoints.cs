using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Persona;
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
            toolPolicy = new
            {
                allowDesktop = pack.ToolPolicy.AllowDesktop,
                allowBrowser = pack.ToolPolicy.AllowBrowser,
                allowEmail = pack.ToolPolicy.AllowEmail
            },
            traits = new
            {
                warmth = pack.Traits.Warmth,
                directness = pack.Traits.Directness,
                formality = pack.Traits.Formality,
                playfulness = pack.Traits.Playfulness,
                boundaryStrictness = pack.Traits.BoundaryStrictness,
                recallBias = pack.Traits.RecallBias
            },
            // Compiled preview helps FED show band effects without a chat turn.
            compiledDirectives = PersonaTraitCompiler.Compile(pack),
            isActive,
            updatedAtUtc = pack.UpdatedAtUtc
        };
    }
}
