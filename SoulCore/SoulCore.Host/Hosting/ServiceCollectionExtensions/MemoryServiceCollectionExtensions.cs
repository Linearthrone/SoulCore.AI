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
        services.AddSingleton<SqliteVictoriaSopRepository>();
        services.AddSingleton<IVictoriaSopStore>(sp => sp.GetRequiredService<SqliteVictoriaSopRepository>());
        services.AddSingleton<SqliteVictoriaResearchRepository>();
        services.AddSingleton<IVictoriaResearchStore>(sp => sp.GetRequiredService<SqliteVictoriaResearchRepository>());
        // PROP-18.6: activities log (Q1 residue); writers land in BED-18.7.
        services.AddSingleton<SqliteVictoriaActivitiesLogRepository>();
        services.AddSingleton<IVictoriaActivitiesLogStore>(sp =>
            sp.GetRequiredService<SqliteVictoriaActivitiesLogRepository>());
        // PROP-22.1: operator work items + week agenda (migration 016).
        services.AddSingleton<SqliteOperatorWorkItemRepository>();
        services.AddSingleton<IOperatorWorkItemStore>(sp => sp.GetRequiredService<SqliteOperatorWorkItemRepository>());
        services.AddSingleton<SqliteOperatorAgendaRepository>();
        services.AddSingleton<IOperatorAgendaStore>(sp => sp.GetRequiredService<SqliteOperatorAgendaRepository>());
        services.AddSingleton<SqliteMemoryStore>(sp => new SqliteMemoryStore(sp.GetRequiredService<SqliteMemorySession>()));

        // PROP-3 Wave 1: durable operator <-> Victoria transcript (migration 007), so a fresh
        // client can backfill history. Not IMemoryStore (summaries) and not
        // IChatSessionHistoryStore (bounded RAM context) — see IChatTranscriptStore.
        services.AddSingleton<SqliteChatTranscriptRepository>();
        services.AddSingleton<IChatTranscriptStore>(sp => sp.GetRequiredService<SqliteChatTranscriptRepository>());

        // PROP-20.37: growth bond soak / weights / ledger / pending + bond counters.
        services.AddSingleton<SqliteGrowthRepository>();
        services.AddSingleton<IGrowthStore>(sp => sp.GetRequiredService<SqliteGrowthRepository>());

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
        // PROP-18.2: optional dated timeline (journal + activity-log + episodic).
        services.AddSingleton<ITool, RecallTimelineTool>();

        return services;
    }
}
