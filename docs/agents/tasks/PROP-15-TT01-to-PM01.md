---
type: intake
prop_id: PROP-15-persona-creation-platform
from: TT-01
to: PM-01
mode: idea
status: Sent — awaiting PM ticketing
created: 2026-10-05
updated: 2026-10-05
proposal: docs/agents/unexecuted_proposals/persona-creation-platform.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
upstream_repo: https://github.com/Linearthrone/SoulCore.AI
pm_tickets:
---

# PROP-15 — CreatorEdition persona platform (quarantined brains + in-app creation)

## Proposal

`docs/agents/unexecuted_proposals/persona-creation-platform.md` (tracked on LinearThrone; **execute on CreatorEdition**)

Mode: **idea**

## Execution repo (mandatory)

**All PROP-15.x tickets and PRs land on:**  
[`House-VictoriAI/SoulCore.AI_CreatorEdition`](https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition)

LinearThrone `SoulCore.AI` remains the House Victoria product line. Do not implement 15.x against LinearThrone main unless PM explicitly ports a Host fix both ways.

## Summary

Strip Victoria-as-default on CreatorEdition. Ship an **in-app persona creation / adjustment UI** (templates + trait scales + charter) with **total memory quarantine** per persona, **one active persona at a time** (APIs framed for future multi-simultaneous), and MVP runtime covering **chat + memory + charter + SoulLoop + VM + tooling**. Shared knowledge later = **MCP shared-memory server**, not a shared house table. Metahuman/VE body stays part two.

## Decisions already accepted (Kurt 2026-10-05)

| Lock | Decision |
| --- | --- |
| Fork | `House-VictoriAI/SoulCore.AI_CreatorEdition` |
| Concurrency v1 | Single active persona |
| Concurrency future | Frame for multi-simultaneous (no dead-end singletons) |
| Memory | Total per-persona quarantine |
| Shared memory later | MCP shared-memory server |
| Creation UI | In-app create + live adjustments |
| MVP scope | Chat + memory + charter + SoulLoop + VM/tooling |

No product clarifying questions remain for PM.

## Suggested splits

PM may re-split / reassign. These are hints — **all on CreatorEdition**:

- `PROP-15.0` — PM01: CreatorEdition freeze + upstream cherry-pick notes; Victoria-defaults strip checklist.
- `PROP-15.1` — BED01: PersonaPack + prompt/tool injection; `personaId` on session; single-active loader framed for multi.
- `PROP-15.2` — BED01: Per-persona quarantined stores (DB/vector paths); dual-persona switch proves zero cross-read.
- `PROP-15.3` — FED01 + BED01: In-app create/edit wizard — templates, trait-band scales, charter editor.
- `PROP-15.4` — FED01: Active-persona switcher + CreatorEdition shell de-brand.
- `PROP-15.5` — BED01 + OPS01: VM + tooling paths per pack (profile dir, sandbox title); desk smoke.
- `PROP-15.6` — QA01: Create persona ≠ Victoria; quarantine test; trait A/B; VM tool path.

## Kill criteria

- Custom non-Victoria persona cannot run E2E on CreatorEdition Host (chat + memory + charter + SoulLoop + VM/tool).
- Quarantine leak across personas after switch.
- Trait scales produce no detectable behavior difference.
- Work merged only to LinearThrone and never to CreatorEdition.

Activate PM-01 to ticket on **CreatorEdition**. TT-01 does not write the execution tickets.
