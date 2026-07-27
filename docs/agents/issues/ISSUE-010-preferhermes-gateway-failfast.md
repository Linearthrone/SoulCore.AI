---
type: issue
id: ISSUE-010
severity: P1
status: Fixed
created: 2026-07-27
filed_by: QA-01
fixed_by: BED-01
related_task: TASK-145, TASK-161
gate: QA-145
updated: 2026-07-27 (Fixed by BED-161: PreferHermes fail-fast; no Ollama fallback)
---

# ISSUE-010 — PreferHermes silently fell back to Ollama when Hermes was down

## Severity

**P1 — Dual-backend / silent bypass.** With PreferHermes=true, Hermes failures fell through to Ollama in `CompleteChatWithToolsAsync` / `CompleteChatAsync`, violating QA-145 AC #8 (Hermes XOR Ollama) and masking gateway outages.

## Fix (BED-161)

- PreferHermes + Hermes.Enabled: Hermes failure throws immediately → `chat.model_down` (no Ollama fallback).
- `HermesHttpClient.CompleteWithToolsAsync` probes `GET /health` and requires API key before chat; throws `hermes gateway unavailable` when unhealthy.
- Secondary Hermes path remains only when PreferHermes=false.

## Related out of scope

- **ISSUE-008** (capture timeout) — not addressed in BED-161 (explicitly out of scope unless trivial).

## Status

**Fixed**
