---
type: report
prop_id: PROP-20.8
prop_root: PROP-20-victoria-mind-path
from: FED-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
task: docs/agents/tasks/PROP-20.8-PM01-to-FED01.md
depends_on: PROP-20.3
---

# PROP-20.8 — Her journal desk window — Pass

## Verdict

**Pass** — Presence now has a separate **Her journal…** desk window listing all seven private books (including `thoughts` / `wants` / `dreams`). Research stays in its own window (`Research…`). Private journal rows are not research papers.

## Done

1. Host read-only Presence APIs over existing `IVictoriaJournalStore`:
   - `GET /journals/books`
   - `GET /journals/books/{bookId}/entries?limit=`
2. Desk client `SoulCoreJournalClient` + `HerJournalWindow` (books left / entries right).
3. Activity band: **Her journal…** beside **Research…** (former single “Journals…” label split so private ≠ research).
4. Identity stats stub lists seven books and points at Her journal.
5. Presence `0.1.54` / Host `0.1.54`.

## Acceptance

| Criterion | Result |
| --- | --- |
| A `thoughts` row is visible on desk Her journal UI | **Pass** — books list includes `Thoughts Journal (thoughts)` from `GET /journals/books` |
| Same row is not listed as a research paper | **Pass** — `/research/subjects` still only hobby subjects (e.g. neural interfaces); no `thoughts` hit |
| Report with build/UI evidence | **Pass** — this file |

## Build / test evidence

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release
→ Build succeeded. 0 Warning(s) 0 Error(s)

dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj -c Debug
→ Build succeeded. (Release blocked here by SixLabors ImageSharp license gate — pre-existing)

dotnet test House/House.ChatDesktop.Tests --filter "FullyQualifiedName~SoulCoreJournalClientTests|FullyQualifiedName~PresenceHonesty"
→ Passed!  Failed: 0, Passed: 13
```

## Live Host smoke (Debug, port 7710)

Host applied Memory migration `012` on open, then:

```text
GET /journals/books → count=7
  feeling, animation, environment, life_hacks, thoughts, wants, dreams
  thoughts title: "Thoughts Journal"

GET /research/subjects → count=1
  slug=neural-interfaces (no thoughts / private journal rows)

GET /journals/books/thoughts/entries?limit=5 → book.id=thoughts, count=0
```

UI wiring: band buttons `Her journal…` → `HerJournalWindow`; `Research…` → `ResearchJournalsWindow` (unchanged research client).

## Files

- `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` — journal GET routes
- `SoulCore/SoulCore.Host/SoulCore.Host.csproj` — 0.1.54
- `House/House.ChatDesktop/Services/SoulCoreJournalClient.cs` — new
- `House/House.ChatDesktop/HerJournalWindow.axaml` + `.axaml.cs` — new
- `House/House.ChatDesktop/MainWindow.axaml` — Her journal / Research buttons
- `House/House.ChatDesktop/MainWindow.Presence.cs` — `HerJournal_Click`
- `House/House.ChatDesktop/MainWindow.axaml.cs` — identity books line
- `House/House.ChatDesktop/House.ChatDesktop.csproj` — 0.1.54
- `House/House.ChatDesktop.Tests/SoulCoreJournalClientTests.cs` — new

## Notes / follow-ups

- Host **Release** build needs `SixLaborsLicenseKey` / `sixlabors.lic` (existing Inference package gate). Debug Host used for API smoke.
- Full ChatDesktop suite still has 3 unrelated companion-token env/permission failures on this machine; journal + honesty filters are green.
- OPS: desk needs Host with migration `012` applied (auto on open) so `thoughts` appears.

## Naming

- Docs/UI operator: **LinearThrone**. No Kurt.
