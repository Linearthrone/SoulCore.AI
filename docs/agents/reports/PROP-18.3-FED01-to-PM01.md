---
type: report
prop_id: PROP-18.3
prop_root: PROP-18-chronology-and-felt-persistence
from: FED-01
to: PM-01
status: Pass
created: 2026-11-19
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
depends_on: PROP-18.7
task: docs/agents/tasks/PROP-18.3-PM01-to-FED01.md
unblocked_by: docs/agents/reports/PROP-18.7-BED01-to-PM01.md
---

# PROP-18.3 — Presence honesty + activities log UI — Pass

## Verdict: **Pass**

Desk surfaces Host residue without fabrication: honesty band (alive / last tick / last want / last journal), chat-open **While you were gone** strip (empty is honest), and an **Activities log** window with the Q1 columns. Journal reader shows topic tags on appends; no voice playback.

## Done

1. **Honesty band** (`BandHonestyText`) — Host-proven only:
   - Alive ← `/health`
   - Last tick ← WS `loop.tick.ok` (want frame may seed if tick frame not yet seen)
   - Last want ← WS `loop.want` (parsed phrase; never mood restamp)
   - Last journal ← newest crumb from `GET /presence/while-you-were-gone` (or activity Desc fallback)
   - Missing values render as **none yet** (no theater)
2. **While you were gone** — on Presence open, calls `GET /presence/while-you-were-gone?since=` using `LocalUiSettings.LastPresenceSeenAtUtc` (persisted on close); dismissible strip above transcript; empty copy: *Nothing stored while you were gone — empty is honest.*
3. **Activities log desk** — `ActivitiesLogWindow` + Activity band **Activities…** button; columns Entry / Date/Time / Activity / Subject/Focus / Desc; optional subject filter; detail pane shows Desc + journal soft-link.
4. **Journal reader** — `HerJournalWindow` / `JournalEntryRow` surface topic tags so PROP-18.7 thoughts appends are visible; no voice playback added.
5. **Client** — `SoulCoreActivitiesClient` for `/activities`, `/presence/while-you-were-gone`, `/journals/topic` + `PresenceResidueFormatter`.

## Acceptance

| Criterion | Result |
| --- | --- |
| Alive / last tick / last want / last journal from Host (no fabrication) | Pass |
| Chat-open While you were gone from store (empty honest) | Pass |
| Activities log columns: Entry, Date/Time, Activity, Subject/Focus, Desc | Pass |
| Topic journal appends visible in existing reader; no voice playback | Pass |

## Out of scope (honored)

Host API authorship (BED-18.7); voice journal playback (Q4 No); outbound ping.

## Live Host note

Local Host on `:7700` / `:7710` answered `/health` but returned **404** on `/activities` and `/presence/while-you-were-gone` (binary predates PROP-18.7 routes). UI treats 404 as honest unavailable copy until OPS restarts Host with 18.7 code. Unit parse/format tests cover the wire contracts.

## Evidence

```
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Debug -o %TEMP%\hv-fed183-build
  → Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Debug
  --filter "FullyQualifiedName~SoulCoreActivitiesClientTests"
  → Passed!  Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

Cases: desk columns parse; missing activities[] is problem; empty while-you-were-gone honest copy; residue list body; honesty line no fabrication; journal topic tag hint.

## Files

- `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs` (+ `PresenceResidueFormatter`)
- `House/House.ChatDesktop/ActivitiesLogWindow.axaml`
- `House/House.ChatDesktop/ActivitiesLogWindow.axaml.cs`
- `House/House.ChatDesktop/MainWindow.axaml`
- `House/House.ChatDesktop/MainWindow.axaml.cs`
- `House/House.ChatDesktop/MainWindow.Presence.cs`
- `House/House.ChatDesktop/HerJournalWindow.axaml`
- `House/House.ChatDesktop/HerJournalWindow.axaml.cs`
- `House/House.ChatDesktop/Services/SoulCoreJournalClient.cs`
- `House/House.ChatDesktop/Services/LocalUiSettings.cs`
- `House/House.ChatDesktop.Tests/SoulCoreActivitiesClientTests.cs`
- `docs/agents/reports/PROP-18.3-FED01-to-PM01.md`

## Naming

Docs/UI operator: **LinearThrone**. No forbidden personal names.

## Next

PM-01 accept → QA against a Host that exposes PROP-18.7 routes (restart after 18.7 deploy).
