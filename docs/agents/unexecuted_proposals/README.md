# Unexecuted Proposals

Structured briefs produced by **TT-01 (Thinktank Facilitator)** before PM-01 execution planning.

## Purpose

- Park explored ideas that are **not yet** turned into execution tickets
- Keep a durable record of avenues, recommendations, risks, and open questions
- Optionally hand off to PM-01 without losing the thinktank work

## Filename + ID convention

| Artifact | ID | Filename |
| --- | --- | --- |
| Proposal (this folder) | **PROP-N** | kebab slug `.md` (not the ID) |
| TT → PM intake | **PROP-N** | `docs/agents/tasks/PROP-{N}-TT01-to-PM01.md` |
| PM → execution | **PROP-N.M** | `docs/agents/tasks/PROP-{N}.{M}-PM01-to-{ROLE}.md` |

**TT-01 never uses TASK-NNN.** PM assigns `.1`, `.2`, … when splitting a proposal.

### Registry (next unused integer)

| prop_id | Slug | Status |
| --- | --- | --- |
| PROP-1 | `victoria-reliable-workspace-browser.md` | sent-to-pm |
| PROP-2 | `victoria-digits-sms-channel.md` | sent-to-pm |
| PROP-3 | `victoria-ue-reliable-embodiment.md` | sent-to-pm |
| PROP-4 | `presence-shell-honest-hud.md` | sent-to-pm |

**Next TT assign: PROP-5.**

Older named ids (`PROP-COMPANION-01`, etc.) stay as historical; new work is integer **PROP-N**.

Slug from the **need or want** (lowercase kebab-case):

```text
export-chat-history-to-xlsx.md
reduce-pwa-login-latency.md
harden-multi-company-ai-isolation.md
```

If a slug exists, append `-2`, `-3`, â€¦ â€” do not overwrite without confirmation.

## Status values

| `status` | Meaning |
| --- | --- |
| `unexecuted` | Parked; thinktank done, not sent to PM |
| `sent-to-pm` | User opted to hand off; see `pm_intake` |
| `withdrawn` | Explicitly abandoned |

## Who writes here

- **TT-01** â€” primary author (idea exploration **and** PM unblock evals)
- **PM-01** â€” may annotate after intake; tickets execution from sent proposals; should not dump raw task tickets here
- Execution roles (FED/BED/â€¦) â€” do not write proposals here

## Unblock proposals

When PM sends `to-TT01` because a ticket cannot complete, TT writes a proposal here and **always** returns `PROP-{N}-TT01-to-PM01.md` so PM can re-ticket as `PROP-N.M`. See `Agents/PM-01-Work-Standards.md` §9.3.1.

## Related paths

| Path | Use |
| --- | --- |
| `docs/agents/tasks/` | PROP intakes + PROP-N.M (or legacy TASK) tickets |
| `docs/agents/reports/` | Role completion reports |
| `Agents/TT-01.md` | Thinktank role definition |
| `Agents/PM-01-EN.md` | PM receives sent proposals |
