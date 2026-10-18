using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Core.Abstractions;
using SoulCore.Memory;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Core.Charter;
using SoulCore.Core.Safety;
using SoulCore.Host.Companion;
using SoulCore.Host.Voice;
using SoulCore.Host.Ws;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using System.Text.Json;

namespace SoulCore.Host.Hosting;

internal static class WebApplicationExtensions
{
    internal static WebApplication UseSoulCoreWeb(this WebApplication app, ChatWsOptions chatWsOptions)
    {
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        var wsPath = string.IsNullOrWhiteSpace(chatWsOptions.Path) ? "/ws" : chatWsOptions.Path;
        if (!wsPath.StartsWith('/'))
            wsPath = "/" + wsPath;

        app.Map(wsPath, async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected WebSocket upgrade. Use ws://127.0.0.1:7700/ws");
                return;
            }

            // BED-155 / SEC-152: fail-closed companion token when SOULCORE_COMPANION_API_TOKEN is set.
            // Accept Authorization: Bearer <token> or X-Api-Key: <token>. Never log secret values.
            var companionToken = CompanionWsAuth.ResolveConfiguredToken(context.RequestServices.GetService<IConfiguration>());
            var authOutcome = CompanionWsAuth.Evaluate(context.Request, companionToken);
            if (authOutcome is CompanionWsAuth.AuthOutcome.Missing or CompanionWsAuth.AuthOutcome.Invalid)
            {
                var wsAuthLogger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("SoulCore.Host.Ws.CompanionAuth");
                var headerSource = CompanionWsAuth.DescribeHeaderSource(context.Request);
                wsAuthLogger.LogWarning(
                    "WS upgrade rejected: companion auth failed ({Safe})",
                    CompanionWsAuth.FormatLogSafe(authOutcome, headerSource));
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var handler = context.RequestServices.GetRequiredService<ChatWebSocketHandler>();
            await handler.RunAsync(socket, context.RequestAborted);
        });

        app.MapCompanionApi();
        app.MapVoiceApi();

