using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Host.Inference;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Host.Hosting;

/// <summary>
/// PROP-22.2: operator work items, week agenda, inference profiles/apply, host log tail
/// (Presence glass shell). Mapped from <see cref="WebApplicationExtensions.UseSoulCoreWeb"/>.
/// Contract: docs/agents/unexecuted_proposals/presence-glass-shell.md §3.
/// Request bodies accept the contract's snake_case column names (camelCase also accepted);
/// responses use the column names verbatim.
/// </summary>
internal static partial class OperatorSurfaceApiExtensions
{
    private const string OllamaTagsUrl = "http://127.0.0.1:11434/api/tags";
    private const int LogTailMaxBytes = 512 * 1024;
    private const int LogTailMaxLines = 2000;
    private const int LogTailDefaultLines = 200;
    private const string HostLogFileName = ".soulcore-host.log";

    internal static IEndpointRouteBuilder MapOperatorSurfaceApi(this IEndpointRouteBuilder app)
    {
        MapWorkItems(app);
        MapAgenda(app);
        MapInferenceSettings(app);
        MapSystemLog(app);
        return app;
    }

    // ── §3.1 work items ─────────────────────────────────────────────────────────────

    private static void MapWorkItems(IEndpointRouteBuilder app)
    {
        app.MapGet("/operator/work-items", async (
            IOperatorWorkItemStore store,
            CancellationToken cancellationToken) =>
        {
            var rows = await store.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Json(new { count = rows.Count, items = rows.Select(WorkItemDto) });
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);

        app.MapPost("/operator/work-items", async (
            HttpRequest request,
            IOperatorWorkItemStore store,
            CancellationToken cancellationToken) =>
        {
            var parsed = await ReadJsonObjectAsync(request, cancellationToken).ConfigureAwait(false);
            if (parsed.Error is not null)
                return parsed.Error;
            var root = parsed.Root;

            var kind = ReadString(root, "kind")?.ToLowerInvariant();
            if (kind is null || !OperatorWorkItemRules.AllowedKinds.Contains(kind))
                return BadRequest("kind is required (task | project)");

            var title = ReadString(root, "title");
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest("title is required");

            var priority = ReadInt(root, "priority") ?? 3;
            if (priority is < 1 or > 5)
                return BadRequest("priority must be 1-5");

            var status = ReadString(root, "status")?.ToLowerInvariant() ?? "open";
            if (!OperatorWorkItemRules.AllowedStatuses.Contains(status))
                return BadRequest("status must be open | done | cancelled");

            string? deadline = null;
            var deadlineRaw = ReadString(root, "deadline_utc", "deadlineUtc");
            if (deadlineRaw is not null)
            {
                if (!DateTimeOffset.TryParse(
                        deadlineRaw,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsedDeadline))
                {
                    return BadRequest("deadline_utc must be ISO-8601");
                }

                deadline = parsedDeadline.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            }

            var item = new OperatorWorkItem(
                Id: 0,
                Kind: kind,
                Title: title,
                Description: ReadString(root, "description"),
                Details: ReadString(root, "details"),
                DeadlineUtc: deadline,
                Priority: priority,
                NoCheckin: ReadBool(root, "no_checkin", "noCheckin") ?? false,
                PhaseCheckpointsJson: ReadJsonText(root, "phase_checkpoints_json", "phaseCheckpointsJson"),
                DeliverablesJson: ReadJsonText(root, "deliverables_json", "deliverablesJson"),
                NeedsExtraPermissions: ReadBool(root, "needs_extra_permissions", "needsExtraPermissions") ?? false,
                OutsideCollaboration: ReadBool(root, "outside_collaboration", "outsideCollaboration") ?? false,
                Status: status,
                CreatedAt: "",
                UpdatedAt: "");

            try
            {
                var id = await store.CreateAsync(item, cancellationToken).ConfigureAwait(false);
                var row = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
                if (row is null)
                    return Results.Json(new { error = "created but not found", id }, statusCode: StatusCodes.Status500InternalServerError);
                return Results.Json(new { id, item = WorkItemDto(row) });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);

        app.MapDelete("/operator/work-items/{id:long}", async (
            long id,
            IOperatorWorkItemStore store,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return BadRequest("id must be positive");
            var removed = await store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            return removed
                ? Results.Json(new { deleted = id })
                : Results.Json(new { error = "work item not found", id }, statusCode: StatusCodes.Status404NotFound);
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);
    }

    // ── §3.2 agenda ─────────────────────────────────────────────────────────────────

    private static void MapAgenda(IEndpointRouteBuilder app)
    {
        app.MapGet("/operator/agenda", async (
            IOperatorAgendaStore store,
            CancellationToken cancellationToken) =>
        {
            var rows = await store.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Json(new { items = rows.Select(AgendaDto) });
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);

        app.MapPost("/operator/agenda", async (
            HttpRequest request,
            IOperatorAgendaStore store,
            CancellationToken cancellationToken) =>
        {
            var parsed = await ReadJsonObjectAsync(request, cancellationToken).ConfigureAwait(false);
            if (parsed.Error is not null)
                return parsed.Error;
            var root = parsed.Root;

            var title = ReadString(root, "title");
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest("title is required");

            var weekday = ReadInt(root, "weekday");
            if (weekday is null or < 0 or > 6)
                return BadRequest("weekday is required (0=Mon … 6=Sun)");

            var time = ReadString(root, "time_local", "timeLocal") ?? "09:00";
            if (!SqliteOperatorAgendaRepository.IsValidTimeLocal(time))
                return BadRequest("time_local must be HH:mm");

            try
            {
                var id = await store.CreateAsync(
                    new OperatorAgendaItem(0, title, weekday.Value, time, ReadString(root, "notes"), ""),
                    cancellationToken).ConfigureAwait(false);
                var row = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
                if (row is null)
                    return Results.Json(new { error = "created but not found", id }, statusCode: StatusCodes.Status500InternalServerError);
                return Results.Json(new { id, item = AgendaDto(row) });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);

        app.MapDelete("/operator/agenda/{id:long}", async (
            long id,
            IOperatorAgendaStore store,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return BadRequest("id must be positive");
            var removed = await store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            return removed
                ? Results.Json(new { deleted = id })
                : Results.Json(new { error = "agenda item not found", id }, statusCode: StatusCodes.Status404NotFound);
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);
    }

    // ── §3.3 inference profiles ─────────────────────────────────────────────────────

    private static void MapInferenceSettings(IEndpointRouteBuilder app)
    {
        app.MapGet("/settings/inference/profiles", async (
            IOptions<InferenceOptions> inferenceOpts,
            InferenceRuntimeSelection selection,
            IHttpClientFactory httpFactory,
            CancellationToken cancellationToken) =>
        {
            var opts = inferenceOpts.Value;
            var profiles = new List<InferenceProfile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string id, string label, string? model, bool cloud)
            {
                if (string.IsNullOrWhiteSpace(model))
                    return;
                var m = model.Trim();
                if (seen.Add($"{m}|{cloud}"))
                    profiles.Add(new InferenceProfile(id, label, m, cloud));
            }

            Add("configured", "Configured chat model", opts.Model, opts.IsCloudEndpoint);
            Add("leisure", "Leisure model", opts.LeisureModel, cloud: false);

            foreach (var tag in await TryListOllamaTagsAsync(httpFactory, cancellationToken).ConfigureAwait(false))
                Add($"local:{tag}", tag, tag, cloud: false);

            return Results.Json(new
            {
                active = ActiveDto(selection, opts),
                profiles = profiles.Select(p => new { id = p.Id, label = p.Label, model = p.Model, cloud = p.Cloud })
            });
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);

        app.MapPost("/settings/inference/apply", async (
            HttpRequest request,
            IOptions<InferenceOptions> inferenceOpts,
            InferenceRuntimeSelection selection,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var parsed = await ReadJsonObjectAsync(request, cancellationToken).ConfigureAwait(false);
            if (parsed.Error is not null)
                return parsed.Error;
            var root = parsed.Root;

            var model = ReadString(root, "model");
            if (string.IsNullOrWhiteSpace(model) || model.Length > 200 || model.Any(char.IsControl))
                return BadRequest("model is required (non-empty string, max 200 chars)");

            bool? cloud = null;
            if (root.TryGetProperty("cloud", out var cloudEl) && cloudEl.ValueKind != JsonValueKind.Null)
            {
                cloud = cloudEl.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
                if (cloud is null)
                    return BadRequest("cloud must be a boolean when provided");
            }

            selection.Apply(model, cloud);
            loggerFactory.CreateLogger("SoulCore.Host.Settings.Inference")
                .LogInformation(
                    "Inference selection applied: model={Model} cloud={Cloud}",
                    model.Trim(),
                    cloud?.ToString() ?? "(default)");

            return Results.Json(new { ok = true, active = ActiveDto(selection, inferenceOpts.Value) });
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);
    }

    // ── §3.4 host log tail ──────────────────────────────────────────────────────────

    private static void MapSystemLog(IEndpointRouteBuilder app)
    {
        app.MapGet("/settings/system-log", async (int? lines, CancellationToken cancellationToken) =>
        {
            var take = lines is > 0 ? Math.Min(lines.Value, LogTailMaxLines) : LogTailDefaultLines;
            var path = ResolveHostLogPath();
            if (path is null)
                return Results.Json(new { available = false, lines = Array.Empty<string>() });

            try
            {
                var tail = await TailFileAsync(path, take, cancellationToken).ConfigureAwait(false);
                return Results.Json(new { available = true, path, lines = tail });
            }
            catch (IOException)
            {
                return Results.Json(new { available = false, lines = Array.Empty<string>() });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { available = false, lines = Array.Empty<string>() });
            }
        }).AddEndpointFilter(WebApplicationExtensions.CompanionEmailAuthFilter);
    }

    /// <summary>Find <c>SoulCore/scripts/.soulcore-host.log</c> walking up from the app base dir and cwd.</summary>
    internal static string? ResolveHostLogPath()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            DirectoryInfo? dir;
            try
            {
                dir = new DirectoryInfo(start);
            }
            catch (ArgumentException)
            {
                continue;
            }

            for (var depth = 0; dir is not null && depth < 12; depth++, dir = dir.Parent)
            {
                foreach (var rel in new[] { Path.Combine("SoulCore", "scripts"), "scripts" })
                {
                    var candidate = Path.Combine(dir.FullName, rel, HostLogFileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }

        return null;
    }

    private static async Task<string[]> TailFileAsync(string path, int maxLines, CancellationToken cancellationToken)
    {
        // The Host process keeps writing this file — open shared, read only the tail window.
        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);

        var start = Math.Max(0, fs.Length - LogTailMaxBytes);
        fs.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var all = text.Split('\n');
        var skipFirst = start > 0 ? 1 : 0; // first line is likely cut mid-line
        var result = new List<string>(Math.Min(all.Length, maxLines));
        for (var i = skipFirst; i < all.Length; i++)
        {
            var line = all[i].TrimEnd('\r');
            if (line.Length == 0 && i == all.Length - 1)
                continue; // trailing newline
            result.Add(RedactSecrets(line));
        }

        return result.Count <= maxLines
            ? result.ToArray()
            : result.GetRange(result.Count - maxLines, maxLines).ToArray();
    }

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._\-~+/=]{8,}")]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"(?i)\b(api[_-]?key|token|secret|password|passwd)(\s*[=:]\s*)(""?)[^\s"",;]{4,}")]
    private static partial Regex KeyValuePattern();

    /// <summary>Mask obvious credentials; lines are otherwise returned verbatim.</summary>
    private static string RedactSecrets(string line)
    {
        var s = BearerPattern().Replace(line, "Bearer ***");
        return KeyValuePattern().Replace(s, "$1$2$3***");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────

    private sealed record InferenceProfile(string Id, string Label, string Model, bool Cloud);

    private static object ActiveDto(InferenceRuntimeSelection selection, InferenceOptions opts)
    {
        var eff = selection.GetEffective(opts);
        return new { model = eff.Model, cloud = eff.Cloud, leisureModel = eff.LeisureModel };
    }

    private static async Task<IReadOnlyList<string>> TryListOllamaTagsAsync(
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            using var http = httpFactory.CreateClient();
            using var resp = await http.GetAsync(OllamaTagsUrl, cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Array.Empty<string>();

            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var tags = new List<string>();
            foreach (var m in models.EnumerateArray())
            {
                if (m.ValueKind == JsonValueKind.Object
                    && m.TryGetProperty("name", out var n)
                    && n.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(n.GetString()))
                {
                    tags.Add(n.GetString()!.Trim());
                }
            }

            return tags;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            // Ollama not running / slow — profiles fall back to configured models only.
            return Array.Empty<string>();
        }
    }

    private static object WorkItemDto(OperatorWorkItem w) => new
    {
        id = w.Id,
        kind = w.Kind,
        title = w.Title,
        description = w.Description,
        details = w.Details,
        deadline_utc = w.DeadlineUtc,
        priority = w.Priority,
        no_checkin = w.NoCheckin ? 1 : 0,
        phase_checkpoints_json = w.PhaseCheckpointsJson,
        deliverables_json = w.DeliverablesJson,
        needs_extra_permissions = w.NeedsExtraPermissions ? 1 : 0,
        outside_collaboration = w.OutsideCollaboration ? 1 : 0,
        status = w.Status,
        created_at = w.CreatedAt,
        updated_at = w.UpdatedAt
    };

    private static object AgendaDto(OperatorAgendaItem a) => new
    {
        id = a.Id,
        title = a.Title,
        weekday = a.Weekday,
        time_local = a.TimeLocal,
        notes = a.Notes,
        created_at = a.CreatedAt
    };

    private static IResult BadRequest(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status400BadRequest);

    private readonly record struct JsonBody(JsonElement Root, IResult? Error);

    private static async Task<JsonBody> ReadJsonObjectAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Clone: the JsonDocument must outlive this method.
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new JsonBody(default, BadRequest("expected JSON object"));
            return new JsonBody(doc.RootElement.Clone(), null);
        }
        catch (JsonException)
        {
            return new JsonBody(default, BadRequest("invalid JSON body"));
        }
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s.Trim();
            }
        }

        return null;
    }

    private static int? ReadInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
                return n;
            if (el.ValueKind == JsonValueKind.String
                && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            {
                return s;
            }
        }

        return null;
    }

    private static bool? ReadBool(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
                continue;
            switch (el.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number when el.TryGetInt32(out var n):
                    return n != 0;
                case JsonValueKind.String:
                    var s = el.GetString();
                    if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1")
                        return true;
                    if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) || s == "0")
                        return false;
                    break;
            }
        }

        return null;
    }

    /// <summary>JSON-typed column: a string is stored as-is; an array/object is stored as its raw JSON text.</summary>
    private static string? ReadJsonText(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
                continue;
            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                    break;
                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    return el.GetRawText();
            }
        }

        return null;
    }
}
