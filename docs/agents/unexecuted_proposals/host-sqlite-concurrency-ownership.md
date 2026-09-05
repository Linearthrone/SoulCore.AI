---
type: proposal
prop_id: PROP-5-host-sqlite-concurrency-ownership
status: unexecuted
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Host SQLite concurrency + charter ownership + SoulLoop single-flight
need: Stop concurrent chat/SMS/SoulLoop/tool paths from racing one SqliteConnection (and a second Charter opener on the same file) so continuity stays boringly reliable
sent_at:
pm_intake:
source_eval: architecture review 2026-09-05 (Good-to-Great / Needs Attention / Reorg / Better-if)
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
parallel_with: PROP-1, PROP-2, PROP-4, PROP-6
---

# Host SQLite concurrency + charter ownership + SoulLoop single-flight

## 1. Need / Want

Kurt needs Host persistence to be **boring under overlap**: chat, SMS, SoulLoop ticks, tasks/workflows/journals, and charter reads must not flake, error mid-turn, or risk torn writes because several async callers share one long-lived `SqliteConnection` (and charter opens a **second** connection to the same file).

This is a **correctness / ownership** need — not a greenfield storage rewrite, not ChatWebSocketHandler decomposition, not Hermes cleanup.

## 2. Goal & Success Criteria

- No Host request/timer/WS path issues concurrent commands on one `SqliteConnection` without an explicit serialization (or per-op connections).
- Charter and memory share **one declared ownership model** for the same DB path; xmldoc matches `Program.cs`.
- SoulLoop `TickAsync` is single-flight across hosted timer + WS `loop.tick`; tick counter is not racy.
- Concurrent soak (memory write + charter read + overlapping ticks) stays green; Host still boots; `/health` remains available when DB is open.
- Kurt-facing success: chat/SMS/loop do not steal the mic from each other; Presence health stops lying about charter/memory under load.
- Explicitly **out of scope for this PROP:** ChatWebSocketHandler god-object split, Program.cs DI module extraction, Hermes retirement, IMAP pooling, vector index, Unreal adapter split, doc-tree merge.

## 3. Context & Constraints

Verified against current tree (2026-09-05):

| Fact | Where |
| --- | --- |
| `SqliteMemoryStore` singleton, one long-lived connection, `Cache=Shared` | `SoulCore.Memory/SqliteMemoryStore.cs` |
| `_gate` only used on dispose — not on command paths | same |
| Same concrete registered as memory / emotion / tasks / workflows / journals / stats | `SoulCore.Host/Program.cs` |
| `CharterService` xmldoc claims independent DB / no Host DI | `SoulCore.Core/Charter/CharterService.cs` |
| Host wires `new CharterService(memoryOptions.ResolveDbPath())` — second opener, same file | `Program.cs` |
| Schema already co-locates charter with memory migrations | Memory schema + charter table |
| SoulLoop hosted timer + WS both call `TickAsync`; `_tickCount++` unsync | `SoulLoopHostedService`, `ChatWebSocketHandler`, `SoulLoopScaffold` |
| Product constraint from USER seat | One LocalAppData SQLite file is fine; no Postgres / dual-DB ops ceremony |

Upstream eval ranked this cluster **highest urgency** among Needs Attention; reorg of the god-object store and WS handler are **sequenced after** connection strategy.

## 4. Clarifying Q&A (answered)

| Q | Locked default (TT synthesis) | Revisit if |
| --- | --- | --- |
| Overlapping `TickAsync`: skip / queue / coalesce? | **Skip** (single-flight; second caller no-ops or acks “busy”) | Kurt demands force-tick always runs |
| Charter permanently co-located in memory file? | **Yes** for this PROP | OPS later wants a dedicated charter path |
| MVP = global serialize vs connection-per-op now? | **Serialize-all first** (`SemaphoreSlim(1,1)`), factory later if measured | Soak shows unacceptable queue latency |
| Hold DB lock across embedding/network I/O? | **Never** | — |
| Enable SoulLoop before gates land? | **No** — keep fail-closed / skip-if-busy until gates exist | — |

Open Kurt questions (non-blocking for park; ask on send-to-PM if still unknown): which flake hurts most this week (mid-chat error vs missing reflection vs SMS silence vs Presence flicker)?

## 5. Avenues Explored

### Avenue A — Serialize + single-flight + ownership honesty (MVP)

Process-wide async gate around all `SqliteMemoryStore` DB work; `PRAGMA busy_timeout` on open; SoulLoop single-flight + `Interlocked` tick; charter either shares the gated session **or** takes the **same** path-keyed gate with short critical sections; fix xmldoc/DI comments.

- **Pros:** Small blast radius; preserves public interfaces; one file; matches “correctness first.”
- **Cons:** Global writer latency under tool storms; not multi-reader.

### Avenue B — Connection-per-operation factory + WAL + busy_timeout

Replace long-lived shared connection with open-per-op (migrations still single-flight at startup); multi-statement ops in one connection + transaction; charter uses same factory/path helper; SoulLoop single-flight still required.

