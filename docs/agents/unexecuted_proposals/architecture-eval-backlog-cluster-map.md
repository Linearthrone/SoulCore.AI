---
type: tt-cluster-map
status: unexecuted
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Architecture eval vs live backlog — parallel cluster map
source_eval: architecture review 2026-09-05 (Good→Great / Needs Attention / Restructure / Better-if)
---

# Architecture eval vs live backlog — parallel cluster map

TT-01 facilitator map. **Not an execution ticket.** Proposals for new clusters: PROP-5…PROP-11.

## 1. Live backlog (before this wave)

| ID | Subject | Status | Primary seat | Overlap with eval? |
| --- | --- | --- | --- | --- |
| PROP-1 | Tablet SMS/MMS gateway | 1.1–1.3 Pass; **1.4 SEC / 1.5 QA / 1.6 FED open** | SEC / QA / FED | None (product lane) |
| PROP-2 | UE reliable embodiment | Ticketed 2.1–2.4; TASK-191 Partial | REX | Weak — Unreal stub split is adjacent, not same |
| PROP-3 | Link Messenger-class rewrite | Parked until SMS QA Pass | FED (later) | None |
| PROP-4 | Presence House drawer + installer | In progress; Velopack toast open | FED / OPS | None |
| PROP-5 | Host SQLite concurrency + charter ownership + SoulLoop single-flight | **Unexecuted** (parked this session) | BED | **Direct** — eval Needs Attention #1–2 + SoulLoop + Better-if charter |
| TASK-123 / 137 / 139 | Legacy QA gates (wave / desktop / MT4) | Pending | QA | None |
| PRODUCT_ROOT claim | “Hermes retired (`NullHermesClient`)” | Claimed done | — | **Partial** — null client yes; contracts/config/DI still live (eval Needs Attention) |

**Issues folder:** empty (none open).

## 2. Eval item → disposition

| Eval item | Severity (eval) | Matches existing backlog? | Cluster / PROP |
| --- | --- | --- | --- |
| Shared SQLite connection races | High | **PROP-5** | PROP-5 |
| Charter dual connection / ownership lie | High | **PROP-5** | PROP-5 |
| SoulLoop no single-flight | Medium | **PROP-5** | PROP-5 |
| `Thread.Sleep` in desktop drag | High impact / low risk | **No** | **PROP-6** |
| Hermes dead contracts still shaping Host | Medium | Partial (Null client only) | **PROP-7** |
| Split `SqliteMemoryStore` god-object | High restructure | No (after concurrency) | **PROP-11** |
| Decompose `ChatWebSocketHandler` | Very high | No | **PROP-8** |
| Extract DI from `Program.cs` | High | No | **PROP-9** |
| Split Inference clients vs tools | Medium | No | **PROP-10** |
| Normalize handbook / docs-site trees | Medium / low risk | No | Fold light truth into PROP-7; **full merge parked** |
| Parallel chat context reads | High / after SQLite | No | Fold into **PROP-8** (gated on PROP-5) |
| Vector / indexed recall | High / later | No | Optional later under PROP-11 — **no start without metric** |
| History `RemoveAt(0)` → deque | Medium / low | No | Fold into **PROP-8** |
| Single prompt composition builder | Medium / low | No | Fold into **PROP-8** |
| IMAP session pool | Medium / high risk | No | **Parked — no PROP** until measured pain |
| Unreal stub → focused adapters | Medium | Adjacent PROP-2 | **Parked / fold into PROP-2** unless Host-free seam work is required |
| Good→Great (protocol, security, nulls, tools, caps) | Protect | N/A | **No PROP** — invariants, not work |

## 3. Parallel lanes (practical wipeout)

Constraint from CONTRA (accepted): **at most one open lane that edits `SoulCore.Host/`** (`Program.cs`, `Ws/`, loop wiring). Non-Host lanes may run in parallel.

