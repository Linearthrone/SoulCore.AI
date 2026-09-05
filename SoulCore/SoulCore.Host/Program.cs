using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Host.Loop;
using SoulCore.Memory;
using SoulCore.Host;
using SoulCore.Host.Hosting;
using SoulCore.Host.Hosting.ServiceCollectionExtensions;

// Local SoulCore/.env → process env (SOULCORE_* only) before any config bind.
// .env overwrites stale Process/User-inherited tokens; never log secret values.
DotEnvLoader.TryLoad();

// Evidence mode: confirm SecretNames keys present (length/bool only — no values).
if (args.Any(a => string.Equals(a, "--secrets-presence", StringComparison.OrdinalIgnoreCase)))
{
    return ReportSecretsPresence();
}

// Evidence mode: VirtualBox guestcontrol logon probe (SOULCORE_VBOX_GUEST_*).
if (args.Any(a => string.Equals(a, "--guestcontrol-probe", StringComparison.OrdinalIgnoreCase)))
{
    return await GuestControlProbe.RunAsync(args);
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

// CLI: backfill missing episodic embeddings via Ollama, then exit (no web host).
if (args.Any(a => string.Equals(a, "--backfill-embeddings", StringComparison.OrdinalIgnoreCase)))
{
    return await BackfillEmbeddings.RunAsync(args);
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
builder.Services.Configure<UnrealBridgeOptions>(
    builder.Configuration.GetSection(UnrealBridgeOptions.SectionName));
builder.Services.Configure<VoiceOptions>(
    builder.Configuration.GetSection(VoiceOptions.SectionName));
builder.Services.Configure<ChatWsOptions>(
    builder.Configuration.GetSection(ChatWsOptions.SectionName));
builder.Services.Configure<SoulLoopOptions>(
    builder.Configuration.GetSection(SoulLoopOptions.SectionName));
builder.Services.Configure<CompanionOptions>(
    builder.Configuration.GetSection(CompanionOptions.SectionName));
builder.Services.Configure<SmsOptions>(
    builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<SafetyOptions>(
    builder.Configuration.GetSection(SafetyOptions.SectionName));
builder.Services.Configure<ToolsOptions>(
    builder.Configuration.GetSection(ToolsOptions.SectionName));
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

var bindOptions = builder.Configuration
    .GetSection(HostBindOptions.SectionName)
    .Get<HostBindOptions>() ?? new HostBindOptions();

var inferenceOptions = builder.Configuration
    .GetSection(InferenceOptions.SectionName)
    .Get<InferenceOptions>() ?? new InferenceOptions();

var unrealOptions = builder.Configuration
    .GetSection(UnrealBridgeOptions.SectionName)
    .Get<UnrealBridgeOptions>() ?? new UnrealBridgeOptions();

var voiceOptions = builder.Configuration
    .GetSection(VoiceOptions.SectionName)
    .Get<VoiceOptions>() ?? new VoiceOptions();

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

builder.Services
    .AddMemory(memoryOptions, safetyOptions)
    .AddInference(inferenceOptions)
    .AddTools()
    .AddCompanion(unrealOptions)
    .AddVoice(voiceOptions);

var app = builder.Build();

app.UseSoulCoreWeb(chatWsOptions);

var wsPath = string.IsNullOrWhiteSpace(chatWsOptions.Path) ? "/ws" : chatWsOptions.Path;
if (!wsPath.StartsWith('/'))
    wsPath = "/" + wsPath;

var logger = app.Logger;
logger.LogInformation(
    "SoulCore.Host listening on http://{Address}:{Port} (health: /health, ws: {WsPath}); memory={MemoryPath}; inference={Inference}; soulLoop={SoulLoop}; unreal={Unreal}",
    bindOptions.BindAddress,
    bindOptions.Port,
    wsPath,
    app.Services.GetRequiredService<IMemoryStore>().DatabasePath,
    inferenceOptions.Enabled ? "ollama" : "null",
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
        SecretNames.HuggingFaceToken,
        SecretNames.CompanionApiToken,
        SecretNames.OllamaApiKey,
        SecretNames.VboxGuestPass
    };

    // Load .env the same way Host startup does so this report matches live auth.
    DotEnvLoader.TryLoad();

    var envPath = DotEnvLoader.ResolveEnvFilePath();
    Console.WriteLine($"env_file_found={!string.IsNullOrEmpty(envPath)}");
    if (!string.IsNullOrEmpty(envPath))
        Console.WriteLine($"env_file_path={envPath}");

    var allPresent = true;
    foreach (var key in keys)
    {
        var value = Environment.GetEnvironmentVariable(key);
        var present = !string.IsNullOrEmpty(value);
        var length = present ? value!.Length : 0;
        allPresent &= present;
        // Fingerprint only — never print the secret. Lets Kurt compare .env vs Host.
        var fp = present ? ShortFingerprint(value!) : "-";
        Console.WriteLine($"{key}: present={present} length={length} fp={fp}");
    }

    Console.WriteLine($"all_present={allPresent}");
    return allPresent ? 0 : 1;
}

static string ShortFingerprint(string secret)
{
    var hash = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(secret));
    return Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
}

static bool IsLoopback(string address) =>
    string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
    || string.Equals(address, "::1", StringComparison.OrdinalIgnoreCase);

// Expose for integration tests later
public partial class Program;