        app.MapGet("/health", async (
            IOptions<HostBindOptions> opts,
            IOptions<SmsOptions> smsOpts,
            IOptions<InferenceOptions> inferenceOpts,
            IMemoryStore memory,
            IUnrealVerbClient unreal,
            IOptions<UnrealBridgeOptions> unrealOpts,
            IOptions<ChatWsOptions> chatOpts,
            IOptions<SoulLoopOptions> loopOpts,
            IToolsAccessSettings access,
            DriftWatcher driftWatcher,
            SpendMeter spendMeter,
            CharterService charter,
            SoulCore.Inference.Presence.IPresenceActivityHub presenceActivity,
            SoulCore.Inference.Presence.ILeisureSession leisure,
            IVictoriaTaskStore victoriaTasks,
            SoulCore.Inference.Presence.IAssignedWorkStore assignedWork,
            CancellationToken cancellationToken) =>
        {
            var inferenceOptions = inferenceOpts.Value;
            var embeddingsOn = inferenceOptions.Enabled && inferenceOptions.EmbeddingsEnabled;
            var memoryOk = memory.IsDatabaseOpen;

            DriftStatus driftStatus;
            try
            {
                driftStatus = driftWatcher.GetStatus();
            }
            catch (Exception)
            {
                driftStatus = new DriftStatus(null, 0, false, null);
            }

            var oldestDriftMinutes = driftStatus.OldestDriftReport is null
                ? 0
                : Math.Max(0, (int)Math.Round((DateTimeOffset.UtcNow - driftStatus.OldestDriftReport.ObservedAt).TotalMinutes));

            SpendSummary spendSummary;
            try
            {
                spendSummary = spendMeter.GetSummary();
            }
            catch (Exception)
            {
                spendSummary = new SpendSummary(0, 0, 0m, 0m, false);
            }

            int charterTotal = 0, charterLocked = 0;
            try
            {
                (charterTotal, charterLocked) = await charter.GetLockCountsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // health stays up even if charter query fails
            }

            var charterFullyLocked = charterTotal > 0 && charterLocked == charterTotal;

            var taskCount = 0;
            long? runningTaskId = null;
            try
            {
                var ledger = await victoriaTasks.ListAsync(null, cancellationToken).ConfigureAwait(false);
                taskCount = ledger.Count;
                runningTaskId = assignedWork.RunningId;
            }
            catch (Exception)
            {
                // health stays up even if the ledger query fails
            }

            return Results.Json(new
            {
                status = memoryOk ? "ok" : "degraded",
                service = "SoulCore.Host",
                version = HostBuildVersion.Current,
                bind = opts.Value.BindAddress,
                port = opts.Value.Port,
                phase = 1,
                tasks = new
                {
                    count = taskCount,
                    runningId = runningTaskId
                },
                ws = new
                {
                    path = chatOpts.Value.Path,
                    url = $"ws://{opts.Value.BindAddress}:{opts.Value.Port}{NormalizePath(chatOpts.Value.Path)}"
                },
                memory = new
                {
                    open = memoryOk,
                    path = memory.DatabasePath
                },
                inference = new
                {
                    enabled = inferenceOptions.Enabled,
                    provider = inferenceOptions.Enabled
                        ? (inferenceOptions.IsCloudEndpoint ? "ollama-cloud" : "ollama")
                        : "null",
                    // BED-01 / TASK-157: expose configured chat model for QA/ops (no secrets).
                    model = inferenceOptions.Model,
                    cloud = inferenceOptions.IsCloudEndpoint,
                    baseUrl = inferenceOptions.IsCloudEndpoint ? InferenceOptions.CloudBaseUrl : "loopback",
                    embeddingsEnabled = embeddingsOn,
                    embeddingModel = inferenceOptions.EmbeddingModel,
                    embeddingBaseUrl = embeddingsOn
                        ? (InferenceOptions.IsOllamaCloudUrl(inferenceOptions.ResolveEmbeddingBaseUrl())
                            ? InferenceOptions.CloudBaseUrl
                            : "loopback")
                        : null,
                    apiKeyConfigured = !string.IsNullOrWhiteSpace(inferenceOptions.ResolveApiKey()),
                    visionSidecar = inferenceOptions.IsVisionSidecarConfigured,
                    visionModel = string.IsNullOrWhiteSpace(inferenceOptions.VisionModel)
                        ? null
                        : inferenceOptions.VisionModel.Trim(),
                    visionApiKeyConfigured = !string.IsNullOrWhiteSpace(inferenceOptions.ResolveVisionApiKey())
                },
                soulLoop = new
                {
                    enabled = loopOpts.Value.Enabled,
                    tickIntervalSeconds = loopOpts.Value.TickIntervalSeconds
                },
                unreal = new
                {
                    enabled = unrealOpts.Value.Enabled,
                    target = unreal.TargetUrl,
                    connected = unreal.IsConnected
                },
                // PROP-4 BED: Presence HUD activity — short doing-now line (never loop.want slogans).
                presence = PresenceDto(presenceActivity.GetSnapshot(), leisure),
                tools = ToolsSettingsDto(access),
                charter = new
                {
                    anchors = charterTotal,
                    locked = charterLocked,
                    fullyLocked = charterFullyLocked,
                    mode = charterFullyLocked ? "locked" : (charterTotal == 0 ? "empty" : "calibration")
                },
                safety = new
                {
                    drift = new
                    {
                        activeDriftCount = driftStatus.UnackedReports,
                        sloExceeded = driftStatus.SloExceeded,
                        oldestDriftMinutes
                    },
                    spend = new
                    {
                        totalTokensIn = spendSummary.TotalTokensIn,
                        totalTokensOut = spendSummary.TotalTokensOut,
                        estimatedCostUsd = spendSummary.EstimatedCost,
                        monthlyCapUsd = spendSummary.MonthlyCap,
                        capExceeded = spendSummary.CapExceeded
                    }
                },
                // PROP-1.4: SMS gateway status — bool/length only (no MDNs/tokens).
                sms = SmsHealthSnapshot.Build(smsOpts.Value)
            });
        });

        // Operator ledger (Presence). Victoria also has task_* tools including task_delete.
        app.MapGet("/tasks", async (IVictoriaTaskStore tasks, CancellationToken cancellationToken) =>
        {
            var rows = await tasks.ListAsync(null, cancellationToken).ConfigureAwait(false);
            return Results.Json(new
            {
                count = rows.Count,
                tasks = rows.Select(TaskLedgerDto)
            });
        });

