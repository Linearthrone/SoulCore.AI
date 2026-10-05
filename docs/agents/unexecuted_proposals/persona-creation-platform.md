---
type: proposal
status: unexecuted
tt_id: TT-01
prop_id: PROP-15-persona-creation-platform
created: 2026-10-05
updated: 2026-10-05
title: Fork-ready persona creation platform — templates, trait scales, individual brains
need: Fork SoulCore, strip Victoria-as-product, build a comprehensive persona creation workflow with templates, trait sliding scales, and clear per-persona “brains”
related:
  - docs/agents/unexecuted_proposals/victoria-browser-live-cursor-pane.md
  - docs/agents/tasks/PROP-14-TT01-to-PM01.md
  - Agents/AGENTS.md
sent_at:
pm_intake:
---

# Persona creation platform (Victoria strip + multi-brain)

## 1. Need / Want

Kurt wants to:

1. **Fork** this project.
2. Take **everything Victoria** out of the default product.
3. Build a **comprehensive persona creation workflow** with:
   - **Templates**
   - **Sliding scales** on traits / attributes
   - **Clear divisions** — individual **“brains”** for each persona / entity

Today SoulCore is a **Victoria appliance** (Kayleigh address lock, House Victoria Presence, `victoria-*` paths/stores, SMS “You are Victoria…”). The runtime core (charter table, memory, SoulLoop, tool loop, Presence shell) is *structurally* reusable; model-facing and brand surfaces are not.

## 2. Goal & Success Criteria

1. A forkable **persona platform** where Victoria is an **optional sample pack**, not the product spine.
2. Operator can create a persona from a **template**, tune **trait scales**, and get a **distinct brain** (prompt + memory + session isolation).
3. Zero Victoria/Kayleigh strings required to run desk chat + memory + SoulLoop for a custom persona.
4. Trait changes produce **detectable** behavior difference (not prompt mush).
5. No cross-persona episodic bleed by default.
6. House Victoria branding, Android package, UE Kayleigh possess, and PROP-14 HWND Victoria chrome are **out of the default fork** (or gated as optional packs).

## 3. Context & Constraints (coupling inventory — condensed)

| Layer | Victoria-hard | Already generic |
| --- | --- | --- |
| Identity prompts | `ChatContextBuilder`, SMS preamble, `QuotedChatText`, episodic authoring | Charter kinds / `CharterService` |
| Stores | `IVictoria*`, `victoria_*` tables | Memory/episodic APIs structurally OK |
| Config | `Companion:DefaultContact*`, `victoria-browser`, `victoria-sandbox` | `contactId` stub for future persona service |
| UI | House Victoria Presence, Victoria Link Android | Presence layout / WS client |
| Embodiment | UE Victoria/Kayleigh, `victoria_eye_capture` | Optional body module |

Full inventory lived in TT session explore (2026-10-05); seam line = **identity/config pack + prompt injection**, not Host rewrite.

## 4. Clarifying Q&A (open — need Kurt)

| # | Question | Why it gates the route |
| --- | --- | --- |
| **Q1** | **Fork artifact:** clean **new public/private repo** (Victoria stripped), or **in-tree platform mode** with Victoria as one pack on this repo? | Decides git strategy, branding cut, and whether PROP-14 keeps shipping on main |
| **Q2** | **Concurrency:** one **active** persona at a time in v1, or multiple brains live together? | Process/HWND/browser cost; SYS says one-at-a-time until proven |
| **Q3** | **Memory boundary:** absolute quarantine, or shared “house” layer + private brains? | Schema and SoulLoop design |
| **Q4** | **Who creates personas?** Kurt only (ops/config), or in-app wizard in Presence? | FED scope; CONTRA wants thin surface first |
| **Q5** | **Keep** browser tools / VM sandbox / SMS / UE in the fork MVP, or **chat+memory+charter+SoulLoop only** first? | Excision blast radius |

## 5. Thinktank seats (summary)

### STRAT
- Frame as **persona platform**, Victoria = optional pack.
- Routes: **A** thin config multi-persona → **B** pack + DB partition → **C** process-per-brain.
- **Recommend B staged from A’s UX** — templates/sliders first, pack-scoped data isolation from day one; C later.

### CONTRA
- High risk of rename-and-UI + prompt soup.
- Kill if one non-Victoria persona cannot run E2E on Host (chat+memory, no Unreal).
- Kill if trait A/B is undetectable.
- **Dissent big rewrite** — demand thin fork proof first.
- Prefer **few discrete trait bands** over many float axes.

