---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.27
prop_id: PROP-18.27
prop_root: PROP-18-chronology-and-felt-persistence
status: findings
created: 2026-11-19
related_qa: docs/agents/reports/PROP-18.24-QA01-to-PM01.md
related_fed: docs/agents/reports/PROP-18.3-FED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.27 (Presence desk — honesty / WYWYG / Activities / journal tags)

## Scope

- Related QA: `docs/agents/reports/PROP-18.24-QA01-to-PM01.md` (Pass — Ready for SLOP-01)
- Related FED: `docs/agents/reports/PROP-18.3-FED01-to-PM01.md`
- Files / packages scanned:
  - `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs` (+ `PresenceResidueFormatter`, DTOs)
  - `House/House.ChatDesktop/Services/SoulCoreJournalClient.cs` (`JournalEntryRow.TopicTagHint`)
  - `House/House.ChatDesktop/Services/LocalUiSettings.cs` (`LastPresenceSeenAtUtc`)
  - `House/House.ChatDesktop/ActivitiesLogWindow.axaml` / `.axaml.cs`
  - `House/House.ChatDesktop/HerJournalWindow.axaml` / `.axaml.cs`
  - `House/House.ChatDesktop/MainWindow.axaml` (WYWYG strip + honesty band + Activities…)
  - `House/House.ChatDesktop/MainWindow.Presence.cs` (`LoadWhileYouWereGoneOnOpenAsync`, `RefreshHonestyBand`, `ActivitiesLog_Click`)
  - `House/House.ChatDesktop/MainWindow.axaml.cs` (client field + Presence-open call site)
  - Tests: `House/House.ChatDesktop.Tests/SoulCoreActivitiesClientTests.cs`
- Method: read primary sources + ripgrep for `SoulCoreActivitiesClient` / `ListTopicAsync` / `PresenceResidueFormatter` / `TopicTagHint` / `WhileYouWereGone` / `BandHonesty` / `_lastJournalCrumb`
- Adjacent alias check: Host `/journals/topic` already audited under PROP-18.25 (not re-scoped)
- **Read-only.** No code modified.

## Summary

- Findings count: **2**
- Highest severity: **P1**
- Verdict: **findings**

## Findings

### F1 — Unused `/journals/topic` client surface (dead wire)

- Category: slop
- Severity: **P1**
- Evidence:
  - `SoulCoreActivitiesClient.ListTopicAsync` / `ParseTopicJournal` / `TopicJournalFetch` / `TopicJournalDto` (`SoulCoreActivitiesClient.cs` ~47–50, ~150–187, ~251–266, ~322–329).
  - Ripgrep: **no desk callers** — not used by `ActivitiesLogWindow`, `HerJournalWindow`, `MainWindow.Presence`, or tests (tests cover parse/format for activities + WYWYG + honesty + `TopicTagHint` only).
  - Journal topic tags are already surfaced via the existing reader path: `SoulCoreJournalClient` → `JournalEntryRow.Line` / `TopicTagHint` + `HerJournalWindow` entry detail (`Tags {tags}`).
- Why it matters: FED-18.3 claimed the client covers `/journals/topic`, but the desk never calls it. Dead public API duplicates Host capability without a UI contract, invites drift (Host changes with no client test), and pads the change surface.
- Recommended action: **ask-user** (then remove *or* wire)
- Suggested owner if remove/dedupe: **FED-01**
- Ask-user question: Drop `ListTopicAsync` (+ `TopicJournalFetch` / `ParseTopicJournal`) until a desk needs subject-filtered topic crumbs, **or** wire it (e.g. Activities subject filter → topic crumbs / journal jump)? Recommend **remove until needed** — tags already visible in Her journal.

### F2 — Honesty band “Last journal” can show an activity Desc

- Category: slop (honesty-label stretch)
- Severity: **P2**
- Evidence:
  - `LoadWhileYouWereGoneOnOpenAsync` (`MainWindow.Presence.cs` ~916–920): if `NewestJournalPreview` is empty but `fetch.Activities` has rows, sets `_lastJournalCrumb = fetch.Activities[0].DescText`.
  - `RefreshHonestyBand` → `PresenceResidueFormatter.FormatHonestyLine(..., lastJournalCrumb)` always labels the slot **Last journal** (`SoulCoreActivitiesClient.cs` ~384–395).
  - FED-18.3 documents this as intentional (“or activity Desc fallback”); QA did not treat it as a fail.
- Why it matters: the band copy says “journal” while the value may be an activities-log Desc. Mild theater relative to the honesty product law (Host-proven, no fabrication of *meaning*). Data is still Host-proven; the **label** is the slop.
- Recommended action: **ask-user**
- Suggested owner if remove/dedupe: **FED-01**
- Ask-user question: Keep Desc fallback but rename the band slot (e.g. **Last residue**), show Desc only under a separate clause, **or** leave “Last journal — none yet” when there are no journal crumbs?

## Clean areas

- **Honesty formatter:** `FormatHonestyLine` uses only caller-supplied Host-proven fields; missing → `none yet` (no invented tick/want/journal). Covered by `FormatHonestyLine_DoesNotFabricate`.
- **WYWYG:** single load path + `PresenceResidueFormatter.FormatWhileYouWereGoneBody`; empty copy is honest; Problem/404 copy is explicit (PROP-18.7 restart), not fabricated residue.
- **Activities log:** one window + five-column template; `ActivityLogRow.*Text` bindings match QA/FED columns; no parallel activities UI.
- **Journal tags:** `TopicTagHint` on list line + entry-detail Tags block; Her journal copy states no voice playback — no second playback surface.
- **Client ownership:** MainWindow long-lived `_activities` vs dialog-owned client in `ActivitiesLogWindow` matches `HerJournalWindow` / `SoulCoreJournalClient` pattern — not alias sprawl.
- **Known Host alias (out of this ticket):** `desc`/`sessionDesc` dual wire already flagged PROP-18.25 F1; desk `FirstNonEmpty` hedge is appropriate until BED settles — not re-opened here.
- **Comments:** PROP-18.3 markers document product law; consistent with nearby Presence/Identity formatters.

## Notes for PM

- Safe to archive? **no** — resolve F1 (remove vs wire) before calling desk hygiene complete; F2 is product-copy ask-user (non-blocking if deferred with rationale).
- If findings: ticket **FED-01** for F1 cleanup (and F2 if user picks a label policy).
- QA Pass (PROP-18.24) stands; these are hygiene / honesty-label items, not functional fails.
- Live `:7700` 404 / OPS PROP-18.6c remains out of SLOP scope.
