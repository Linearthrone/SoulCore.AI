---
type: report
prop_id: PROP-17.3
from: BED-01
to: PM-01
status: Pass
created: 2026-10-12
updated: 2026-10-12
---

# PROP-17.3 — Local LeisureModel R&R — Pass

## Done

1. `Inference:LeisureModel` default `qwen2.5:1.5b`; `ResolveLeisureBaseUrl` embedding-style local `:11434` only — never reuses cloud `BaseUrl`. Empty LeisureModel → quiet (no work-model / cloud fallback).
2. `LeisureEligibility`: R&R when no runnable `in_progress`/`todo` has a live surface; suspended never blocks.
3. `IInferenceLease` exclusive keep-alive; `AssignedWorkHostedService` skips ticks while leisure holds the lease.
4. `LeisureHostedService` + `LeisureRunner`: self-started research (no task / no release); activity `Reading about X · local`; notes → `life_hacks` journal; action proposals (buy/email/install/trade/replace lesson) → `suspended` rows.
5. `UntrustedObservation`: page/screenshot text marked untrusted; stripped from chat/work preamble; cannot mint `confirmed` / name deny tools.
6. Human chat + SMS abort in-flight R&R before next preamble; work/R&R continuations no longer append to `presence-local`.
7. No creativity clock / wall-clock kill on a living research thread.

## Evidence

- `dotnet build SoulCore.Protocol.Tests` — success
- Targeted: **42 passed** (`Prop17Leisure*` + routing/cloud)
- Broader PROP-17 filter: **59 passed** (`Prop17*` + `WorkContinuation*` + `TaskTools*`)

## Files (SoulCore)

- Config: `InferenceOptions.LeisureModel` / `ResolveLeisureBaseUrl`; appsettings
- Inference: `InferenceModelRouting.ResolveLeisureModel`, `ToolLoopOptions.ModelOverride`, Ollama honor override
- Presence: `InferenceLease`, `LeisureEligibility`, `LeisureSession`, `ActionProposalClassifier`, `UntrustedObservation`, `LeisureTools`, `SurfaceAvailability`
- Host: `LeisureRunner`, `LeisureHostedService`, work-runner skip + no presence-local spam, chat/SMS abort, preamble strip
- Tests: `Prop17LeisureTests.cs`

## Blockers

None for 17.3. Soft handoff: FED activity-band interest phrases (17.4).
