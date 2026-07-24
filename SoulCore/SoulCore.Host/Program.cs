using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Core.Safety;
using SoulCore.Hermes;
using SoulCore.Host.Loop;
using SoulCore.Host.Ws;
using SoulCore.Inference;
using SoulCore.Memory;

// Local SoulCore/.env → process env (SOULCORE_* only) before any config bind.
// Existing non-empty process env wins; never log secret values.
DotEnvLoader.TryLoad();

// Evidence mode: confirm SecretNames keys present (length/bool only — no values).
if (args.Any(a => string.Equals(a, "--secrets-presence", StringComparison.OrdinalIgnoreCase)))
{
    return ReportSecretsPresence();
}

// Evidence mode: write emotion → dispose → reopen → verify (no web host).
if (args.Any(a => string.Equals(a, "--emotion-roundtrip", StringComparison.OrdinalIgnoreCase)))
{
    var pathArg = args.SkipWhile(a => !string.Equals(a, "--emotion-roundtrip", StringComparison.OrdinalIgnoreCase))
        .Skip(1)
        .FirstOrDefault(a => !a.StartsWith('-'));
    return await EmotionRoundTrip.RunAsync(pathArg);
}

// Evidence mode: SoulLoop tick on/off (no web host).
if (args.Any(a => string.Equals(a, "--soul-loop-tick", StringComparison.OrdinalIgnoreCase)))
{
    var enabled = args.Any(a => string.Equals(a, "--enabled", StringComparison.OrdinalIgnoreCase));
    return await SoulLoopTickEvidence.RunAsync(enabled);
}

var builder = WebApplication.CreateBuilder(args);

// User-secrets / env for secrets — never App.config tokens from quarry.
builder.Configuration.AddEnvironmentVariables(prefix: "SOULCORE_");
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

builder.Services.Configure<HostBindOptions>(
    builder.Configuration.GetSection(HostBindOptions.SectionName));
builder.Services.Configure<MemoryOptions>(
    builder.Configuration.GetSection(MemoryOptions.SectionName));
builder.Services.Configure<InferenceOptions>(
    builder.Configuration.GetSection(InferenceOptions.SectionName));
builder.Services.Configure<HermesOptions>(
    builder.Configuration.GetSection(HermesOptions.SectionName));
builder.Services.Configure<UnrealBridgeOptions>(
    builder.Configuration.GetSection(UnrealBridgeOptions.SectionName));
builder.Services.Configure<ChatWsOptions>(
    builder.Configuration.GetSection(ChatWsOptions.SectionName));
builder.Services.Configure<SoulLoopOptions>(
    builder.Configuration.GetSection(SoulLoopOptions.SectionName));
builder.Services.Configure<SafetyOptions>(
    builder.Configuration.GetSection(SafetyOptions.SectionName));

var bindOptions = builder.Configuration
    .GetSection(HostBindOptions.SectionName)
    .Get<HostBindOptions>() ?? new HostBindOptions();

var inferenceOptions = builder.Configuration
    .GetSection(InferenceOptions.SectionName)
    .Get<InferenceOptions>() ?? new InferenceOptions();

var hermesOptions = builder.Configuration
    .GetSection(HermesOptions.SectionName)
    .Get<HermesOptions>() ?? new HermesOptions();

var unrealOptions = builder.Configuration
    .GetSection(UnrealBridgeOptions.SectionName)
    .Get<UnrealBridgeOptions>() ?? new UnrealBridgeOptions();

var chatWsOptions = builder.Configuration
    .GetSection(ChatWsOptions.SectionName)
    .Get<ChatWsOptions>() ?? new ChatWsOptions();

var soulLoopOptions = builder.Configuration
    .GetSection(SoulLoopOptions.SectionName)
    .Get<SoulLoopOptions>() ?? new SoulLoopOptions();

var memoryOptions = builder.Configuration
    .GetSection(MemoryOptions.SectionName)
    .Get<MemoryOptions>() ?? new MemoryOptions();

var safetyOptions = builder.Configuration
    .GetSection(SafetyOptions.SectionName)
    .Get<SafetyOptions>() ?? new SafetyOptions();

// SEC-004: V1 bind = 127.0.0.1 only. Refuse non-loopback without explicit future SEC gate.
if (!IsLoopback(bindOptions.BindAddress))
{
    throw new InvalidOperationException(
        $"SoulCore V1 refuses non-loopback bind '{bindOptions.BindAddress}'. " +
        "Use 127.0.0.1 only (SEC-004).");
}

builder.WebHost.UseUrls($"http://{bindOptions.BindAddress}:{bindOptions.Port}");

builder.Services.AddSingleton<SqliteMemoryStore>();
builder.Services.AddSingleton<IMemoryStore>(sp => sp.GetRequiredService<SqliteMemoryStore>());
builder.Services.AddSingleton<IEmotionState>(sp => sp.GetRequiredService<SqliteMemoryStore>());

