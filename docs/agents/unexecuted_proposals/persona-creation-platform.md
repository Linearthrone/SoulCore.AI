---
type: proposal
status: sent-to-pm
tt_id: TT-01
prop_id: PROP-15-persona-creation-platform
created: 2026-10-05
updated: 2026-10-05
title: CreatorEdition persona platform — in-app creation, trait scales, quarantined brains
need: On the CreatorEdition fork, strip Victoria-as-default and build in-app persona creation with templates, trait scales, total memory quarantine, and VM+tooling — one active brain now, multi later
related:
  - https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
  - docs/agents/unexecuted_proposals/victoria-browser-live-cursor-pane.md
  - Agents/AGENTS.md
sent_at: 2026-10-05
pm_intake: docs/agents/tasks/PROP-15-TT01-to-PM01.md
kurt_locks:
  fork_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
  concurrency_v1: single-active-persona
  concurrency_future: multi-simultaneous-framed
  memory: total-quarantine-per-persona
  shared_memory_later: mcp-shared-server
  creation_ui: in-app-wizard-and-live-adjust
  mvp_scope: chat-memory-charter-soulloop-vm-tooling
  locked_at: 2026-10-05
---

# CreatorEdition persona platform (Victoria strip + quarantined brains)

## 1. Need / Want

Kurt wants a **Creator Edition** of SoulCore where operators **create and tune personas in-app**, each with its own brain, templates, and trait scales — not a hardwired Victoria appliance.

## 2. Goal & Success Criteria

1. Execution target: **[House-VictoriAI/SoulCore.AI_CreatorEdition](https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition)** (fork exists). LinearThrone `SoulCore.AI` remains the House Victoria product line.
2. **In-app** create + edit: templates, trait sliding scales, charter/identity, active persona switch — adjustments live in the Presence/Creator UI.
3. **One active persona at a time** in v1; architecture **framed** for multiple simultaneous brains later (no dead-end singletons).
4. **Total memory quarantine** per persona (no shared episodic/charter/journal bleed). Future shared knowledge = **separate MCP shared-memory server**, not hole-punching quarantine.
5. MVP runtime for an active persona: **chat + memory + charter + SoulLoop + VM + tooling** (browser/desktop tool path included).
6. Zero Victoria/Kayleigh required as defaults to run CreatorEdition.
7. Trait changes produce detectable behavior (band-compiled directives, not float mush).

## 3. Context & Constraints

| Fact | Note |
| --- | --- |
| Fork | `House-VictoriAI/SoulCore.AI_CreatorEdition` — README already frames persistence + later VE/Metahuman body |
| Upstream | `Linearthrone/SoulCore.AI` — Victoria companion; PROP-14 etc. stay there unless explicitly ported |
| Coupling | Identity prompts, `IVictoria*`, `victoria-browser` / `victoria-sandbox`, House Victoria Presence brand |
| Generic seams | CharterService, Companion `contactId` stub, tool loop, Presence shell layout |

## 4. Clarifying Q&A — **LOCKED 2026-10-05**

| # | Kurt lock |
| --- | --- |
| **Q1 Fork** | Exists: `https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition`. All PROP-15 execution lands there. |
| **Q2 Concurrency** | **v1 = single active persona.** Frame APIs/session/Host for **future multi-simultaneous** brains (switcher + persona-scoped resources now; no concurrent HWND/VM required in v1). |
| **Q3 Memory** | **Total quarantine** per persona. Shared blocks later via **MCP shared-memory server** (group/specific knowledge) — not a shared house table in v1. |
| **Q4 Creation UI** | **In-app:** create persona + make adjustments in the app UI (not ops-only YAML). |
| **Q5 MVP scope** | **Chat, memory, charter, SoulLoop, and VM + tooling.** UE/Metahuman body stays “part two” per fork README unless PM expands. |

Answers are sufficient for send-to-PM.

## 5. Thinktank seats (summary)

- **STRAT:** Pack + partition; Victoria optional; stage UX then isolation.
- **CONTRA:** Kill if custom persona E2E fails; kill mushy sliders; no big-bang OS rewrite.
- **SYS:** One Host OK for single-active; **per-persona DB files** fit total quarantine better than shared-DB `persona_id` alone; resolve VM window title / Playwright profile via `personaId`; frame multi-active without implementing it.

## 6. Avenues

