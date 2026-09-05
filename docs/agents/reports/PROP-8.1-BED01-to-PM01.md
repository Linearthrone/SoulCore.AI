---
type: report
prop_id: PROP-8.1
from: BED-01
to: PM-01
status: Completed
created: 2026-09-05
branch: cursor/prop8-chat-strangler-8a1f
base: cursor/prop9-di-modules-8a1f
verdict: Pass
---

# PROP-8.1 — Chat orchestration strangler (BED-01)

## Verdict: **Pass**

Strangler extraction complete. `ChatWebSocketHandler` is a 17-line facade; prompt ownership, history ring buffer, session runner, and focused handlers are in place. Chat + tool-loop regression tests green (26/26 filtered).

## Deliverables

| # | Requirement | Status |
| --- | --- | --- |
| 1 | `ChatContextBuilder` — single prompt owner | **Done** — `IChatContextBuilder` + `ChatContextBuilder` |
| 2 | History deque/ring + snapshot (no `RemoveAt(0)`) | **Done** — `SessionRing` in `ChatSessionHistoryStore` |
| 3 | Session runner + focused handlers thin handler | **Done** — see types below |
| 4 | Gated parallel independent context reads (PROP-5 Pass) | **Done** — `Task.WhenAll` in `BuildAsync` |
| 5 | Tests green + line-count delta | **Done** — 26/26 chat/tool-loop tests |

## Architecture (before → after)

**Before:** One ~1,643-line `ChatWebSocketHandler` owned socket lifecycle, frame routing, chat orchestration, prompt assembly (6 sequential reads + string patches), emotion correction, loop-tick, post-chat Unreal side effects, tool-loop, and history append with `List.RemoveAt(0)` trim.

**After:**

```
ChatWebSocketHandler (17 lines, facade)
  └── ChatWebSocketSessionRunner — WS loop + frame routing
        ├── ChatSendHandler — chat.send orchestration + inference
        │     └── IChatContextBuilder — parallel reads → immutable ChatContext
        ├── EmotionCorrectHandler — emotion.correct
        ├── LoopTickHandler — loop.tick ack
        └── EmotionSnapshotSender — shared emotion.snapshot frames
ChatPostEffectsHandler — speak/emotion/loco/animation/look (Strategy A)
ChatSessionHistoryStore — ring buffer + copy-on-read snapshot
```

## New / changed types

| File | Lines | Role |
| --- | ---: | --- |
| `ChatWebSocketHandler.cs` | 17 | Facade (was **1,643**) |
| `ChatWebSocketSessionRunner.cs` | 138 | Session loop + dispatch |
| `ChatSendHandler.cs` | 644 | chat.send + tool-loop / single-shot |
| `ChatPostEffectsHandler.cs` | 383 | Post-chat Unreal side effects |
| `ChatContextBuilder.cs` | 266 | Prompt owner + parallel reads |
| `ChatContext.cs` | 11 | Immutable context record |
| `IChatContextBuilder.cs` | 17 | Builder interface |
| `EmotionCorrectHandler.cs` | 168 | emotion.correct |
| `LoopTickHandler.cs` | 50 | loop.tick |
| `EmotionSnapshotSender.cs` | 60 | emotion.snapshot helper |
| `WsFrameSender.cs` | 21 | Shared frame send |
| `ChatSessionHistoryStore.cs` | 107 | Ring buffer (was **69**, had `RemoveAt(0)`) |

**Line-count delta (handler lane):**

- `ChatWebSocketHandler`: **1,643 → 17** (−99% in god file)
- Extracted Host/Ws orchestration: **~1,875 lines** across 10 focused types (testable seams)
- `ChatSessionHistoryStore`: **69 → 107** (ring buffer; no front-trim loop)

## Parallel context reads (PROP-8.4 slice, PROP-5 gate satisfied)

`ChatContextBuilder.BuildAsync` runs three independent reads concurrently:

1. Episodic recall (semantic or recency fallback)
2. Charter identity anchors
3. Emotion preamble

Results assemble into one immutable `ChatContext` before tool-guidance append. Failures in any leg degrade gracefully (empty identity/memory or default emotion) — behavior parity preserved.

## Tests

```
dotnet test SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
  --filter "FullyQualifiedName~ChatWebSocketHandler|FullyQualifiedName~ChatSessionHistory|FullyQualifiedName~ChatContextBuilder"

Passed!  - Failed: 0, Passed: 26, Skipped: 0, Total: 26
```

**New focused tests:**

- `ChatSessionHistoryStoreTests` — ring trim, snapshot copy-on-read, clear
- `ChatContextBuilderTests` — preamble order, memory truncation, parallel build + tool guidance, charter failure resilience

**Updated:** `ChatWebSocketHandlerToolLoopTests` — wired to new handler graph; Hermes ctor references removed (BED-185 parity).

## Fences respected

- No SignalR rewrite
- No Hermes reintroduction
- No Memory repo / PROP-11 edits
- No desktop backend changes
- DI modules from PROP-9 reused (`CompanionServiceCollectionExtensions`)

## Commits

Branch: `cursor/prop8-chat-strangler-8a1f` (from `cursor/prop9-di-modules-8a1f`)