// Safety / spend layer (BED-080 libs wired by BED-082). Singletons; report-only (no act blocking yet).
builder.Services.AddSingleton<CharterService>(_ => new CharterService(memoryOptions.ResolveDbPath()));
builder.Services.AddSingleton<ICharter>(sp => sp.GetRequiredService<CharterService>());
builder.Services.AddSingleton<DriftWatcher>(_ => new DriftWatcher(safetyOptions.DriftSloMinutes));
builder.Services.AddSingleton<SpendMeter>(_ => new SpendMeter(
    safetyOptions.InputTokenRatePer1K,
    safetyOptions.OutputTokenRatePer1K,
    safetyOptions.MonthlyCapUsd));

if (inferenceOptions.Enabled)
{
    builder.Services.AddHttpClient<OllamaInferenceClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
        client.BaseAddress = NormalizeBaseUri(opts.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    });
    builder.Services.AddTransient<IInferenceClient>(sp => sp.GetRequiredService<OllamaInferenceClient>());
}
else
{
    builder.Services.AddSingleton<IInferenceClient, NullInferenceClient>();
}

if (hermesOptions.Enabled)
{
    builder.Services.AddHttpClient<HermesHttpClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<HermesOptions>>().Value;
        client.BaseAddress = NormalizeBaseUri(opts.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    });
    builder.Services.AddTransient<IHermesClient>(sp => sp.GetRequiredService<HermesHttpClient>());
}
else
{
    builder.Services.AddSingleton<IHermesClient, NullHermesClient>();
}

builder.Services.AddSingleton<PresenceWsHub>();
builder.Services.AddSingleton<IWsFrameAdapter>(sp => sp.GetRequiredService<PresenceWsHub>());
builder.Services.AddSingleton<SoulLoopScaffold>();
builder.Services.AddSingleton<ISoulLoop>(sp => sp.GetRequiredService<SoulLoopScaffold>());
builder.Services.AddHostedService<SoulLoopHostedService>();
builder.Services.AddSingleton<ChatWebSocketHandler>();

if (unrealOptions.Enabled)
{
    builder.Services.AddSingleton<UnrealVerbClientStub>();
    builder.Services.AddSingleton<IUnrealVerbClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
}
else
{
    builder.Services.AddSingleton<IUnrealVerbClient, NullUnrealVerbClient>();
}

var app = builder.Build();

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

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<ChatWebSocketHandler>();
    await handler.RunAsync(socket, context.RequestAborted);
});

app.MapGet("/health", (
    IOptions<HostBindOptions> opts,
    IMemoryStore memory,
    IUnrealVerbClient unreal,
    IOptions<UnrealBridgeOptions> unrealOpts,
    IOptions<ChatWsOptions> chatOpts,
    IOptions<SoulLoopOptions> loopOpts,
    DriftWatcher driftWatcher,
    SpendMeter spendMeter) =>
{
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
            provider = inferenceOptions.Enabled ? "ollama" : "null"
        },
        hermes = new
        {
            enabled = hermesOptions.Enabled,
            provider = hermesOptions.Enabled ? "http" : "null"
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
        }
    });
});

app.MapGet("/", () => Results.Redirect("/health"));

var logger = app.Logger;
logger.LogInformation(
    "SoulCore.Host listening on http://{Address}:{Port} (health: /health, ws: {WsPath}); memory={MemoryPath}; inference={Inference}; hermes={Hermes}; soulLoop={SoulLoop}; unreal={Unreal}",
    bindOptions.BindAddress,
    bindOptions.Port,
    wsPath,
    app.Services.GetRequiredService<IMemoryStore>().DatabasePath,
    inferenceOptions.Enabled ? "ollama" : "null",
    hermesOptions.Enabled ? "http" : "null",
    soulLoopOptions.Enabled ? "enabled" : "disabled",
    unrealOptions.Enabled ? unrealOptions.WsUrl : "disabled");

await app.Services.GetRequiredService<IWsFrameAdapter>()
    .StartAsync()
    .ConfigureAwait(false);

// Optional Unreal connect — must not crash Host if :8888 is down.
if (unrealOptions.Enabled && unrealOptions.ConnectOnStartup)
{
    try
    {
        await app.Services.GetRequiredService<IUnrealVerbClient>()
            .EnsureConnectedAsync()
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Unreal startup connect failed (ignored)");
    }
}

await app.RunAsync();
return 0;

static int ReportSecretsPresence()
{
    var keys = new[]
    {
        SecretNames.A2eApiToken,
        SecretNames.HermesApiKey,
        SecretNames.HuggingFaceToken
    };

    var envPath = DotEnvLoader.ResolveEnvFilePath();
    Console.WriteLine($"env_file_found={!string.IsNullOrEmpty(envPath)}");

    var allPresent = true;
    foreach (var key in keys)
    {
        var value = Environment.GetEnvironmentVariable(key);
        var present = !string.IsNullOrEmpty(value);
        var length = present ? value!.Length : 0;
        allPresent &= present;
        Console.WriteLine($"{key}: present={present} length={length}");
    }

    Console.WriteLine($"all_present={allPresent}");
    return allPresent ? 0 : 1;
}

static bool IsLoopback(string address) =>
    string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
    || string.Equals(address, "::1", StringComparison.OrdinalIgnoreCase);

static Uri NormalizeBaseUri(string baseUrl)
{
    var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
    return new Uri(trimmed, UriKind.Absolute);
}

static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return "/ws";
    return path.StartsWith('/') ? path : "/" + path;
}

// Expose for integration tests later
public partial class Program;
