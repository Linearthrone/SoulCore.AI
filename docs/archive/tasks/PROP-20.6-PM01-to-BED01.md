---
type: task
prop_id: PROP-20.6
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-17
title: Leisure mind gate + desk-model fallback when 1.5b cannot hold her
depends_on: none
soft_depends: PROP-20.4 (prefer shared mind builder when present)
siblings: PROP-20.4, PROP-20.5
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.6 — Leisure one-mind + model fallback

**Activate:** `@Agents/BED-01.md` then execute this ticket.

## Problem

Leisure must carry the same mind block (floors + resting + reason for the interest + related memories) or the pass must not run / not write as her. `qwen2.5:1.5b` may run only when it can hold that block; otherwise use the desk chat model. Leisure tool allow list stays current `LeisureTools` (mail/trade/shell/settings/`desktop_open_app`/`browser_navigate` stay denied).

## Solution

1. Before a leisure write or speak-as-her completion, ensure the prompt contains floors + resting lines (call 20.4 shared builder if available; otherwise temporary local assemble matching 20.4 resting Mid defaults — replace when builder lands).
2. If the configured small leisure model cannot hold the block (context/budget failure or explicit capacity check), re-run that pass on the desk chat model — no blank stand-in, no skipping mind block to save the small model.
3. Keep `LeisureTools` deny list intact; do not widen SMS allow tools into leisure.
4. Tests covering: write suppressed/absent without mind block; fallback path selected when small model cannot hold.

## Files to change

- `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs`
- `SoulCore/SoulCore.Inference/Presence/LeisureTools.cs` only if needed for comments/tests
- `SoulCore/SoulCore.Protocol.Tests/Prop17LeisureTests.cs` (extend, do not gut)

## Do not touch

- SMS Companion (20.5/20.7)
- Journal migrations (20.3)
- ChatDesktop UI

## Acceptance

- [ ] Leisure/SMS-style write-as-her absent unless prompt had floors + resting (leisure path proven)
- [ ] Small-model failure → desk chat model with mind block still present
- [ ] Tests + report: `docs/agents/reports/PROP-20.6-BED01-to-PM01.md`
