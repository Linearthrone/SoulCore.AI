# Docs hygiene

## Active (source of truth)

| Path | Role |
| --- | --- |
| `docs/handbook/` | Architecture, modules, workflows, conventions |
| `docs/runbooks/` | Step-by-step ops procedures |
| `docs/agents/PROP_NUMBERING.md` | Only PROP registry |
| `docs/agents/tasks/` | **Active** execution tickets |
| `docs/agents/reports/` | **Active** role reports |
| `docs/agents/unexecuted_proposals/` | Parked / in-flight proposals |
| `docs-site/` | Searchable VitePress site (synced from handbook + runbooks) |

## Archive

| Path | Role |
| --- | --- |
| `docs/archive/issues/` | Closed / obsolete issues |
| `docs/archive/tasks/` | Done, Pass, or superseded tickets |
| `docs/archive/reports/` | Matching historical reports |
| `docs/archive/qa-harnesses/` | Ticket-scoped QA probe scripts (historical) |
| `docs/archive/roles/` | Obsolete role templates (cloud OPS-01-EN, etc.) |

Recover deleted dumps from git history: `docs/agents/log/` (353 historical copies), `reports/_*/` evidence folders, Hermes PreferHermes runbook, Host `artifacts/` publish trees.

## Rules

1. Architecture lives in the handbook — not in PRODUCT_ROOT or ticket prose.
2. One PROP table only (`PROP_NUMBERING.md`).
3. When a PROP split or TASK is Pass/Done/superseded, **move** it here; do not leave it in the active queue.
4. Never commit QA evidence logs or `artifacts/` bin dumps.