### SYS
- Pack + partition feasible on current Host; process-per-brain multiplies Playwright/VM/HWND pain.
- Prefer **`persona_id` columns + one SQLite** for MVP; separate `.db` later.
- Traits → **named directive blocks** with budgets (identity / voice / boundaries / recall bias); boundaries beat voice.
- Presence HWND can keep Victoria *names* briefly; **must** resolve window title / profile dir via `personaId` before a second brain.

## 6. Avenues

| ID | Avenue | Notes |
| --- | --- | --- |
| **A** | Config-first multi-persona (row `persona_id`, shared DB) | Fast UX; weak mental model of “brain” |
| **B** | Persona Pack + partitioned memory (+ template/slider workflow) | **Recommended** — matches “individual brains” without C’s ops |
| **C** | Orchestrator + worker process per persona | Strong isolation; premature for v1 |
| **D** | Big-bang rewrite “persona OS” | **Rejected** — CONTRA kill |
| **E** | Cosmetic rename only | **Rejected** — fails strip + creation workflow |

## 7. Recommended route

**Platform extract → Persona Pack + creation workflow (B), UX from A, isolation proof before C.**

### Phase 0 — Fork / freeze contract
- Decide Q1 (new repo vs in-tree).
- Freeze: which mainline props (e.g. PROP-14) stay on House Victoria line vs land in platform fork.
- Strip default brand: Companion/SMS/desk identity strings → placeholders; ship **no** Victoria soul import by default.

### Phase 1 — Identity knobs + one custom brain (proof)
- `PersonaOptions` / pack: `AssistantName`, `HumanAddressName`, `ContactId`, browser profile dir, sandbox title.
- Inject into `ChatContextBuilder`, SMS preamble, episodic / quote / tool guidance.
- Create persona **P** ≠ Victoria; chat + memory + charter + SoulLoop work; Victoria optional.

### Phase 2 — Creation workflow
- **Templates** (e.g. Companion, Coach, Operator-aide, Blank) as Persona Packs.
- **Trait scales** → discrete bands → compiled directive blocks (not raw floats in prose).
- In-app or ops wizard (Q4): name, template, sliders, boundaries, save pack.
- Memory reads/writes scoped by `persona_id`.

### Phase 3 — Clear brain divisions
- Session + Presence + browser profile keyed to active persona.
- No cross-persona episodic read by default (Q3 may add shared house tier later).
- Export/import pack (prompt + traits + charter anchors + optional theme).

### Phase 4 — Optional hardening
- Process-per-brain / per-persona DB file if Q2 concurrency or untrusted packs demand it.
- Embodiment / UE / Link Android as **separate packs**, not MVP spine.

**Do not** ship Character.AI marketplace, council-of-minds, or Unreal multi-avatar in v1.

## 8. Alternatives (parked)

- Process-per-brain as day-one definition of “brain”
- Cosmetic Victoria rename without pack model
- Trait float soup without compile + eval

## 9. Risks & Kill Criteria

| Risk | Kill / mitigate |
| --- | --- |
| Excision never finishes; Victoria ghosts in prompts | Kill platform claim until Phase 1 E2E green for non-Victoria P |
| Sliders don’t change behavior | Kill slider count; require blind A/B on 3–5 bands |
| Second persona steals wrong HWND/profile | Gate multi-brain behind persona→path/title resolver |
| Fork bitrots vs main | Freeze contract + cherry-pick policy in Phase 0 |
| Scope swallows PROP-14 / SMS / UE | MVP = chat+memory+charter+SoulLoop unless Q5 expands |

## 10. Suggested PM Handoff (after Kurt locks Q1–Q5)

- `prop_id`: `PROP-15-persona-creation-platform`
- Suggested splits:
  - **15.0** PM — fork/freeze contract (repo strategy, Victoria line vs platform line)
  - **15.1** BED — `PersonaOptions` + prompt injection; Victoria strings out of runtime defaults
  - **15.2** BED — `persona_id` on memory/charter/emotion paths; no cross-read
  - **15.3** BED+FED — template gallery + trait-band compile + creation wizard
  - **15.4** FED — Presence active-persona switcher + de-brand shell for platform mode
  - **15.5** OPS — sample packs (Blank + 2 templates); optional Victoria pack **not** default
  - **15.6** QA — custom persona E2E + trait A/B smoke
  - Later: browser/VM/SMS/UE packs; process isolation if needed

**TT recommendation:** Park until Kurt answers **Q1–Q5**. Default TT stance if he defers: **new fork repo**, **one active persona at a time**, **absolute memory quarantine**, **in-app wizard after ops/config proof**, **chat+memory+charter+SoulLoop MVP** (tools/browser as follow-on packs).
