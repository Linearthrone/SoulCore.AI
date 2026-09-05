---
type: config
id: PROP-NUMBERING
updated: 2026-09-05
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

| prop_id | Subject | Status | Splits |
| --- | --- | --- | --- |
| PROP-1-digits-sms-channel | Tablet SMS/MMS Avenue B (MDN = SM-X218U; DIGITS dropped). Temp: Tasker/Termux; goal: self-sufficient House gateway. | **1.1–1.3 Pass** → 1.4+ open | 1.1–1.6 |
| PROP-2-ue-reliable-embodiment | UE Kayleigh 1P / Victoria walk / one eye | accepted-pm-ticketed | 2.1–2.4 |
| PROP-3-link-messenger-product | Link Messenger-class rewrite | parked until SMS QA Pass | — |
| PROP-4-presence-shell-honest-hud | Presence House drawer + installer | **in progress** — shell + honesty + icon shipped; Velopack update toast open | 4.1 FED/BED (this wave); 4.2 OPS installer/update |
| PROP-5-host-sqlite-concurrency-ownership | Host SQLite concurrency + charter ownership + SoulLoop single-flight | **sent-to-pm** — TINA-main 2026-09-05 | — |
| PROP-6-desktop-drag-async-delay | Desktop drag Thread.Sleep → async delay | **sent-to-pm** — TINA-main 2026-09-05; parallel NOW w/ PROP-5 | — |
| PROP-7-hermes-dead-surface-cleanup | Remove live Hermes contracts/config/DI + docs honesty | **sent-to-pm** — TINA-main 2026-09-05; after PROP-5 | — |
| PROP-8-chat-orchestration-decomposition | ChatWebSocketHandler strangler + prompt builder + history + gated parallel reads | **sent-to-pm** — TINA-main 2026-09-05; after PROP-5; prefer after PROP-9 | — |
| PROP-9-host-di-composition-modules | Extract Program.cs DI into Add* modules | **sent-to-pm** — TINA-main 2026-09-05; after PROP-5 + PROP-7 | — |
| PROP-10-inference-clients-tools-split | Inference Clients vs Tools boundary | **sent-to-pm** — TINA-main 2026-09-05; after PROP-7 | — |
| PROP-11-memory-store-repository-split | Split SqliteMemoryStore into repos (one DB file) | **sent-to-pm** — TINA-main 2026-09-05; after PROP-5 | — |

Cluster map: `docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md`

Next free `N`: **12**.
