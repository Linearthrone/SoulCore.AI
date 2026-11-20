---
type: task
prop_id: PROP-20.9
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: QA-01
priority: P0
status: Pending
created: 2026-11-17
title: Wave A regression — mind / memory / SMS gate / journals / identity
depends_on: PROP-20.1, PROP-20.2, PROP-20.3, PROP-20.4, PROP-20.5, PROP-20.6, PROP-20.7, PROP-20.8
updated: 2026-11-17
---

# PROP-20.9 — QA Wave A

**Activate:** `@Agents/QA-01.md`

## Scope (must evidence Pass/Fail — no silent skip)

1. **Unit/integration:** `dotnet test` filters covering `VictoriaMindBlockTests`, `ChatContextBuilderTests`, `MemoryRecallDatedTests`, `SqliteMemoryStoreJournalTests`, `SmsSecurityGateTests`, `SmsOutboundComposer` / `SmsInboundProp207` / SMS inbound+outbound suites, `Prop17LeisureTests`, ChatDesktop identity tests if present. Paste real output. Include **PROP-20.7** (answer+URL ≤480; long → research paper).
2. **Host Debug smoke** (Release blocked on SixLabors — Debug OK): start Host `-c Debug`, probe:
   - `GET /health`
   - `GET /settings/identity`
   - `GET /journals/books` (must include thoughts/wants/dreams)
3. **SMS gate:** confirm denied tool never executes (test output from suite is enough if Host SMS not live).
4. **Do not** claim Release production Pass.

## Skip only

Captcha / CSS animation (N/A here).

## Issues

New bugs → `docs/agents/issues/ISSUE-20261117-*-….md`. P0/P1 notify PM in report.

## Report

`docs/agents/reports/PROP-20.9-QA01-to-PM01.md`