| ID | Avenue | Notes |
| --- | --- | --- |
| **B+** | Persona Pack + **per-persona store quarantine** + in-app creator + VM/tool paths scoped to active pack | **Recommended (locked)** |
| A | Shared DB + `persona_id` only | Too weak for “total quarantine”; may use as index *inside* a persona file, not across personas |
| C | Process-per-brain day one | Park until multi-simultaneous ships |
| D | Big-bang rewrite | **Rejected** |

## 7. Recommended route (CreatorEdition)

### Phase 0 — Fork contract
- PROP-15 tickets / PRs target **CreatorEdition** repo.
- Upstream Victoria line continues independently; cherry-pick policy documented (tools/Host fixes may port either way).
- Strip default Victoria brand/soul from CreatorEdition defaults.

### Phase 1 — Persona runtime spine
- `PersonaPack` / options: names, human address, contact id, **Playwright profile dir**, **VM/sandbox window title**, tool policy.
- Inject into `ChatContextBuilder`, tool guidance, episodic authoring.
- **Active persona** session: only one loaded; APIs take `personaId` so multi-active is a future call pattern, not a rewrite.
- Per-persona **SQLite (and any vector store) paths** — total quarantine.

### Phase 2 — In-app creation & adjustment UI
- Template gallery (Blank + 2–3 starters).
- Trait scales → discrete bands → compiled directive blocks (identity / voice / boundaries / recall bias).
- Screens: create wizard, edit traits/charter, switch active persona, confirm quarantine (no cross-read).
- Live adjust: changing scales/charter updates pack and next turn’s context (document whether mid-turn hot-reload).

### Phase 3 — VM + tooling per active brain
- Persona-scoped desktop/VM target + browser profile; no Victoria hardcodes.
- Tool loop / SoulLoop / charter tick only against **active** persona’s stores.
- Framing hooks for future: multiple packs “warm” but only one “active” until multi-simultaneous epic.

### Phase 4 — Future (out of v1 ship)
- Multi-simultaneous brains (N VMs / N profiles / N Presence panes or tabs).
- MCP **shared-memory server** for opt-in shared knowledge blocks.
- Metahuman / VE body (“part two”).

**Do not** punch holes in quarantine for convenience. **Do not** require Victoria pack to boot CreatorEdition.

## 8. Alternatives (parked)

- In-tree platform mode on LinearThrone main (superseded by existing fork)
- Shared house memory table in v1
- Ops-only persona YAML without UI
- Chat-only MVP without VM/tooling (superseded by Kurt lock)

## 9. Risks & Kill Criteria

| Risk | Kill / mitigate |
| --- | --- |
| Work lands on wrong repo | PM tickets must cite CreatorEdition; refuse LinearThrone-only PRs for 15.x |
| Quarantine leak via global singleton stores | Kill Pass until dual-persona switch proves zero cross-read |
| Trait mush | Cap bands; A/B smoke required |
| Single-active painted into undoing multi later | Every store/tool path keyed by `personaId` from Phase 1 |
| VM/HWND still Victoria-named | Persona→title/profile resolver before second pack can activate |
| Scope creep into Metahuman | Hold body to Phase 4 / separate PROP |

## 10. Suggested PM Handoff

- `prop_id`: `PROP-15-persona-creation-platform`
- **Repo:** `House-VictoriAI/SoulCore.AI_CreatorEdition`
- Suggested splits:
  - **15.0** PM — CreatorEdition freeze + upstream cherry-pick notes; strip Victoria defaults checklist
  - **15.1** BED — PersonaPack + prompt/tool injection; `personaId` on session; single-active loader framed for multi
  - **15.2** BED — **Per-persona quarantined stores** (DB/vector paths); switch active proves no cross-read
  - **15.3** FED+BED — In-app create/edit wizard: templates, trait scales, charter editor
  - **15.4** FED — Active-persona switcher + CreatorEdition shell de-brand
  - **15.5** BED+OPS — VM + tooling paths per pack (profile dir, sandbox title); desk smoke
  - **15.6** QA — Create persona ≠ Victoria; quarantine test; trait A/B; VM tool path
  - **Later** — multi-simultaneous; MCP shared-memory server; Metahuman body

**TT recommendation:** Sent to PM-01 2026-10-05. Execution on **CreatorEdition**. Intake: `docs/agents/tasks/PROP-15-TT01-to-PM01.md`.
