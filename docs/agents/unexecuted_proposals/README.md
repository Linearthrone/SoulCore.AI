# Unexecuted Proposals

Structured briefs produced by **TT-01 (Thinktank Facilitator)** before PM-01 execution planning.

## Purpose

- Park explored ideas that are **not yet** turned into execution tickets
- Keep a durable record of avenues, recommendations, risks, and open questions
- Hand off to PM-01 without losing the thinktank work

## ID convention (2026-08-19+)

**Canonical:** [`docs/agents/PROP_NUMBERING.md`](../PROP_NUMBERING.md)

| Kind | ID | Example |
| --- | --- | --- |
| Proposal | `PROP-{N}-{subject}` | `PROP-1-digits-sms-channel` |
| Split task | `PROP-{N}.{M}` | `PROP-1.1` (OPS kill-test) |

TT **does not** assign colliding `TASK-###` numbers for new idea intakes. PM may adjust `.M` splits and roles.

## Filename convention (slug file)

Slug from the **need or want** (lowercase kebab-case):

```text
victoria-digits-sms-channel.md
victoria-ue-reliable-embodiment.md
```

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

When PM sends `to-TT01` because a ticket cannot complete, TT writes a proposal here (new `PROP-N` if it is a new brief) and **always** returns a PM intake so PM can re-ticket. See `Agents/PM-01-Work-Standards.md` §9.3.1.

## Related paths

| Path | Use |
| --- | --- |
| `docs/agents/PROP_NUMBERING.md` | PROP registry + rules |
| `docs/agents/tasks/` | Execution tickets (`PROP-N.M-…` or legacy `TASK-…`) |
| `docs/agents/reports/` | Role completion reports |
| `Agents/TT-01.md` | Thinktank role definition |
| `Agents/PM-01-EN.md` | PM receives sent proposals |
