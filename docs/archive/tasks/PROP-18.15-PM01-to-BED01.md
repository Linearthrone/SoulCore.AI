---
type: task
prop_id: PROP-18.15
from: PM-01
to: BED-01
priority: P1
status: Done
created: 2026-11-19
title: Fix ActivityResidueComposer CS1061 (ISSUE-20261119-001)
issue: docs/agents/issues/ISSUE-20261119-001-ActivityResidueComposer-CS1061-blocks-build.md
---

# PROP-18.15 — Build break fix

**Activate:** `@Agents/BED-01.md`

`ActivityResidueComposer` CS1061 (`IReadOnlyList<string>` `.Add`) blocks Inference/Prop188 rebuild. Fix type to mutable list (or equivalent). Confirm `dotnet build` Inference + Prop188 filter green. If already fixed by PROP-18.7, document proof and close issue.

Report: `docs/agents/reports/PROP-18.15-BED01-to-PM01.md`
