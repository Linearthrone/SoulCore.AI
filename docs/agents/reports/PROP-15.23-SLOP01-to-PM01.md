---
type: report
prop_id: PROP-15.23
prop_root: PROP-15-persona-creation-platform
from: SLOP-01
to: PM-01
task_id: PROP-15.23
status: findings
created: 2026-10-05
updated: 2026-10-05
qa_report: docs/agents/reports/PROP-15.22b-QA01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
---

# SLOP-01 Report — PROP-15.23

## Scope
- Related QA task / report: `PROP-15.22b` / `docs/agents/reports/PROP-15.22b-QA01-to-PM01.md` (Pass)
- Related DEV: `PROP-15.21b` (BED Host 0.2.0 + capabilities) + `PROP-15.22` (FED chrome / feed)
- Files / packages scanned:
  - `SoulCore/SoulCore.Host/HostBuildVersion.cs`
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (`/health` version + `capabilities.personas`)
  - `SoulCore/SoulCore.Protocol.Tests/HostBuildVersionTests.cs`
  - `House/House.ChatDesktop/MainWindow.axaml` (PersonaSwitcher ItemTemplate, `BuildVersionsText`, Updates currency boxes)
  - `House/House.ChatDesktop/MainWindow.Persona.cs`
  - `House/House.ChatDesktop/MainWindow.Updates.cs`
  - `House/House.ChatDesktop/MainWindow.Presence.cs` (health → `_healthHasPersona` / chrome)
  - `House/House.ChatDesktop/Services/BuildCurrencyStatus.cs`
  - `House/House.ChatDesktop/Services/PresenceUpdateService.cs` (CreatorEdition feed defaults)
  - `House/House.ChatDesktop/Services/SoulCorePersonaClient.cs` (`PersonaApiMissing` / 404 copy)
  - `House/House.ChatDesktop/Services/SoulCoreHealthClient.cs` (`HealthHasPersona` / version)
  - `House/House.ChatDesktop/Services/LocalUiSettings.cs` (feed override comment)
  - Related tests: `BuildCurrencyStatusTests`, `PresenceUpdateServiceTests`
- Method: read + ripgrep for sibling duplicates / alias pairs / AGENTS naming. No product code modified. Stress-suite (15.13*) not expanded.

## Summary
- Findings count: **3** (1× P1, 2× P2)
- Highest severity: **P1**
- Naming (AGENTS): **1** forbidden token in scoped Presence path (F1)

## Findings

### F1 — Forbidden human-name token in Presence system notice
- Category: slop
- Severity: P1
- Evidence: `House/House.ChatDesktop/MainWindow.Presence.cs` ~L158–160 — system notice example path embeds `C:\Users\kurtw\Soul_Core`
- Why it matters: `Agents/AGENTS.md` zero-exception rule — human name must never appear as Kurt / kurt / kurtw; UI copy for LinearThrone must not leak machine username tokens.
- Recommended action: **remove** (replace example with a neutral path, e.g. `C:\Users\LinearThrone\…` or drop the concrete username segment)
- Suggested owner: FED-01

### F2 — Dual Host “has personas” signals; Presence ignores `capabilities.personas`
- Category: alias-sprawl
- Severity: P2
- Evidence:
  - Host emits both `capabilities.personas = true` (`WebApplicationExtensions.cs` ~L151–156) and a `persona` object (~L157–164)
  - Presence stale detection uses `_personaApiOk` (`/api/personas` 404 → `PersonaApiMissing`) + `_healthHasPersona` (`dto?.Persona is not null` in `SoulCoreHealthClient.cs` ~L148) — **no** parse of `capabilities` anywhere under `House/House.ChatDesktop`
  - BED 15.21b + OPS 15.21c already noted optional FED wiring of `capabilities.personas`
- Why it matters: two Host answers to the same question (“CreatorEdition persona Host?”). Currency chrome works today via persona object + API 404; `capabilities` is Host-only observability unless FED consumes it. Future readers may assume chrome already keys off `capabilities` and drift.
- Recommended action: **ask-user**
- Ask-user question: Keep `capabilities.personas` as Host/ops observability only (document; leave chrome on persona object + API), or ticket FED to treat `capabilities.personas !== true` as Host outdated alongside existing signals?

### F3 — Parallel CreatorEdition feed URL constants
- Category: duplicate
- Severity: P2
- Evidence: `PresenceUpdateService.cs` ~L14–18 — `DefaultGithubRepoUrl` and `DefaultGithubApiReleasesUrl` both hardcode `House-VictoriAI/SoulCore.AI_CreatorEdition`; empty-feed path uses the API constant (~L200) while overrides already derive API via `TryGithubApiFromRepoUrl` (~L205)
- Why it matters: mild; a future feed rename can update one constant and leave the probe URL on the old repo.
- Recommended action: **dedupe** (set `DefaultGithubApiReleasesUrl` from `TryGithubApiFromRepoUrl(DefaultGithubRepoUrl)` or a single owner/repo pair)
- Suggested owner: FED-01

## Clean areas
- **`BuildCurrencyStatus`**: single formatter for title chrome + Settings currency; `HostNoPersonaApiMessage` shared with persona client — no copy drift of the restart string.
- **`HostCurrencyLabel` / `HostCurrencyShort`**: intentional long vs short surfaces (Settings vs title); branching shares `IsHostOutdatedForPersonas` — not flagged as actionable sprawl.
- **Persona status dual TextBlocks** (`PersonaChromeStatusText` + `PersonaSwitcherStatusText`): one `SetPersonaStatus` writer — correct dual UI, not duplicate logic.
- **CreatorEdition feed default**: tests assert CreatorEdition repo and reject legacy `Linearthrone/SoulCore.AI` — feed cutover is coherent.
- **Host version bump**: `HostBuildVersion` → `/health.version` 0.2.0; test pins `0.2.` prefix — no parallel version helper on Host.
- **InformationalVersion strip** on Host vs Presence: same pattern in two assemblies (expected for independent binaries) — not recommended to share across Host/desktop boundary.
- Stress-suite / 15.13* left untouched per ticket.

## Notes for PM
- Safe to archive? **no** — ticket FED for **F1** (AGENTS scrub) before calling the 15.22 chrome slice done; **F3** can ride the same FED pass; **F2** needs LinearThrone / PM choice (ask-user).
- Functional QA Pass (15.22b) stands; findings are hygiene / naming / signal-ownership, not reopen of persona switcher or currency chrome behavior.
- Optional QA note (host-down Frame Refresh wait) remains non-blocking per QA — out of SLOP scope unless PM tickets FED for honesty path.