```text
WAVE NOW (5 parallel seats — different ownership)
├─ Lane SMS     PROP-1.4/.5/.6     SEC / QA / FED
├─ Lane UE      PROP-2.1–2.4       REX
├─ Lane Presence PROP-4            FED / OPS
├─ Lane Desktop PROP-6             BED (Inference/Tools/Desktop only)
└─ Lane Persist PROP-5             BED (Memory + Charter + SoulLoop)  ← sole Host lane

WAVE AFTER PROP-5 Pass
├─ Lane Hermes  PROP-7             BED (Host+Config dead surface)  ← sole Host lane
├─ Lane Memory  PROP-11            BED (Memory repos; no Host WS)
└─ (product lanes continue)

WAVE AFTER PROP-7 Pass
├─ Lane DI      PROP-9             BED (Program.cs modules)        ← sole Host lane
└─ Lane Infer   PROP-10            BED (Inference csproj/folders; no Host)

WAVE AFTER PROP-5 + PROP-9 (or after PROP-7 if DI deferred)
└─ Lane Chat    PROP-8             BED (ChatWebSocketHandler + prompt + history)
                                   ← sole Host lane; includes gated parallel reads
```

### Conflict fences

| Hot file / area | Allowed owner |
| --- | --- |
| `SqliteMemoryStore.cs` | PROP-5 then PROP-11 only |
| `CharterService` / charter DI | PROP-5 only |
| `SoulLoopScaffold` / hosted loop | PROP-5 only |
| `NativeDesktopControlBackend.cs` | PROP-6 only |
| Hermes types / `HermesToolRouting` / PreferHermes arms | PROP-7 only |
| `Program.cs` | PROP-5 (minimal) → PROP-7 → PROP-9 (never two PRs at once) |
| `ChatWebSocketHandler.cs` | PROP-7 (delete-only Hermes arms) → PROP-8 (structural split) |
| `SoulCore.Inference` clients vs `Tools/` | PROP-10 (after PROP-7 if it deletes Hermes routing) |

## 4. Explicitly not ticketed

| Item | Why parked |
| --- | --- |
| IMAP connection pool | High risk, no measured storm; connect-per-call is acceptable |
| Full `docs/handbook` ↔ `docs-site` merge epic | Busywork during Host freeze; only sync claims that would otherwise lie (Hermes) via PROP-7 |
| Standalone vector DB PROP | Semantic recall already exists; new index needs a failing metric |
| Unreal adapter PROP | Keep under PROP-2 REX lane unless a Host-free seam is proven necessary |
| Rewriting PROP-1…4 | Product lanes already staffed — do not absorb into eval wipeout |

## 5. Kill criteria for the wipeout program

- Two open PRs both edit `Program.cs` or `ChatWebSocketHandler`
- “Architecture purity” tickets without soak / user-visible flake evidence
- IMAP / vector / docs-merge minted without a measured failure
- Chat path split across multiple teams in one wave
- Scope hitchhikes EF / second database onto PROP-5

## 6. Proposal index (this wave)

| prop_id | Slug | Wave |
| --- | --- | --- |
| PROP-5-host-sqlite-concurrency-ownership | `host-sqlite-concurrency-ownership.md` | NOW (Host) |
| PROP-6-desktop-drag-async-delay | `desktop-drag-async-delay.md` | NOW (parallel) |
| PROP-7-hermes-dead-surface-cleanup | `hermes-dead-surface-cleanup.md` | After PROP-5 |
| PROP-8-chat-orchestration-decomposition | `chat-orchestration-decomposition.md` | After PROP-5 (+ prefer after PROP-9) |
| PROP-9-host-di-composition-modules | `host-di-composition-modules.md` | After PROP-7 |
| PROP-10-inference-clients-tools-split | `inference-clients-tools-split.md` | After PROP-7 |
| PROP-11-memory-store-repository-split | `memory-store-repository-split.md` | After PROP-5 |
