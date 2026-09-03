# Unexecuted Proposals

Structured briefs produced by **TT-01 (Thinktank Facilitator)** before PM-01 execution planning.

## Purpose

- Park explored ideas that are **not yet** turned into execution tickets
- Keep a durable record of avenues, recommendations, risks, and open questions
- Hand off to PM-01 without losing the thinktank work

## Filename + ID convention

| Artifact | ID | Filename |
| --- | --- | --- |
| Proposal (this folder) | **PROP-N** | kebab slug `.md` (not the ID) |
| TT → PM intake | **PROP-N** | `docs/agents/tasks/PROP-{N}-TT01-to-PM01.md` |
| PM → execution | **PROP-N.M** | `docs/agents/tasks/PROP-{N}.{M}-PM01-to-{ROLE}.md` |

**TT-01 never uses TASK-NNN.** PM assigns `.1`, `.2`, … when splitting a proposal.

### Registry

**Single source of truth:** [`../PROP_NUMBERING.md`](../PROP_NUMBERING.md)

Do **not** maintain a second PROP table here — it drifts. Next free `N` and active subjects live only in `PROP_NUMBERING.md`.

Slug from the **need or want** (lowercase kebab-case):

```text
presence-shell-honest-hud.md
victoria-link-messenger-product.md
```

Ticketed proposals live under `docs/archive/proposals/` once accepted.

Frontmatter **must** include `prop_id: PROP-{N}-{subject}` once numbered.

If a slug exists, append `-2`, `-3`, … — do not overwrite without confirmation.

## Status values

| `status` | Meaning |
| --- | --- |
| `unexecuted` | Parked; thinktank done, not sent to PM |
| `sent-to-pm` | User opted to hand off; see `pm_intake` / `prop_id` |
| `accepted-pm-ticketed` | PM accepted; splits live as `PROP-N.M` |
| `parked-pending-digits-pass` | Explicit hold (e.g. Link rewrite) |
| `withdrawn` | Explicitly abandoned |

## Who writes here

- **TT-01** — primary author (idea exploration **and** PM unblock evals)
- **PM-01** — may annotate after intake; tickets execution from sent proposals; should not dump raw task tickets here
- Execution roles (FED/BED/…) — do not write proposals here

## Unblock proposals

When PM sends `to-TT01` because a ticket cannot complete, TT writes a proposal here and **always** returns `PROP-{N}-TT01-to-PM01.md` so PM can re-ticket as `PROP-N.M`. See `Agents/PM-01-Work-Standards.md` §9.3.1.

## Related paths

| Path | Use |
| --- | --- |
| `docs/agents/PROP_NUMBERING.md` | PROP registry + rules (canonical) |
| `docs/agents/tasks/` | Active execution tickets |
| `docs/archive/tasks/` | Completed / superseded tickets |
| `docs/agents/reports/` | Active role completion reports |
| `docs/handbook/` | Architecture source of truth (not tickets) |
| `Agents/TT-01.md` | Thinktank role definition |
| `Agents/PM-01-EN.md` | PM receives sent proposals (`PM-01.md` is a stub → EN) |