        app.MapPost("/tasks", async (
            HttpRequest request,
            IVictoriaTaskStore tasks,
            CancellationToken cancellationToken) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Results.Json(new { error = "expected JSON object" }, statusCode: StatusCodes.Status400BadRequest);

            if (!root.TryGetProperty("title", out var titleEl) || titleEl.ValueKind != JsonValueKind.String)
                return Results.Json(new { error = "title is required" }, statusCode: StatusCodes.Status400BadRequest);

            var title = titleEl.GetString();
            if (string.IsNullOrWhiteSpace(title))
                return Results.Json(new { error = "title must be non-empty" }, statusCode: StatusCodes.Status400BadRequest);

            string? description = ReadOptionalString(root, "description");
            string? priority = ReadOptionalString(root, "priority");
            string? surface = ReadOptionalString(root, "surface");
            string? status = ReadOptionalString(root, "status");
            string? notes = ReadOptionalString(root, "notes");
            var ready = false;
            if (root.TryGetProperty("ready", out var readyEl))
            {
                ready = readyEl.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.String => string.Equals(readyEl.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }

            try
            {
                var id = await tasks.CreateAsync(
                    title.Trim(),
                    description,
                    priority,
                    surface,
                    ready,
                    status,
                    notes,
                    cancellationToken).ConfigureAwait(false);
                var row = await tasks.GetAsync(id, cancellationToken).ConfigureAwait(false);
                if (row is null)
                    return Results.Json(new { error = "created but not found", id }, statusCode: StatusCodes.Status500InternalServerError);
                return Results.Json(new { id, task = TaskLedgerDto(row) });
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/tasks/{id:long}/suspend", async (
            long id,
            HttpRequest request,
            IVictoriaTaskStore tasks,
            SoulCore.Inference.Presence.IAssignedWorkStore assigned,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return Results.Json(new { error = "id must be positive" }, statusCode: StatusCodes.Status400BadRequest);

            string? notes = null;
            if (request.ContentLength is > 0)
            {
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    notes = ReadOptionalString(doc.RootElement, "notes");
            }

            var ok = await tasks.SuspendAsync(id, notes, cancellationToken).ConfigureAwait(false);
            if (!ok)
                return Results.Json(new { error = "task not found", id }, statusCode: StatusCodes.Status404NotFound);

            if (assigned.RunningId == id)
                assigned.Clear();

            var row = await tasks.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Json(new { id, status = "suspended", task = row is null ? null : TaskLedgerDto(row) });
        });

        app.MapPost("/tasks/{id:long}/release", async (
            long id,
            IVictoriaTaskStore tasks,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return Results.Json(new { error = "id must be positive" }, statusCode: StatusCodes.Status400BadRequest);

            var ok = await tasks.ReleaseAsync(id, cancellationToken).ConfigureAwait(false);
            if (!ok)
                return Results.Json(new { error = "task not found", id }, statusCode: StatusCodes.Status404NotFound);

            var row = await tasks.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Json(new { id, status = "todo", task = row is null ? null : TaskLedgerDto(row) });
        });

        app.MapDelete("/tasks/{id:long}", async (
            long id,
            IVictoriaTaskStore tasks,
            SoulCore.Inference.Presence.IAssignedWorkStore assigned,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return Results.Json(new { error = "id must be positive" }, statusCode: StatusCodes.Status400BadRequest);

            var removed = await tasks.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            if (!removed)
                return Results.Json(new { error = "task not found", id }, statusCode: StatusCodes.Status404NotFound);

            if (assigned.RunningId == id)
                assigned.Clear();

            return Results.Json(new { deleted = id });
        });

        // Research / hobby subject journals (APA papers). Read-only for Presence.
        app.MapGet("/research/subjects", async (
            IVictoriaResearchStore research,
            CancellationToken cancellationToken) =>
        {
            var rows = await research.ListSubjectsAsync(cancellationToken).ConfigureAwait(false);
            return Results.Json(new
            {
                count = rows.Count,
                subjects = rows.Select(s => new
                {
                    id = s.Id,
                    slug = s.Slug,
                    title = s.Title,
                    summary = s.Summary,
                    paperCount = s.PaperCount,
                    updatedAt = s.UpdatedAt
                })
            });
        });

        app.MapGet("/research/subjects/{id:long}/papers", async (
            long id,
            IVictoriaResearchStore research,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return Results.Json(new { error = "id must be positive" }, statusCode: StatusCodes.Status400BadRequest);
            var subject = await research.GetSubjectAsync(id, cancellationToken).ConfigureAwait(false);
            if (subject is null)
                return Results.Json(new { error = "subject not found", id }, statusCode: StatusCodes.Status404NotFound);
            var papers = await research.ListPapersAsync(id, 100, cancellationToken).ConfigureAwait(false);
            return Results.Json(new
            {
                subject = new
                {
                    id = subject.Id,
                    title = subject.Title,
                    summary = subject.Summary,
                    paperCount = subject.PaperCount
                },
                count = papers.Count,
                papers = papers.Select(ResearchPaperDto)
            });
        });

        app.MapGet("/research/papers/{id:long}", async (
            long id,
            IVictoriaResearchStore research,
            CancellationToken cancellationToken) =>
        {
            if (id <= 0)
                return Results.Json(new { error = "id must be positive" }, statusCode: StatusCodes.Status400BadRequest);
            var paper = await research.GetPaperAsync(id, cancellationToken).ConfigureAwait(false);
            if (paper is null)
                return Results.Json(new { error = "paper not found", id }, statusCode: StatusCodes.Status404NotFound);
            return Results.Json(new { paper = ResearchPaperDto(paper) });
        });

        app.MapPost("/health/drift/ack", (DriftWatcher driftWatcher) =>
        {
            var acked = driftWatcher.AcknowledgeAll();
            return Results.Json(new { acked });
        });

        app.MapGet("/settings/tools", (IToolsAccessSettings access) => Results.Json(ToolsSettingsDto(access)));

        app.MapPost("/settings/tools", async (HttpRequest request, IToolsAccessSettings access) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Results.BadRequest(new { error = "expected JSON object" });

            static bool? ReadBool(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var p))
                    return null;
                return p.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }

