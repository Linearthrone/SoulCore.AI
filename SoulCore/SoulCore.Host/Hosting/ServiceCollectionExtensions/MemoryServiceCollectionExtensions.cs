using Microsoft.Extensions.DependencyInjection;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Core.Safety;
using SoulCore.Inference.Tools;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class MemoryServiceCollectionExtensions
{
    internal static IServiceCollection AddMemory(
        this IServiceCollection services,
        MemoryOptions memoryOptions,
        SafetyOptions safetyOptions)
    {
        // PROP-11.1: one SQLite session + focused repos behind existing interfaces.
        services.AddSingleton<SqliteMemorySession>();
        services.AddSingleton<SqliteEpisodicMemoryRepository>();
        services.AddSingleton<IMemoryStore>(sp => sp.GetRequiredService<SqliteEpisodicMemoryRepository>());
        services.AddSingleton<SqliteEmotionRepository>();
        services.AddSingleton<IEmotionState>(sp => sp.GetRequiredService<SqliteEmotionRepository>());
        services.AddSingleton<SqliteVictoriaTaskRepository>();
        services.AddSingleton<IVictoriaTaskStore>(sp => sp.GetRequiredService<SqliteVictoriaTaskRepository>());
        services.AddSingleton<SqliteVictoriaWorkflowRepository>();
        services.AddSingleton<IVictoriaWorkflowStore>(sp => sp.GetRequiredService<SqliteVictoriaWorkflowRepository>());
        services.AddSingleton<SqliteVictoriaJournalRepository>();
        services.AddSingleton<IVictoriaJournalStore>(sp => sp.GetRequiredService<SqliteVictoriaJournalRepository>());
        services.AddSingleton<SqliteMemoryStore>(sp => new SqliteMemoryStore(sp.GetRequiredService<SqliteMemorySession>()));

        // Safety / spend layer (BED-080 libs wired by BED-082; TASK-102 hard gate on CapExceeded).
        // PROP-5.3: Charter shares the memory DB file; both serialize via SqlitePathGate on ResolveDbPath().
        services.AddSingleton<CharterService>(_ => new CharterService(memoryOptions.ResolveDbPath()));
        services.AddSingleton<ICharter>(sp => sp.GetRequiredService<CharterService>());
        services.AddSingleton<DriftWatcher>(_ => new DriftWatcher(safetyOptions.DriftSloMinutes));
        services.AddSingleton<SpendMeter>(_ => new SpendMeter(
            safetyOptions.InputTokenRatePer1K,
            safetyOptions.OutputTokenRatePer1K,
            safetyOptions.MonthlyCapUsd,
            safetyOptions.MonthlyTokenCap));

        // BED-133: expose the memory count/stats surface (implemented additively by
        // SqliteMemoryStore; does NOT extend IMemoryStore, so existing stubs stay green).
        services.AddSingleton<IMemoryStats>(sp => sp.GetRequiredService<SqliteEpisodicMemoryRepository>());

        // Memory tools (BED-131): recall_memory + store_memory wrap IMemoryStore so
        // the model can decide to recall a specific memory or store a new one within
        // a turn (in addition to the preamble-injected baseline recall). Registered as
        // ITool singletons — ToolRegistry collects them via IEnumerable<ITool>.
        services.AddSingleton<ITool, RecallMemoryTool>();
        services.AddSingleton<ITool, StoreMemoryTool>();

        return services;
    }
}
