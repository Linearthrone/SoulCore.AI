---
type: report
prop_id: PROP-16.6
prop_root: PROP-16-presence-glass-overlay
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.6-PM01-to-BED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.6 — Chat-multi — Pass

## Verdict

**Pass.** Host chat mode defaults to `one` when `chat-mode.txt` is missing. `GET` and `POST /api/personas/mode` are registered before `/{personaId}`. Body `{"mode":"one"}` or `{"mode":"many"}` is stored. Any other value is 400 and does not create the file.

`chat.send` may include `personaId`. The turn copies mode, foreground id, and payload `personaId` before any await and does not call `GetActive()` again for that pin. Mode `one` writes the foreground episodic store when `personaId` is omitted, and rejects a different id without creating that persona's database. Mode `many` requires `personaId`, writes that persona's context, transcript, and episodic store, and leaves the foreground where it was. That background turn calls `CompleteAsync` only.

Tools run only when the pinned id is the foreground. The tool loop is filtered with that pack's `ToolPolicy` before dispatch. `AllowDesktop` false drops desktop tools, and a refused tool is not executed. Changing the foreground cancels an in-flight tool loop pinned to the previous foreground. A background chat that did not register a tool loop is not cancelled. One Host process. SoulLoop reads and writes `GetMemoryStore` for the foreground id only.

No Avalonia edit. No House edit. No commit. No push. Branch stayed `pr-177`.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| Missing mode storage reads as `one` | Pass | `MissingModeStorage_ReadsAsOne_AndOtherValuesAre400`. A fresh root has no `chat-mode.txt`. `HostChatMode.Current` is `one` and does not create the file. `GET /api/personas/mode` is 200 `{"mode":"one"}`, so `mode` is not captured by `/{personaId}`. `POST` with `both`, `One`, and `""` is 400 and the file stays absent. `POST {"mode":"many"}` persists `many`. A second `HostChatMode` on the same root reads `many`. |
| Mode `one` writes the foreground store when `personaId` is omitted, and rejects another persona without writing their store | Pass | `ModeOne_OmittedPersona_WritesForeground_AndRejectsOtherWithoutWrite`. Omitted `personaId` writes `episodic-pinned` on persona A, recalls `only-foreground-memory` into that turn's preamble, and appends `hello-a` to A's transcript. The turn reads mode, `ActivePersonaId`, and `GetActive()` once each. A second turn with persona B returns `chat.persona_rejected`, does not call the model, does not create B's database, and leaves A's episodic rows unchanged. A stays foreground. |
| Mode `many` writes persona B's episodic store while A stays foreground, and that turn does not dispatch a tool | Pass | `ModeMany_WritesPersonaB_WhileAStaysForeground_AndDoesNotDispatchATool`. A missing `personaId` is `chat.persona_required` and does not create B's database. A turn with `personaId` B recalls `only-b-memory`, writes `episodic-pinned` on B, and appends `hello-b` to B's transcript only. `CompleteWithToolsAsync` was not called. `desktop_screenshot` was not executed. `ActivePersonaId` stays A on the session and the store hub. |
| A foreground turn with `AllowDesktop` false does not dispatch a desktop tool | Pass | `ForegroundTurn_AllowDesktopFalse_DoesNotDispatchDesktopTool`. The definitions passed to the tool loop include `speak`, `browser_navigate`, and `email_send`, and do not include `desktop_screenshot`. An explicit `desktop_screenshot` execute is refused. Desktop call count stays 0. Speak, browser, and email run. |
| A foreground change cancels the previous foreground's in-flight tool loop | Pass | `ForegroundChange_CancelsPreviousToolLoop_AndDoesNotCancelBackgroundChat`. Persona A's tool loop is inside `CompleteWithToolsAsync`. Switching the foreground to C returns `chat.foreground_changed` on A's socket and does not write A's store. Persona B's background `CompleteAsync` finishes with `chat.done`, was not cancelled, and writes `episodic-pinned` on B only. `desktop_open_app` was not executed. |
| SoulLoop does not write a background persona's store | Pass | `SoulLoop_DoesNotWriteBackgroundPersonaStore`. The tick's injected memory store records 0 writes. The reflection row is on foreground `loop-a`. Background `loop-b` still holds only `b-sentinel`. The hub's active id stays `loop-a`. |
| `dotnet test` for the new tests passes, with the output in the report | Pass | 6 passed, 0 failed. Output below. |
| Report lists commands and results | Pass | This file. |

## What changed

- `GET` and `POST /api/personas/mode` on the persona group, registered before `/{personaId}`. Storage is `chat-mode.txt` under the personas root. Missing or unrecognized contents read as `one` and do not create the file.
- `chat.send` accepts `personaId`. The handler copies mode, foreground id, foreground pack, and payload `personaId` before any await. Context uses `BuildPinnedAsync` with that pack and `GetMemoryStore(personaId)`. Transcript appends through `GetTranscriptStore(personaId)`. The episodic write uses the same memory handle. The foreground is not switched.
- A foreground turn filters the registry with `PersonaToolPolicy` before `CompleteWithToolsAsync`. Desktop names containing `desktop`, browser names starting with `browser_`, and email names starting with `email_` are dropped when the matching flag is false. `ExecuteAsync` on a dropped name does not call the tool. A background turn calls `CompleteAsync` only and skips post-effects.
- `ForegroundToolLoopGate` tracks tool loops by the foreground id copied at the start of the turn. `ActivePersonaSession.SetActiveAsync` cancels the previous id when the foreground actually changes.
- SoulLoop episodic recall and reflection call `GetMemoryStore` for the current foreground id when a session and store hub are present. They do not take a background id.

## Commands

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --nologo --filter "FullyQualifiedName~ChatMultiTests"
```

Branch during the run: `pr-177` (`git branch --show-current`).

### Chat-multi tests

```text
  Determining projects to restore...
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-gwg2-r3hj-4w44 [SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j3p4-wp97-rph4 [SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j9gm-c75j-xc9q [SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-jjfr-hcj7-qf5w [SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wmxv-xphr-5c9g [SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
  All projects are up-to-date for restore.
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-gwg2-r3hj-4w44
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j3p4-wp97-rph4
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j9gm-c75j-xc9q
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-jjfr-hcj7-qf5w
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wmxv-xphr-5c9g
  SoulCore.Protocol -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  SoulCore.Core -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  SoulCore.Config -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Config\bin\Debug\net8.0\SoulCore.Config.dll
  SoulCore.Adapters.Ws -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Adapters.Ws\bin\Debug\net8.0\SoulCore.Adapters.Ws.dll
  SoulCore.Memory -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Memory\bin\Debug\net8.0\SoulCore.Memory.dll
  SoulCore.Inference -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\bin\Debug\net8.0\SoulCore.Inference.dll
  SoulCore.Hermes -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Hermes\bin\Debug\net8.0\SoulCore.Hermes.dll
SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Host\Hosting\ServiceCollectionExtensions\VoiceServiceCollectionExtensions.cs(49,13): warning CA1416: This call site is reachable on all platforms. 'VoiceSpeakService' is only supported on: 'windows'. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416) [SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Host\SoulCore.Host.csproj]
  SoulCore.Host -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll
  SoulCore.Protocol.Tests -> SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll
Test run for SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 3 s - SoulCore.Protocol.Tests.dll (net8.0)
```