            if (ReadBool(root, "allowDesktopCapture") is { } deskCap)
                access.SetAllowDesktopCapture(deskCap);
            if (ReadBool(root, "allowBrowserCapture") is { } browserCap)
                access.SetAllowBrowserCapture(browserCap);
            if (ReadBool(root, "allowComputerControl") is { } control)
                access.SetAllowComputerControl(control);
            if (ReadBool(root, "softCursorRestore") is { } soft)
                access.SetSoftCursorRestore(soft);
            if (ReadBool(root, "vmEmbedPane") is { } vmEmbed)
            {
                access.SetVmEmbedPane(vmEmbed);
                if (!vmEmbed)
                    VictoriaVirtualBoxWindowLocator.ClearCache();
            }
            if (ReadBool(root, "allowMt4Read") is { } mt4Read)
                access.SetAllowMt4Read(mt4Read);
            if (ReadBool(root, "allowMt4Trade") is { } mt4Trade)
                access.SetAllowMt4Trade(mt4Trade);
            if (ReadBool(root, "allowEmailRead") is { } emailRead)
                access.SetAllowEmailRead(emailRead);
            if (ReadBool(root, "allowEmailSend") is { } emailSend)
                access.SetAllowEmailSend(emailSend);
            if (ReadBool(root, "allowEmailDelete") is { } emailDelete)
                access.SetAllowEmailDelete(emailDelete);
            if (ReadBool(root, "allowGuestSudo") is { } guestSudo)
                access.SetAllowGuestSudo(guestSudo);

