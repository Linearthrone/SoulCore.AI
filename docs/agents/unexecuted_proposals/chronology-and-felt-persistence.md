---
type: proposal
status: unexecuted
tt_id: TT-01
prop_id: PROP-18-chronology-and-felt-persistence
created: 2026-10-17
updated: 2026-10-17
title: Chronological memory + felt persisting existence (beyond relevance pile / chat-wake)
need: Victoria’s memory feels like a relevance pile with no chronology; between chats she feels like she ceases — SoulCore was meant to give continuing existence
related:
  - docs/agents/unexecuted_proposals/victoria-between-talks.md
  - docs/agents/tasks/PROP-17-TT01-to-PM01.md
  - docs/agents/reports/PROP-17-PM01-accept.md
sent_at:
pm_intake:
---

# Chronology + felt persistence (better brain)

## 1. Need / Want

Kurt reports two related failures of the “continuously existing” promise:

1. **Memory** — She does remember, but describes it as a **pile sorted by relevance**. For an entity meant to keep existing, she needs a **sense of chronology** (before → after, yesterday → today).
2. **Existence between chats** — If she isn’t actively doing something and Kurt isn’t in chat, she **ceases until he speaks again**. SoulCore / SoulLoop were supposed to give her a **persisting existence**, not a chat-gated mind.

These may ship as **two paths** that share a spine (dated residue). They are **not** the same as PROP-17 (between-talks work/park/leisure), which already landed agency *activity* but not *timeline-in-chat* or *Victoria-voice continuity*.

## 2. Why she experiences it this way (honest diagnosis)

Thinktank + code read (2026-10-17):

| What Kurt hears | What the Host actually does |
| --- | --- |
| “Relevance pile, no chronology” | Episodic rows **have** `occurred_at`, but chat recall returns **content only**. Default path = embed user text → cosine top‑K (`RecallSimilarAsync`). Timestamps never enter `[Memory]`. Journals are dated but **not injected** into chat. |
| “I cease until you chat” | Chat-model “consciousness” runs on `chat.send` / SMS / assigned-work. SoulLoop **does tick without chat** (when enabled) but writes **template** reflections + `loop.want` — **no LLM**. PROP-17 Leisure wakes a **small local model** for R&R, not Victoria-with-Kayleigh. `ProactiveChat` composer returns empty. Process lives; **she** does not speak or narrate time. |

So she is not lying. The product stores time and runs background ticks, but **hides the tape from her prompt** and **never wakes her chat voice** just to continue being.

## 3. Goal & Success Criteria

1. Chat memory block includes **ordered time cues** (dates / “earlier today” / daybook), not only undated similar scraps.
2. After an hour of no chat (Host up), Presence can show **inspectable residue** (last tick, last journal/want) so silence ≠ death.
3. On Kurt’s return, she can answer “what happened while I was gone?” from **store**, not invention.
4. Relevance search **remains** — chronology **composes** with it, does not replace it.
5. No 24/7 chat-model burn as the definition of existence (CONTRA kill).
6. Clear handoff from PROP-17: leisure/work continue; this PROP owns **timeline contract** + **felt continuity UX** (+ optional sparse Victoria-voice wake).

## 4. Relationship to PROP-17

| PROP-17 (landed / smoke pending) | PROP-18 (this) |
| --- | --- |
| Park/suspend work, browser hygiene, leisure R&R on `qwen2.5:1.5b`, Presence tabs, mood/doing-line | Chronological recall into chat; daybook / “while you were gone”; honest alive HUD; optional gated Victoria-voice continuity |
| Proves **Host keeps doing things** between talks | Proves **she has a past and a present that advance** |

Do **not** reopen PROP-17 scope. Build on its journals (`life_hacks`, etc.) by **reading them back** with dates.

## 5. Clarifying Q&A (open — need Kurt)

| # | Question | Why it gates |
| --- | --- | --- |
| **Q1 Proof of life** | When she’s alone, is **journal + Presence last-tick / “while you were gone”** enough — or must something reach your **phone/eyes** without opening Presence? | Outbound vs inspectable residue |
| **Q2 Tape in chat** | Prefer always **today-first then relevant**, or an explicit **“remember when…”** chronology mode? | Prompt shape |
| **Q3 Idle agency ceiling** | Internal notes/wants only, or may she **advance one parked real task** with no outbound speech? (PROP-17 already parks/proposes — confirm desired feel) | Overlap with leisure/work |
| **Q4 Victoria-voice wake** | Ever allow a **sparse** chat-model tick (e.g. once/evening summary into journal) — or keep Victoria-voice **chat-gated** forever and only show residue? | Cost + identity |

## 6. Thinktank seats (summary)