- **Pros:** Idiomatic Microsoft.Data.Sqlite; room for concurrent readers later.
- **Cons:** Touches every store method; migration bootstrap must stay fail-closed; more churn than A.

### Avenue C — Ownership-first merge (charter into memory boundary) then serialize

Fold charter access into memory ownership / shared `ISqliteSession`; delete dual-open wiring; then apply A or B; then SoulLoop gate.

- **Pros:** Ends the doc/DI lie permanently; one schema owner.
- **Cons:** Larger first PR if bundled with connection rewrite; slower to ship.

### Avenue D — Split charter to a second DB file “for safety” (rejected as lead)

- **Why parked:** USER/CONTRA — doubles backup/path confusion; irreversible ops cost; does not fix in-process connection races by itself.

### Avenue E — Full repository / clean-architecture rewrite (rejected)

- **Why parked:** CONTRA kill criteria — no soak failure artifact should not license EF/actor/mailbox platforms; interfaces already exist for a later extract.

## 6. Recommended Route

**Ship Avenue A as the first slice**, with **Avenue C ownership honesty folded in lightly** (same gate / same file; stop lying in comments; prefer one connection owner over dual long-lived openers).

Sequence (hard order):

1. **SoulLoop single-flight** + atomic tick counter (S; independent; stops duplicate side effects).
2. **Store-wide async serialization** on `SqliteMemoryStore` (+ path-keyed gate if charter remains a second type) + `busy_timeout` (S).
3. **Charter ownership fix:** one writer policy on `ResolveDbPath()` — shared gated session **or** charter methods go through the memory connection owner; Memory migrations own `charter_*` DDL; CharterService stops being a second schema authority (S–M).
4. **Concurrent soak test** that would fail before / pass after (required evidence; CONTRA kill switch if green with no prior repro — still ship gates as defense-in-depth).
5. **Park for later PROP:** Avenue B factory; physical repo split behind existing interfaces; parallel chat context reads; ChatWebSocketHandler decomposition; Hermes removal; Program.cs DI modules.

**Do not** hold the DB semaphore across embedding HTTP or other network I/O.

## 7. Alternatives (parked)

| Item | Why parked |
| --- | --- |
| Avenue B connection-per-op | After A + measured contention |
| Physical `SqliteMemoryStore` → many repositories | After connection strategy is safe |
| Parallel memory/charter/emotion reads in chat | Blocked on concurrency fix (eval D) |
| Second charter DB file | Ops ceremony; USER reject as lead |
| ChatWebSocketHandler / Program.cs / Hermes / IMAP / vector / Unreal | Separate eval clusters; do not hitchhike |

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Serialize-all adds latency under tool storms | Keep critical sections tight; never lock across network; escalate to Avenue B only with numbers |
| Deadlock if lock held across await of external I/O | Code review checklist + tests |
| Dual-open remains “temporarily” and becomes permanent | Acceptance: Host must not construct a second R/W opener on `ResolveDbPath()` |
| Schema drift (inline charter DDL vs Memory migrations) | Single DDL source in Memory migrations |
| Partial deploy / wrong `DbPath` | Existing path rules; no new multi-root layout in this PROP |
| Scope expands into persistence platform rewrite | **Kill** if tickets leave Memory+Charter+SoulLoop gate without a soak metric |
| “No reproduction → do nothing” | Still ship A as defense-in-depth; **kill endless rewrite** if soak is green after A |

**Kill the epic if:** success metric becomes “prettier ownership diagram” rather than “N concurrent writers for M minutes with zero SQLite misuse/BUSY escapes,” or if scope adds EF/second database/ChatWebSocketHandler split in the same wave.

## 9. Open Questions for User / PM

1. Which user-visible flake is dominant this week (if any measured): mid-chat error, missing reflection, SMS silence, Presence charter/health flicker?
2. Confirm skip-on-overlap for SoulLoop force-tick (vs queue)?
3. Confirm one SQLite file remains the non-negotiable backup story for this PROP?

## 10. Suggested PM Handoff

- `prop_id`: `PROP-5-host-sqlite-concurrency-ownership`
- Mode: **idea** (from architecture eval; not a stuck-ticket unblock)
- What PM should decide first: accept Avenue A MVP vs jump to Avenue B; confirm SoulLoop skip-vs-queue.
- Suggested splits (hints only; PM owns `.M`):

| Split | Role | One-line |
| --- | --- | --- |
| `PROP-5.1` | BED-01 | SoulLoop single-flight + atomic `_tickCount`; WS + hosted timer share one gate |
| `PROP-5.2` | BED-01 | `SqliteMemoryStore` async command serialization + `busy_timeout`; no lock across network |
| `PROP-5.3` | BED-01 | Charter ownership: one R/W policy on memory DB path; Memory migrations own DDL; fix stale xmldoc |
| `PROP-5.4` | QA-01 | Concurrent soak: chat write + SMS/memory write + dual tick + charter read; dispose-under-load |
| `PROP-5.5` (later) | BED-01 | Optional Avenue B connection factory — only if 5.2 soak shows queue pain |

**TT recommendation to Kurt:** park here until you say **send to PM-01**.
