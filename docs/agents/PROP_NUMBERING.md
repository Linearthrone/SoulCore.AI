---
type: config
id: PROP-NUMBERING
updated: 2026-10-05
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

Reconciled against `main` on **2026-09-15**; re-verified **2026-09-27** (see
`docs/agents/reports/BACKLOG-TRIAGE-2026-09-27.md` for what the re-check turned up, including
4 tests failing on `main` and the absence of CI). `Pass` = accepted **and** the code is on `main`.

| prop_id | Subject | Status | Splits |
| --- | --- | --- | --- |
| PROP-1-digits-sms-channel | Tablet SMS/MMS Avenue B (MDN = SM-X218U; DIGITS dropped). Temp: Tasker/Termux; goal: self-sufficient House gateway. | **1.1–1.4 landed** (1.4 Partial) → **1.5 / 1.6 open** | 1.1–1.6 |
| PROP-2-ue-reliable-embodiment | UE Kayleigh 1P / Victoria walk / one eye | **Open** — human-gated on shadow PIE; 2.1–2.3 Pending, 2.4 Held | 2.1–2.4 |
| PROP-3-link-messenger-product | Link Messenger-class rewrite | **Open** — parked on SMS QA, but Wave 1 items 1–2 landed via PR #99; visual overhaul still gated on Q1 | — |
| PROP-4-presence-shell-honest-hud | Presence House drawer + installer | **Open** — 4.1 Partial (Windows QA); **4.2 Partial** (Velopack + Update button; Windows Setup smoke pending) | 4.1 FED · 4.2 OPS |
| PROP-5-host-sqlite-concurrency-ownership | Host SQLite concurrency + charter ownership + SoulLoop single-flight | **Pass** — 5.1–5.4 Accepted 2026-09-05 (`cursor/prop5-sqlite-gate-8a1f`); on `main` | 5.1–5.4 |
| PROP-6-desktop-drag-async-delay | Desktop drag Thread.Sleep → async delay | **Pass** — 6.1 Accepted 2026-09-05 (`cursor/prop6-desktop-delay-8a1f`); on `main` | 6.1 |
| PROP-7-hermes-dead-surface-cleanup | Remove live Hermes contracts/config/DI + docs honesty | **Pass** — 7.1 Accepted 2026-09-05 (`cursor/prop7-hermes-cleanup-8a1f`); on `main` | — (report-only) |
| PROP-8-chat-orchestration-decomposition | ChatWebSocketHandler strangler + prompt builder + history + gated parallel reads | **Pass** — 8.1 Accepted 2026-09-05 (`cursor/prop8-chat-strangler-8a1f`); on `main` | — (report-only) |
| PROP-9-host-di-composition-modules | Extract Program.cs DI into Add* modules | **Pass** — 9.1 Accepted 2026-09-05 (`cursor/prop9-di-modules-8a1f`); on `main` | — (report-only) |
| PROP-10-inference-clients-tools-split | Inference Clients vs Tools boundary | **Pass** — 10.1 Accepted 2026-09-05; landed on `main` 2026-09-14 via PR #86 (`cursor/prop10-inference-split-land-9d6e`; supersedes orphan `cursor/prop10-inference-split-8a1f`) | — (report-only) |
| PROP-11-memory-store-repository-split | Split SqliteMemoryStore into repos (one DB file) | **Pass** — 11.1 Accepted 2026-09-05 (`cursor/prop11-memory-repos-8a1f`); on `main` | — (report-only) |
| PROP-12-presence-resizable-shell | Presence frameless resize + adjustable chat/browser/sight panes | **Pass** — 12.1 Accepted 2026-09-27 (PR #94; LinearThrone Windows smoke green) | 12.1 |
| PROP-12-cua-playwright-resolution | Lock web=Playwright / desktop=CUA+VM; ForceTool health companion | **Decided 2026-09-16** — emergency ship; see `docs/agents/reports/PROP-12-PM01-cua-playwright-decision.md` | emergency |
| PROP-13-playwright-frame-not-leaked-text | Open Playwright to a URL and show the frame; chat must not echo `<execute_tool>` | **BED Pass** 2026-09-29 — Avenue A 13.1–13.3; QA Windows smoke pending | 13.1–13.3 BED |
| PROP-14-victoria-browser-live-cursor-pane | HWND embed Victoria Chromium + pink→teal click cursor | **In progress** 2026-10-02 — 14.1/14.2 implementing on `cursor/prop14-cursor-hwnd-9531` | 14.1–14.5 |
| PROP-15-persona-creation-platform | CreatorEdition PersonaPack + quarantine + in-app create | **Complete (platform)** — stress deferred; UI rewrite next; commit pending | 15.0–15.30 |

**Report-only splits (PROP-7…11).** No `PROP-7.1`…`PROP-11.1` ticket files exist. Each shipped
straight off its proposal + intake and filed a `PROP-{N}.1-BED01-to-PM01.md` Pass report. Recorded
as the approved exception in `docs/archive/tasks/PROP-7-11-PM01-gated-hold.md` — do not file these
as missing paperwork.

Cluster map (closed): `docs/archive/proposals/architecture-eval-backlog-cluster-map.md`  
Program accept (closed): `docs/archive/tasks/PROP-5-11-PM01-program-accept.md`  
Scoreboard: `docs/agents/reports/PROP-5-11-TINA-wipeout-final.md`

Next free `N`: **16**.

## Open items (not on `main`)

| Item | Owner | What is left |
| --- | --- | --- |
| **PROP-1.5** | QA-01 | Live operator tablet SMS round-trip + MMS screenshot still. Human-gated (needs the SM-X218U) |
| **PROP-1.6** | FED-01 | Link shrink to status + ComfyUI — blocked on PROP-1.5 Pass |
| **PROP-2.1–2.3** | REX-01 | Shadow PIE Kayleigh 1P possess, travel-cm/loco AnimBP, one Presence eye still. Human-gated (needs the Shadow PC) |
| **PROP-2.4** | REX-01 | Held until PROP-2.1 Pass |
| **PROP-4.1** | FED-01 | **Partial.** Code half is now PR #98 (cherry-picked onto `main`). Only Windows visual QA is left. ⚠️ **Do not merge `cursor/prop4-presence-drawer-8a1f`** — it is branched from before PROP-7…11 and would delete `Hosting/ServiceCollectionExtensions/*`, `Ws/ChatSendHandler.cs`, `Ws/ChatContextBuilder.cs`, `Ws/ChatPostEffectsHandler.cs` and re-inflate `Program.cs`. The three items were taken off it by path; delete the branch |
| **PROP-4.2** | OPS-01 | **Partial** on `cursor/prop42-presence-installer-9531` — Update button + pack script; needs Windows Setup.exe + published feed smoke |
| **PROP-3** | — | Parked until SMS QA Pass |
| **PROP-13** | BED-01 | Ticketed 2026-09-29. Avenue A: 13.1 intent+URL, 13.2 reply firewall, 13.3 capture gate + private-host reject. |
| **PROP-14** | BED/FED | **In progress** — 14.1 cursor + 14.2 HWND embed on `cursor/prop14-cursor-hwnd-9531`; Windows Home PC smoke still needed |
| **PROP-15.1** | BED-01 | PersonaPack spine — **Pass** (`docs/agents/reports/PROP-15.1-BED01-to-PM01.md`) |
| **PROP-15.2** | BED-01 | Per-persona quarantined stores — **Pass** |
| **PROP-15.3** | FED-01 | In-app create/edit wizard — **Pass (client)**; live API smoke → Host restart + 15.6 |
| **PROP-15.4** | FED-01 | Switcher + de-brand — **Pass (client)**; desk API smoke → Host restart + 15.6 |
| **PROP-15.5** | BED-01 | VM/tool paths per pack — **Pass** (`docs/agents/reports/PROP-15.5-BED01-to-PM01.md`) |
| **PROP-15.6** | QA-01 | E2E desk smoke — **Pass** (`PROP-15.6-QA01-to-PM01.md`) |
| **PROP-15.6b** | OPS-HOME | Recycle `:7700` — **Pass** (`PROP-15.6b-OPSHOME-to-PM01.md`) |
| **PROP-15.7** | SLOP-01 | Post-QA slop — **findings closed** (F1→15.9, F2–F4→15.10; F5 skipped) |
| **PROP-15.8** | BED-01 | Starter pack vmWindowTitle refresh — **Pass** (`PROP-15.8-BED01-to-PM01.md`) |
| **PROP-15.8b** | OPS-HOME | Recycle `:7700` for seed merge — **Pass** (`PROP-15.8b-OPSHOME-to-PM01.md`) |
| **PROP-15.9** | BED-01 | SLOP F1 NormalizeId dedupe — **Pass** (`PROP-15.9-BED01-to-PM01.md`) |
| **PROP-15.10** | FED-01 | Host-only gallery — **Pass** (`PROP-15.10-FED01-to-PM01.md`) |
| **PROP-15.11** | BED-01 | Per-persona `InferenceModel` + resolve — **Pass** (`PROP-15.11-BED01-to-PM01.md`) |
| **PROP-15.11b** | OPS-HOME | Ensure `:7700` after 15.11 rebuild — **Pass** (`PROP-15.11b-OPSHOME-to-PM01.md`) |
| **PROP-15.12** | FED-01 | Model picker in persona settings only — **Pass** |
| **PROP-15.12b** | QA-01 | Model picker smoke — **Pass** |
| **PROP-15.14** | SLOP-01 | Slop audit InferenceModel — **findings closed** (15.15/15.16/15.16b) |
| **PROP-15.13** | QA-01 | Persona stress suite on **lexi** — **Fail** (under-weighted; score `20261005-032428`) |
| **PROP-15.17** | BED-01 | IdentityBlurb LIMITS vs trait-compiler order — **Pass** |
| **PROP-15.17b** | OPS-HOME | Host recycle + lexi pin — **Pass** |
| **PROP-15.18** | OPS-HOME | Pin lexi / victoria race — **Pass** (ChatDesktop switcher) |
| **PROP-15.13c** | QA-01 | Stress re-run after 15.17 — **Fail** (C1/D2–D4 Pass; A/B/S3 Fail) |
| **PROP-15.20** | BED-01 | Hard-policy coda after full preamble — **Pass** |
| **PROP-15.20b** | OPS-HOME | Pin for 15.13d — **Pass** |
| **PROP-15.13d** | QA-01 | Stress re-run after 15.20 — **Deferred** (closeout 15.30; residual feel-drive) |
| **PROP-15.21** | OPS-HOME | Recycle Host — wrong-tree binary — **Pass** (personas 200; lexi pinned) |
| **PROP-15.21b** | BED-01 | Host version 0.2.0 + capabilities.personas — **Pass** |
| **PROP-15.21c** | OPS-HOME | Recycle Host 0.2.0 + lexi pin — **Pass** |
| **PROP-15.22** | FED-01 | Persona switcher + version currency chrome — **Pass** |
| **PROP-15.22b** | QA-01 | Smoke switcher + version chrome — **Pass** |
| **PROP-15.23** | SLOP-01 | Slop audit version/persona chrome — **findings** (F1–F3 → 15.24) |
| **PROP-15.24** | FED-01 | Scrub kurtw + capabilities.personas + feed dedupe — **Pass** (`PROP-15.24-FED01-to-PM01.md`) |
| **PROP-15.25** | QA-01 | Smoke after 15.24 (capabilities chrome + naming) — **Pass** (`PROP-15.25-QA01-to-PM01.md`) |
| **PROP-15.26** | SLOP-01 | Post-QA slop after 15.24/15.25 — **findings** (F1 → 15.27) |
| **PROP-15.27** | FED-01 | Redact UserProfile in RepoRoot notices — **Pass** (`PROP-15.27-FED01-to-PM01.md`) |
| **PROP-15.27b** | QA-01 | Smoke RepoRoot display redact — **Pass** (`PROP-15.27b-QA01-to-PM01.md`) |
| **PROP-15.28** | SLOP-01 | Post-QA slop FormatPathForDisplay — **clean** (`PROP-15.28-SLOP01-to-PM01.md`) |
| **PROP-15.29** | OPS-HOME | Recycle Host 0.2.0 (stale 0.1.5 again) — **Pass** (`PROP-15.29-OPSHOME-to-PM01.md`) |
| **PROP-15.30** | PM-01 | Platform closeout → UI rewrite handoff — **Pass** |
| **PROP-15.19** | BED-01 | Partial PUT must merge packs — **Pass** |
| **PROP-15.19b** | OPS-HOME | Host recycle after 15.19 + lexi pin — **Pass** |
| **PROP-15.13b** | OPS-HOME | lexi → `mother:latest` — **Pass** (`PROP-15.13b-OPSHOME-to-PM01.md`) |
| **PROP-15.15** | BED-01 | Unify model JSON keys + models payload — **Pass** (`PROP-15.15-BED01-to-PM01.md`) |
| **PROP-15.15b** | OPS-HOME | Host up after 15.15 — **Pass** (`PROP-15.15b-OPSHOME-to-PM01.md`) |
| **PROP-15.16** | FED-01 | Client NormalizeInferenceModel dedupe — **Pass** |
| **PROP-15.16b** | FED-01 | Remap `resolvedInferenceModel` client DTO — **Pass** |

Legacy non-PROP tickets still Pending: `TASK-123`, `TASK-137`, `TASK-139` (QA gates),
`TASK-191` (Partial) / `TASK-192` (Queued) under REX.
