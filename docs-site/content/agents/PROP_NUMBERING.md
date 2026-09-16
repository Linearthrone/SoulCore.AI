---
type: config
id: PROP-NUMBERING
updated: 2026-09-15
owner: PM-01 / TT-01
---

# PROP numbering (TT → PM)

**Effective 2026-08-19.** Stops TT intake IDs from colliding with PM `TASK-###` execution counters.

## Format

| Kind | Form | Example |
| --- | --- | --- |
| Proposal (from TT) | `PROP-{N}-{subject}` | `PROP-1-digits-sms-channel` |
| Split work item | `PROP-{N}.{M}` | `PROP-1.1`, `PROP-1.2` |
| Filename (proposal) | keep slug under `unexecuted_proposals/`; frontmatter **must** set `prop_id` | `victoria-digits-sms-channel.md` |
| Filename (split task) | `PROP-{N}.{M}-PM01-to-{ROLE}.md` in `docs/agents/tasks/` | `PROP-1.1-PM01-to-OPS01.md` |

- `{N}` = monotonic integer assigned at TT send-to-PM (or by PM on first accept if TT omitted it).
- `{subject}` = short kebab slug of the need (not a novel).
- `{M}` = split index starting at **1**. PM may **re-split / merge / reassign roles**; keep the same `PROP-N` root.
- Optional `PROP-{N}.0` = PM accept / routing note back to TT (not an execution seat).

## Rules

1. **TT never invents `TASK-###` for new idea intakes.** Use `PROP-N-subject` + optional suggested `PROP-N.M` splits in §10 Suggested PM Handoff.
2. **PM owns division of labor.** Suggested `PROP-N.M` from TT are hints; WonderWoman/PM may renumber `.M` or change roles when ticketing.
3. Legacy `TASK-{date}-{id}-…` files remain valid for pre-PROP work (Playwright Wave 30 = TASK-193..199). Do not reuse those integers for new TT ideas.
4. Reports: `docs/agents/reports/PROP-{N}.{M}-{ROLE}-to-PM01.md` (or keep TASK- date filenames only for legacy).
5. Unblock evals of an **existing** `TASK-*` may still return `TASK-*-TT01-to-PM01.md`, but any **new** proposal spawned from that eval gets a fresh `PROP-N`.

## Registry (active)

Reconciled against `main` on **2026-09-15**. `Pass` = accepted **and** the code is on `main`.

| prop_id | Subject | Status | Splits |
| --- | --- | --- | --- |
| PROP-1-digits-sms-channel | Tablet SMS/MMS Avenue B (MDN = SM-X218U; DIGITS dropped). Temp: Tasker/Termux; goal: self-sufficient House gateway. | **1.1–1.4 landed** (1.4 Partial) → **1.5 / 1.6 open** | 1.1–1.6 |
| PROP-2-ue-reliable-embodiment | UE Kayleigh 1P / Victoria walk / one eye | **Open** — human-gated on shadow PIE; 2.1–2.3 Pending, 2.4 Held | 2.1–2.4 |
| PROP-3-link-messenger-product | Link Messenger-class rewrite | parked until SMS QA Pass | — |
| PROP-4-presence-shell-honest-hud | Presence House drawer + installer | **Open** — 4.1 **Partial** (branch slice unlanded + Windows visual QA), 4.2 Pending | 4.1 FED · 4.2 OPS |
| PROP-5-host-sqlite-concurrency-ownership | Host SQLite concurrency + charter ownership + SoulLoop single-flight | **Pass** — 5.1–5.4 Accepted 2026-09-05 (`cursor/prop5-sqlite-gate-8a1f`); on `main` | 5.1–5.4 |
| PROP-6-desktop-drag-async-delay | Desktop drag Thread.Sleep → async delay | **Pass** — 6.1 Accepted 2026-09-05 (`cursor/prop6-desktop-delay-8a1f`); on `main` | 6.1 |
| PROP-7-hermes-dead-surface-cleanup | Remove live Hermes contracts/config/DI + docs honesty | **Pass** — 7.1 Accepted 2026-09-05 (`cursor/prop7-hermes-cleanup-8a1f`); on `main` | — (report-only) |
| PROP-8-chat-orchestration-decomposition | ChatWebSocketHandler strangler + prompt builder + history + gated parallel reads | **Pass** — 8.1 Accepted 2026-09-05 (`cursor/prop8-chat-strangler-8a1f`); on `main` | — (report-only) |
| PROP-9-host-di-composition-modules | Extract Program.cs DI into Add* modules | **Pass** — 9.1 Accepted 2026-09-05 (`cursor/prop9-di-modules-8a1f`); on `main` | — (report-only) |
| PROP-10-inference-clients-tools-split | Inference Clients vs Tools boundary | **Pass** — 10.1 Accepted 2026-09-05; landed on `main` 2026-09-14 via PR #86 (`cursor/prop10-inference-split-land-9d6e`; supersedes orphan `cursor/prop10-inference-split-8a1f`) | — (report-only) |
| PROP-11-memory-store-repository-split | Split SqliteMemoryStore into repos (one DB file) | **Pass** — 11.1 Accepted 2026-09-05 (`cursor/prop11-memory-repos-8a1f`); on `main` | — (report-only) |

**Report-only splits (PROP-7…11).** No `PROP-7.1`…`PROP-11.1` ticket files exist. Each shipped
straight off its proposal + intake and filed a `PROP-{N}.1-BED01-to-PM01.md` Pass report. Recorded
as the approved exception in `docs/archive/tasks/PROP-7-11-PM01-gated-hold.md` — do not file these
as missing paperwork.

Cluster map (closed): `docs/archive/proposals/architecture-eval-backlog-cluster-map.md`  
Program accept (closed): `docs/archive/tasks/PROP-5-11-PM01-program-accept.md`  
Scoreboard: `docs/agents/reports/PROP-5-11-TINA-wipeout-final.md`

Next free `N`: **12**.

## Open items (not on `main`)

| Item | Owner | What is left |
| --- | --- | --- |
| **PROP-1.5** | QA-01 | Live operator tablet SMS round-trip + MMS screenshot still. Human-gated (needs the SM-X218U) |
| **PROP-1.6** | FED-01 | Link shrink to status + ComfyUI — blocked on PROP-1.5 Pass |
| **PROP-2.1–2.3** | REX-01 | Shadow PIE Kayleigh 1P possess, travel-cm/loco AnimBP, one Presence eye still. Human-gated (needs the Shadow PC) |
| **PROP-2.4** | REX-01 | Held until PROP-2.1 Pass |
| **PROP-4.1** | FED-01 | **Partial.** Landed slice is on `main`. Still only on `cursor/prop4-presence-drawer-8a1f`: `PresenceHonestyTests.cs`, `Properties/AssemblyInfo.cs` (`InternalsVisibleTo`), and the Avalonia `RadialGradientBrush` `Radius` → `RadiusX`/`RadiusY` fix in `MainWindow.Presence.cs`. Needs its own PR, then Windows visual QA |
| **PROP-4.2** | OPS-01 | Presence installer + Start shortcut + Velopack update toast — not started |
| **PROP-3** | — | Parked until SMS QA Pass |

Legacy non-PROP tickets still Pending: `TASK-123`, `TASK-137`, `TASK-139` (QA gates),
`TASK-191` (Partial) / `TASK-192` (Queued) under REX.
