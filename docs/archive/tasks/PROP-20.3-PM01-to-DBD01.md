---
type: task
prop_id: PROP-20.3
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: DBD-01
priority: P0
status: Pending
created: 2026-11-17
title: Journal books thoughts / wants / dreams
depends_on: none
siblings: PROP-20.1, PROP-20.2, PROP-20.8
blocks: PROP-20.8 (FED Her journal desk)
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.3 — Journals schema

**Activate:** `@Agents/DBD-01.md` then execute this ticket.

## Problem

Victoria journal allow-list is only `feeling`, `animation`, `environment`, `life_hacks` (`SqliteVictoriaJournalRepository`). Law requires also `thoughts`, `wants`, `dreams`. Research papers stay a separate store.

## Solution

1. Migration under `SoulCore.Memory` to register the three new book ids (match existing journal book pattern from `010_life_hacks_journal.sql`).
2. Extend allowed book ids + sort order in `SqliteVictoriaJournalRepository` / `IVictoriaJournalStore` docs.
3. Update `SqliteMemoryStoreJournalTests` expectations.
4. Rollback note in report.

## Files to change

- `SoulCore/SoulCore.Memory/Migrations/` (new)
- `SoulCore/SoulCore.Memory/Repositories/SqliteVictoriaJournalRepository.cs`
- `SoulCore/SoulCore.Memory/IVictoriaJournalStore.cs` (comments/contract)
- `SoulCore/SoulCore.Memory/SqliteMemorySession.cs` (ApplyIfMissing)
- `SoulCore/SoulCore.Protocol.Tests/SqliteMemoryStoreJournalTests.cs`

## Do not touch

- Research journal store, ChatDesktop UI (20.8), Host chat prompt, SMS

## Acceptance

- [ ] Writing a row to `thoughts` succeeds and lists under that book only
- [ ] Research paper list does not show the private journal row
- [ ] Tests green with pasted output
- [ ] Report: `docs/agents/reports/PROP-20.3-DBD01-to-PM01.md`
