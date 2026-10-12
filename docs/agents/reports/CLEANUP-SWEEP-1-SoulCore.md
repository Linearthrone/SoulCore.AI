# CLEANUP Sweep 1 — SoulCore PROP-17

Branch: `cursor/prop17-cleanup-sweeps-6372`  
Commit: `67418cc` (code) + this report  
Scope: SoulCore PROP-17 surface (park, SOP, leisure, consent, browser hygiene). No `House/**`.

## Findings fixed (file:symbol)

| # | Location | Change |
|---|----------|--------|
| 1 | `ConsentArgs.StripModelConfirmed` | New shared strip for model-minted `confirmed` |
| 2 | `EmailToolSupport.StripModelConfirmed` / `IsConfirmed` | Delegate strip; drop unused always-false overload; consent-only check |
| 3 | `Mt4ToolSupport.StripConfirmed` / `IsConfirmed` | Same dedupe; drop unused always-false overload |
| 4 | `EmailWriteTools` / `Mt4ToolBase.ExecuteWriteAsync` | Call sites updated to new `IsConfirmed` signatures |
| 5 | `TaskReleaseTool` | Removed dead unused `_assigned` field/ctor param |
| 6 | `ActionProposalClassifier.LooksLikeActionProposal` | Removed same-purpose alias of `TryClassify` |
| 7 | `LeisureSession.BeginOrContinue` | Use `LeisurePrompt.DefaultInterest` (no hard-coded duplicate) |
| 8 | `LeisureEligibility.CanEnterRestAndResearch` | Flatten empty suspended fall-through branch |
| 9 | `LeisureTools.IsAllowed` / `FilterDefinitions` | Early-return style; drop null-`??` after null check |
| 10 | `LeisureRunner.ModelPresentLocalAsync` | Kill redundant second exact-scan loop + nested match |
| 11 | `LeisureRunner.NotingRegistry.ExecuteAsync` | Wrap untrusted content once |
| 12 | `LeisureRunner.RunOnePassAsync` | Strip restating PROP-17 narrative comments |
| 13 | `LeisureHostedService.ExecuteAsync` | Remove empty `catch` around trusted `Release` |
| 14 | `AssignedWorkRunner.SurfaceFailureKey` | Extract helpers; `OfType<string>()`; trim narrative comments |
| 15 | `AssignedWorkHostedService` / `ChatSendHandler` | Trim redundant PROP-17 restating comments |
| 16 | `DesktopOpenAppTool` / `PlaywrightBrowserBridge` / `VirtualBoxGuestAppLauncher*` | Trim restating hygiene comments |
| 17 | `InferenceLease.Touch` | Remove “creativity clock” commentary |
| 18 | `Prop17LeisureTests` | Assert via `TryClassify` after alias removal |

**Cleanup count: 18** (net −79 lines in first commit, +`ConsentArgs`).

## Deferred (Sweep 2+)

- Parallel `NotingRegistry` private types in `LeisureRunner` vs `AssignedWorkRunner` (different semantics; shared base would need care).
- Dual `Schema/` + `Migrations/` copies for 008–010 (likely intentional packaging; do not merge without Memory ownership).
- Broader Email/Mt4 gate helpers still parallel (`IsReadAllowed` / `IsTradeAllowed` overloads) — outside confirmed-strip.
- `TaskCreateTool` / `TaskUpdateStatusTool` verbose arg parsing + tool-boundary `catch` (keep for ITool contract).
- `PlaywrightBrowserBridge` large try/catch surface (mixed pre-PROP-17 + hygiene).
- Park-until `DateTimeOffset.TryParse` culture/style differs slightly between probe paths (behavior OK today).

## Residual risk

- Low: `ModelPresentLocalAsync` simplified to the effective match set of the old nested loops (outer base-name gate never returned alone). Covered indirectly by leisure quiet paths; no dedicated tags-probe unit test.
- Low: `IsConfirmed` signature change is internal; all repo call sites updated. External forks calling the removed always-false overload would need a one-line fix.
- Naming: LinearThrone / Kayleigh preserved; no Kurt introduced on this wave.

## Tests

```text
dotnet test SoulCore/SoulCore.Protocol.Tests \
  --filter "FullyQualifiedName~Prop17|FullyQualifiedName~TaskTools|FullyQualifiedName~WorkContinuation|FullyQualifiedName~GuestWorkspace|FullyQualifiedName~EmailTools|FullyQualifiedName~Mt4Tools"

Passed!  Failed: 0, Passed: 124, Skipped: 0, Total: 124
```

Log: `/opt/cursor/artifacts/sweep1-prop17-tests.log`

## Sweep 2 recommendation

**Yes.** Dig into shared `NotingRegistry` extraction, Email/Mt4 gate helper consolidation, and Schema/Migrations ownership for 008–010 — higher maintainability payoff with still-bounded risk if kept behavior-preserving.
