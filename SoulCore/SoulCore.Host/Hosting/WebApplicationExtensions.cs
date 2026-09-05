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
using SoulCore.Inference;
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

            return Results.Json(new
            {
                status = memoryOk ? "ok" : "degraded",
                service = "SoulCore.Host",
                bind = opts.Value.BindAddress,
                port = opts.Value.Port,
                phase = 1,
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
                    apiKeyConfigured = !string.IsNullOrWhiteSpace(inferenceOptions.ResolveApiKey())
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
                presence = PresenceDto(presenceActivity.GetSnapshot()),
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

            return Results.Json(ToolsSettingsDto(access));
        });

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

        // FED-196 / BED-195: near-live Victoria Playwright browser (in-memory; not gallery).
        app.MapGet("/browser/view", (IVictoriaBrowserViewHub view) =>
        {
            var snap = view.GetSnapshot();
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
                note = "Victoria's dedicated Playwright Chromium (not Kurt's Chrome). In-memory stream only — not written to desktop screenshot gallery."
            });
        });

        app.MapGet("/browser/view/image", (IVictoriaBrowserViewHub view) =>
        {
            if (!view.TryGetImageBytes(out var bytes, out var contentType) || bytes is null || bytes.Length == 0)
                return Results.NotFound();
            return Results.File(bytes, contentType);
        });

        app.MapGet("/", () => Results.Redirect("/health"));

        return app;
    }

    private static object PresenceDto(SoulCore.Inference.Presence.PresenceActivitySnapshot snap) => new
    {
        currentActivity = snap.Phrase,
        activitySource = snap.Source,
        activityUpdatedAt = snap.UpdatedAt
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
            allowMt4Read = access.AllowMt4Read,
            allowMt4Trade = access.AllowMt4Trade,
            allowEmailRead = access.AllowEmailRead,
            allowEmailSend = access.AllowEmailSend,
            allowEmailDelete = access.AllowEmailDelete,
            desktopBackend = access.DesktopBackend,
            browserBackend = access.BrowserBackend,
            mt4Backend = access.Mt4Backend,
            desktopTargetWindowTitle = access.DesktopTargetWindowTitle,
            cuaDriverAvailable = cuaPath is not null,
            cuaDriverPath = cuaPath,
            scope = "session",
            note = "Session gates until Host restart. Seeded from Tools in appsettings.json (desktop/browser capture + computer control default on; email read/send/delete default off). SoftCursorRestore + DesktopBackend=cua = LLMOD-style agent cursor (blue overlay; your mouse stays put). Non-empty DesktopTargetWindowTitle hard-scopes desktop_* to that VM/window title substring. Email accounts bind from Email:Accounts (env passwords only)."
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