            return Results.Json(ToolsSettingsDto(access));
        }).AddEndpointFilter(CompanionEmailAuthFilter);

        // PROP-17.1: host-side consent grant (companion token). Model cannot mint confirmed.
        app.MapPost("/settings/consent", async (
            HttpRequest request,
            SoulCore.Inference.Presence.IOperatorConsentGate consent) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("scope", out var scopeEl)
                || scopeEl.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(scopeEl.GetString()))
            {
                return Results.BadRequest(new { error = "expected { scope: email_send|email_delete|mt4_write }" });
            }

            var scope = scopeEl.GetString()!.Trim().ToLowerInvariant();
            consent.Grant(scope);
            return Results.Json(new { ok = true, scope });
        }).AddEndpointFilter(CompanionEmailAuthFilter);

        // Email account credentials (Presence Settings + companion). Passwords never echoed.
        // Auth mirrors companion API when SOULCORE_COMPANION_API_TOKEN is set.
        app.MapGet("/settings/email", (SoulCore.Inference.Tools.Email.IEmailAccountStore store) =>
        {
            var accounts = store.ListAccounts().Select(store.ToPublicDto).ToArray();
            return Results.Json(new
            {
                accounts,
                note = "Passwords are write-only. Leave password blank to keep the current secret. Runtime overrides live under %LOCALAPPDATA%/SoulCore/email-accounts.runtime.json."
            });
        }).AddEndpointFilter(CompanionEmailAuthFilter);

        app.MapPost("/settings/email", async (
            HttpRequest request,
            SoulCore.Inference.Tools.Email.IEmailAccountStore store) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Results.BadRequest(new { error = "expected JSON object" });

            static string? ReadString(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
                    return null;
                return p.GetString();
            }

            static int? ReadInt(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var p))
                    return null;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
                    return n;
                if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s))
                    return s;
                return null;
            }

            static bool? ReadBool(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var p))
                    return null;
                return p.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }

            var id = ReadString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest(new { error = "id required (victoria | personal | business)" });

            try
            {
                var updated = store.Upsert(new SoulCore.Inference.Tools.Email.EmailAccountWriteRequest
                {
                    Id = id,
                    Role = ReadString(root, "role"),
                    DisplayName = ReadString(root, "displayName"),
                    Address = ReadString(root, "address"),
                    ImapHost = ReadString(root, "imapHost"),
                    ImapPort = ReadInt(root, "imapPort"),
                    ImapUseSsl = ReadBool(root, "imapUseSsl"),
                    SmtpHost = ReadString(root, "smtpHost"),
                    SmtpPort = ReadInt(root, "smtpPort"),
                    SmtpUseSsl = ReadBool(root, "smtpUseSsl"),
                    Username = ReadString(root, "username"),
                    Password = ReadString(root, "password"),
                    Enabled = ReadBool(root, "enabled")
                });

                return Results.Json(new
                {
                    ok = true,
                    account = store.ToPublicDto(updated)
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).AddEndpointFilter(CompanionEmailAuthFilter);

        // TASK-177: Identity tab payload — Companion display name + charter anchor details
        // (read-only from CharterService; no fabricated biography).
        app.MapGet("/settings/identity", async (
            IOptions<CompanionOptions> companionOpts,
            CharterService charter,
            CancellationToken cancellationToken) =>
        {
            var companion = companionOpts.Value ?? new CompanionOptions();
            int charterTotal = 0, charterLocked = 0;
            IReadOnlyList<CharterAnchorInfo> identityAnchors = Array.Empty<CharterAnchorInfo>();
            IReadOnlyList<CharterAnchorInfo> allAnchors = Array.Empty<CharterAnchorInfo>();
            try
            {
                (charterTotal, charterLocked) = await charter.GetLockCountsAsync(cancellationToken).ConfigureAwait(false);
                identityAnchors = await charter.ListAnchorDetailsAsync("identity", cancellationToken).ConfigureAwait(false);
                allAnchors = await charter.ListAnchorDetailsAsync(kind: null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Return name + empty anchors rather than failing the Settings tab.
            }

            var fullyLocked = charterTotal > 0 && charterLocked == charterTotal;
            static object AnchorDto(CharterAnchorInfo a) => new
            {
                id = a.Id,
                kind = a.Kind,
                title = a.Title,
                body = a.Body,
                priority = a.Priority,
                isLocked = a.IsLocked,
                source = a.Source
            };

            return Results.Json(new
            {
                displayName = companion.DefaultContactName,
                contactId = companion.DefaultContactId,
                charter = new
                {
                    anchors = charterTotal,
                    locked = charterLocked,
                    fullyLocked,
                    mode = fullyLocked ? "locked" : (charterTotal == 0 ? "empty" : "calibration")
                },
                identityAnchors = identityAnchors.Select(AnchorDto).ToArray(),
                anchors = allAnchors.Select(AnchorDto).ToArray(),
                note = "Read-only charter/identity anchors from SoulCore SQLite. Display name from Companion options (Victoria)."
            });
        });

        app.MapGet("/desktop/view", (IDesktopViewHub view) =>
        {
            var snap = view.GetSnapshot();
            var recent = (snap.Recent ?? Array.Empty<DesktopViewGalleryEntry>())
                .Select(r => new
                {
                    fileName = r.FileName,
                    path = r.Path,
                    source = r.Source,
                    format = r.Format,
                    width = r.Width,
                    height = r.Height,
                    capturedAt = r.CapturedAt,
                    action = r.Action,
                    imageUrl = "/desktop/view/gallery/" + Uri.EscapeDataString(r.FileName)
                })
                .ToArray();
            return Results.Json(new
            {
                hasImage = snap.HasImage,
                imagePath = "/desktop/view/image",
                diskPath = snap.ImagePath,
                galleryDir = snap.GalleryDir ?? view.GalleryDirectory,
                format = snap.Format,
                width = snap.Width,
                height = snap.Height,
                cursorX = snap.CursorX,
                cursorY = snap.CursorY,
                lastAction = snap.LastAction,
                updatedAt = snap.UpdatedAt,
                softCursorRestore = snap.SoftCursorRestore,
                source = snap.Source,
                recent,
                note = "Last image Victoria actually captured (source=desktop|eyes|browser). Every capture is also written under galleryDir (temp ring buffer). Open diskPath / recent[].path on this machine."
            });
        });

        app.MapGet("/desktop/view/image", (IDesktopViewHub view) =>
        {
            var bytes = view.TryGetImageBytes();
            if (bytes is null || bytes.Length == 0)
                return Results.NotFound();

            var snap = view.GetSnapshot();
            var contentType = string.Equals(snap.Format, "png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/bmp";
            return Results.File(bytes, contentType);
        });

        // BED-186: serve a gallery frame by basename (loopback Presence UI).
        app.MapGet("/desktop/view/gallery/{fileName}", (string fileName, IDesktopViewHub view) =>
        {
            var bytes = view.TryGetGalleryImageBytes(fileName);
            if (bytes is null || bytes.Length == 0)
                return Results.NotFound();

            var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            var contentType = ext switch
            {
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "webp" => "image/webp",
                _ => "image/bmp"
            };
            return Results.File(bytes, contentType);
        });

        // FED-196: near-live Victoria screen (Playwright Chromium or VirtualBox guest / HWND embed).
        app.MapGet("/browser/view", (IVictoriaBrowserViewHub view, IOptions<ToolsOptions> tools, IToolsAccessSettings access) =>
        {
            var snap = view.GetSnapshot();
            var opts = tools.Value;
            // Session VmEmbedPane (Presence Settings) wins over appsettings seed.
            var vmEmbed = access.VmEmbedPane;
            var playwrightEmbed = !vmEmbed && opts.PlaywrightEmbedPane;
            var embed = vmEmbed || playwrightEmbed;
            var embedSurface = vmEmbed ? "vm" : playwrightEmbed ? "playwright" : "none";
            var softCursor = VictoriaBrowserViewHub.WantsPresenceSoftCursor(snap.Backend, embedSurface);
            return Results.Json(new
            {
                hasImage = snap.HasImage,
                imagePath = "/browser/view/image",
                url = snap.Url,
                title = snap.Title,
                lastAction = snap.LastAction,
                waitingOnYou = snap.WaitingOnYou,
                backend = snap.Backend,
                updatedAt = snap.UpdatedUtc,
                embedPane = embed,
                embedSurface,
                cursorX = softCursor ? snap.CursorX : null,
                cursorY = softCursor ? snap.CursorY : null,
                cursorState = softCursor ? snap.CursorState : null,
                cursorAt = softCursor ? snap.CursorAt : null,
                frameWidth = snap.FrameWidth,
                frameHeight = snap.FrameHeight,
                note = vmEmbed
                    ? "Her screen prefers VirtualBox HWND via GET /browser/embed (surface=vm). Soft cursor pink→teal on desktop_click/move. JPEG is fallback."
                    : snap.Backend == VictoriaBrowserViewHub.BackendVboxGuest
                        ? "VirtualBox guest framebuffer (victoria-sandbox). Hover coords = guest origin 0,0 for desktop_click. In-memory only."
                        : embed
                            ? "PROP-14: Prefer HWND embed via GET /browser/embed; JPEG is fallback only."
                            : "Victoria's dedicated Playwright Chromium (not Kayleigh's Chrome). In-memory stream only — not written to desktop screenshot gallery."
            });
        });

        app.MapGet("/browser/view/image", (IVictoriaBrowserViewHub view) =>
        {
            if (!view.TryGetImageBytes(out var bytes, out var contentType) || bytes is null || bytes.Length == 0)
                return Results.NotFound();
            return Results.File(bytes, contentType);
        });

        // PROP-14 / VM embed: Windows desk — expose Chromium or VirtualBox HWND for Presence SetParent.
        app.MapGet("/browser/embed", async (
            IBrowserBridge bridge,
            IOptions<ToolsOptions> tools,
            IToolsAccessSettings access,
            CancellationToken ct) =>
        {
            var opts = tools.Value;
            var vmEmbed = access.VmEmbedPane;
            var playwrightEmbed = !vmEmbed && opts.PlaywrightEmbedPane;
            if (!vmEmbed && !playwrightEmbed)
            {
                VictoriaVirtualBoxWindowLocator.ClearCache();
                return Results.Json(new
                {
                    mode = "disabled",
                    surface = "none",
                    hwnd = 0,
                    pid = 0,
                    title = (string?)null,
                    detail = "VM embed is off. In Presence: Settings -> Tools & Access -> Embed VirtualBox in Her screen."
                });
            }

            if (!OperatingSystem.IsWindows())
            {
                return Results.Json(new
                {
                    mode = "fallback",
                    surface = vmEmbed ? "vm" : "playwright",
                    hwnd = 0,
                    pid = 0,
                    title = (string?)null,
                    detail = "HWND embed is Windows-only. Use JPEG /browser/view fallback."
                });
            }

            if (!BrowserToolGate.IsCaptureAllowed(access))
            {
                return Results.Json(new
                {
                    mode = "capture_off",
                    surface = vmEmbed ? "vm" : "playwright",
                    hwnd = 0,
                    pid = 0,
                    title = (string?)null,
                    detail = "AllowBrowserCapture is false — refuse embed."
                });
            }

            // VM embed wins when enabled — Her screen shows victoria-sandbox VirtualBox window.
            if (vmEmbed)
            {
                var titleFilter = (opts.DesktopTargetWindowTitle ?? "").Trim();
                if (string.IsNullOrWhiteSpace(titleFilter))
                    titleFilter = "victoria-sandbox";

                // TryFind caches HWND: after Presence SetParent the window is no longer
                // top-level, so EnumWindows alone would miss it and Presence would tear down.
                var vm = VictoriaVirtualBoxWindowLocator.TryFind(titleFilter);
                if (vm is null)
                {
                    return Results.Json(new
                    {
                        mode = "fallback",
                        surface = "vm",
                        hwnd = 0,
                        pid = 0,
                        title = (string?)null,
                        detail = $"No VirtualBox window matching '{titleFilter}' yet - start victoria-sandbox (visible, not minimized) or use JPEG fallback."
                    });
                }

                return Results.Json(new
                {
                    mode = "embedded",
                    surface = "vm",
                    hwnd = vm.Hwnd.ToInt64(),
                    pid = vm.Pid,
                    title = vm.Title,
                    exePath = vm.ExePath,
                    detail = "Presence should SetParent this VirtualBox HWND into Her screen (no floating twin)."
                });
            }

            VictoriaVirtualBoxWindowLocator.ClearCache();

            // Ensure headed Chromium is up so a window exists to reparent.
            if (string.Equals(bridge.BackendName, PlaywrightBrowserBridge.BackendId, StringComparison.OrdinalIgnoreCase))
            {
                var health = await bridge.HealthAsync(ct).ConfigureAwait(false);
                if (!health.Success)
                {
                    return Results.Json(new
                    {
                        mode = "fallback",
                        surface = "playwright",
                        hwnd = 0,
                        pid = 0,
                        title = (string?)null,
                        detail = health.Content
                    });
                }
            }

            var found = VictoriaChromiumWindowLocator.TryFind();
            if (found is null)
            {
                return Results.Json(new
                {
                    mode = "fallback",
                    surface = "playwright",
                    hwnd = 0,
                    pid = 0,
                    title = (string?)null,
                    detail = "No ms-playwright Chromium HWND yet — JPEG fallback. Ensure PlaywrightHeaded/EmbedPane and a live page."
                });
            }

            return Results.Json(new
            {
                mode = "embedded",
                surface = "playwright",
                hwnd = found.Hwnd.ToInt64(),
                pid = found.Pid,
                title = found.Title,
                exePath = found.ExePath,
                detail = "Presence should SetParent this HWND into Her screen (no floating twin)."
            });
        });

        app.MapGet("/", () => Results.Redirect("/health"));

        return app;
    }

    private static object TaskLedgerDto(VictoriaTask t) => new
    {
        id = t.Id,
        title = t.Title,
        status = t.Status,
        surface = t.Surface,
        priority = t.Priority,
        notes = t.Notes,
        parkReason = t.ParkReason,
        parkUntil = t.ParkUntil
    };

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        var s = el.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static object ResearchPaperDto(VictoriaResearchPaper p) => new
    {
        id = p.Id,
        subjectId = p.SubjectId,
        kind = p.Kind,
        title = p.Title,
        abstractText = p.Abstract,
        bodyApa = p.BodyApa,
        bibliographyApa = p.BibliographyApa,
        status = p.Status,
        updatedAt = p.UpdatedAt
    };

    private static object PresenceDto(
        SoulCore.Inference.Presence.PresenceActivitySnapshot snap,
        SoulCore.Inference.Presence.ILeisureSession leisure) => new
    {
        currentActivity = snap.Phrase,
        activitySource = snap.Source,
        activityUpdatedAt = snap.UpdatedAt,
        onHerMind = leisure.ActivityLine()
    };

    private static object ToolsSettingsDto(IToolsAccessSettings access)
    {
        var cuaPath = CuaDriverCli.TryFindExe();
        return new
        {
            allowDesktopCapture = access.AllowDesktopCapture,
            allowBrowserCapture = access.AllowBrowserCapture,
            allowComputerControl = access.AllowComputerControl,
            softCursorRestore = access.SoftCursorRestore,
            vmEmbedPane = access.VmEmbedPane,
            allowMt4Read = access.AllowMt4Read,
            allowMt4Trade = access.AllowMt4Trade,
            allowEmailRead = access.AllowEmailRead,
            allowEmailSend = access.AllowEmailSend,
            allowEmailDelete = access.AllowEmailDelete,
            allowGuestSudo = access.AllowGuestSudo,
            desktopBackend = access.DesktopBackend,
            browserBackend = access.BrowserBackend,
            mt4Backend = access.Mt4Backend,
            desktopTargetWindowTitle = access.DesktopTargetWindowTitle,
            cuaDriverAvailable = cuaPath is not null,
            cuaDriverPath = cuaPath,
            scope = "session",
            note = "Session gates until Host restart. Seeded from Tools in appsettings.json. Presence Settings → Tools & Access → Embed VirtualBox in Her screen toggles VmEmbedPane (victoria-sandbox HWND)."
        };
    }

    private static async ValueTask<object?> CompanionEmailAuthFilter(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var config = http.RequestServices.GetService<IConfiguration>();
        var token = CompanionWsAuth.ResolveConfiguredToken(config);
        var outcome = CompanionWsAuth.Evaluate(http.Request, token);
        if (outcome is CompanionWsAuth.AuthOutcome.Missing or CompanionWsAuth.AuthOutcome.Invalid)
        {
            var logger = http.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SoulCore.Host.Settings.Email.Auth");
            logger.LogWarning(
                "Email settings rejected ({Safe})",
                CompanionWsAuth.FormatLogSafe(outcome, CompanionWsAuth.DescribeHeaderSource(http.Request)));
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/ws";
        return path.StartsWith('/') ? path : "/" + path;
    }
}