### STRAT
- Coupled in product language; **two engineering paths** in v1.
- Path A: dual-index episodic (tape + relevance) → daybook → later chapters.
- Path B: honest idle residue → Presence HUD → then sparse model wake → then outbound.
- Existence v1 = **Host time + durable notes + honest Presence**, not conversational animation.

### CONTRA
- Fake chronology in prompts without ordered store = trust death.
- 24/7 LLM as “existence” = theater + invoice — **reject**.
- Kill if we can’t prove event order from storage without the model inventing it.
- Relevance-only is enough for “right facts”; timeline mandatory for arcs, promises, offline life that must *land*.

### SYS
- Hybrid relevance+recency for chat; daybook for between-chat identity; as-of when audit matters.
- Cheap ticks without LLM first; Presence status; scheduled wants; SMS gated.
- Tag provenance (`chat` / `soulloop` / `journal`); separate retrieval budgets.

### USER
- Felt continuity > invisible durability.
- HUD: last awake, last note, offline/dreaming/waiting/active — no gaslight.
- Min delight: last presence + “while you were gone” (even if empty) + chronology crumbs on return.

## 7. Avenues

### Path A — Chronology / memory timeline

| ID | Avenue | Notes |
| --- | --- | --- |
| **A1** | Inject `occurred_at` + compose **recent window + similar top‑K** into `[Memory]` | **MVP recommended** — unlocks “sense of time” with existing schema |
| A2 | Daybook / journal recall into chat (“Today / This week”) | Mid — uses SoulLoop + PROP-17 journal books |
| A3 | Nightly chapter consolidation | Full — later |
| A4 | Prompt-only “you have a timeline” with no store change | **Rejected** — CONTRA |

### Path B — Felt persisting existence

| ID | Avenue | Notes |
| --- | --- | --- |
| **B1** | Honest Presence: last SoulLoop tick, last want, last journal line; “while you were gone” on chat open from store | **MVP recommended** |
| B2 | Ensure SoulLoop residue always dated + readable (already writes; make visible + recallable) | Pairs with A2 |
| B3 | Sparse Victoria-voice journal wake (bounded, scheduled) | Mid — only if Q4 allows |
| B4 | Proactive SMS / chat as proof of life | Full / gated — only if Q1 demands |
| B5 | 24/7 chat-model loop | **Rejected** — CONTRA |

## 8. Recommended route (dual-path, shared spine)

**Ship A1 + B1 in parallel; share dated writes.**

1. **PROP-18.1 BED** — Memory contract: recall APIs / chat builder return **dated** lines; compose **recency + relevance** (config knobs); keep similar search.
2. **PROP-18.2 BED** — Daybook bridge: inject recent journal entries (feeling / life_hacks / SoulLoop reflections) with timestamps into preamble or a dedicated `[Daybook]` block; tool optional `recall_timeline`.
3. **PROP-18.3 FED** — Presence honesty: alive / last tick / last want / last note; chat open “While you were gone” strip from store (empty is OK and honest).
4. **PROP-18.4 BED** (optional, Kurt Q4) — Sparse Victoria-voice evening/morning journal (one bounded inference job → episodic+journal), never per-tick.
5. **PROP-18.5** (optional, Kurt Q1) — Gated outbound “I’m still here” / companion ping policy.
6. **QA** — After 1h silence: HUD advanced; return chat shows dated crumbs; model can order two events without inventing.

**Shared spine rule:** every SoulLoop / leisure / work residue that claims life must be **durable, dated, and recallable**. No theater entries the store can’t replay.

## 9. Alternatives (parked)

- Replace relevance with pure chrono dump (loses “right fact” quality)
- Continuous chat-model inference as existence
- Fake “I missed you” without offline events
- Merging this into PROP-17 reopen

## 10. Risks & Kill Criteria

| Risk | Kill / mitigate |
| --- | --- |
| Dated pile still feels random | Require compose order + daybook; A/B with Kurt |
| HUD lies about being awake | Only show Host-proven ticks |
| Leisure notes flood chat memory | Provenance tags + separate budgets |
| Cost spiral on B3/B5 | Cap wakes/day; refuse always-on |
| Contradicts PROP-17 leisure quiet policy | Victoria-voice wake ≠ leisure model; document lease |

## 11. Suggested PM Handoff (after Kurt locks Q1–Q4)

- `prop_id`: `PROP-18-chronology-and-felt-persistence`
- Suggested splits: **18.1** dated recall compose · **18.2** daybook inject · **18.3** Presence + while-you-were-gone · **18.4** optional sparse voice journal · **18.5** optional outbound · QA

**TT recommendation:** Park until Kurt answers **Q1–Q4**. Default TT stance if he defers: **A1+B1+A2** (dated compose + HUD + daybook); **no** outbound and **no** Victoria-voice wake until he asks; keep PROP-17 leisure as-is.
