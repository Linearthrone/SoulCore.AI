---
type: issue
id: ISSUE-20260723-002
severity: P1
status: closed
created: 2026-07-23
filed_by: QA-01
related_task: TASK-20260723-084
gate: E3 (hard-stop)
updated: 2026-07-23 (QA-089: E3 PASS, E1 PASS - hard-stop gate CLEARED)
closed_by: QA-01
closed_date: 2026-07-23T21:06:21Z
---

# ISSUE-002 — E3 loco hard-stop FAIL: inference timeout blocks UE verb forwarding

> **[已修复 2026-07-23 QA-089]** E3 loco PASS. Host log shows `soul=loco` / `move_avatar_relative 50 0 0`
> forwarded after BED-088 keyword-based loco intent detection. E1 speak re-confirmed PASS.
> Hard-stop gate CLEARED. SoulLoop enable decision may proceed.

## Severity

**P1 — Hard-stop gate failure. SoulLoop must NOT be enabled until resolved.**

## Summary

E3 (Chat -> Host -> UE loco) **FAILED** during QA-084 post-recycle E2E gate run.
The Host accepted the `chat.send` frame with loco intent ("take a small step forward")
but did **not** forward a `move_avatar_relative <f> <r> <u>` plain frame to UE `:8888`.

Root cause: the Host's inference call path (`CompleteChatAsync`) is failing with
`TaskCanceledException` on both the Ollama (`:11434`) and Hermes (`:8642`) HTTP
requests. Because the inference reply never returns, the Host never reaches the
UE verb-forwarding code path (`UeVerbWireMapper.MapLoco`), so no loco frame is
emitted to UE.

## Repro steps

1. Host running on 127.0.0.1:7700 (PID 66700, post OPS-083 recycle), soulLoop.enabled=false
2. UE connected on ws://127.0.0.1:8888 (unreal.connected=true)
3. Run: `.\SoulCore\scripts\e2e\e2e-E3-loco.ps1 -Force` (or focused probe)
4. Send `chat.send` with text "take a small step forward"
5. Observe: Host emits `presence.status` + `emotion.snapshot` but NO `move_avatar_relative`
   reaches UE `:8888` (only `sensor_frame` telemetry seen)

## Actual E3 evidence

```
hostFrames=3 (presence.status + 2x emotion.snapshot)
ueConnected=True
ueFrames=16 (all sensor_frame JSON telemetry)
locoSeen=False
Result: Fail  HARD-STOP: do NOT enable SoulLoop if E3 fails
```

## Root cause (from Host log)

`.soulcore-host.log` shows repeated warnings for every chat.send:

```
warn: SoulCore.Host.Ws.ChatWebSocketHandler[0]
      Chat model path failed; stub=False
      System.Threading.Tasks.TaskCanceledException: A task was canceled.
         at ChatWebSocketHandler.CompleteChatAsync(...) line 443
         at ChatWebSocketHandler.HandleChatSendAsync(...) line 298
```

The Host sends HTTP POST to:
- `http://127.0.0.1:11434/api/generate` (Ollama)
- `http://127.0.0.1:8642/v1/chat/completions` (Hermes)

Both are sent but **both time out** (TaskCanceledException). The verb-forwarding
path (MapSpeak / MapSetEmotion / MapLoco) executes only AFTER a successful inference
reply, so with inference failing, E1 (speak) and E3 (loco) cannot pass.

## Backend reachability (verified)

Both backends ARE reachable and healthy — the timeout is in the Host's HTTP client, not a down service:

- **Ollama `:11434`**: `GET /api/tags` -> 200, model `gemma4:latest` loaded (9.6 GB)
- **Hermes `:8642`**: `GET /health` -> 200, `{"status":"ok","platform":"hermes-agent","version":"0.18.2"}`
- **Ports**: 11434, 8642, 8888, 7700 all TcpTestSucceeded=True

So this is an **inference HTTP timeout** (Host's HttpClient timeout too short for
model generation latency), NOT a down backend.

## Impact

- **E1 (speak)**: Fail (same root cause — no inference reply -> no speak verb forwarded)
- **E2 (set_emotion)**: PASS — emotion.snapshot is emitted independent of inference
  (derived from the emotion model on chat.send receipt, not from the inference reply)
- **E3 (loco)**: FAIL (hard-stop gate)
- SoulLoop enable is BLOCKED until E3 passes (charter §3.3 decision deferred)

## Why this is NOT a BED-082 regression

BED-082 wired CharterService, DriftWatcher, SpendMeter into DI. The Host boots clean
(0 build warnings, 62/62 tests pass), /health is ok, WS accepts connections, emotion
snapshot works. The inference timeout is a pre-existing environment/config condition
(HTTP client timeout vs model latency), unrelated to the safety-lib DI wiring.
SpendMeter's `RecordSpend` is wrapped in try/catch and swallows failures, so it cannot
be causing the inference path failure.

## Suggested fix (for DEV-01 / OPS-01)

### Root cause (updated by PM after investigation)

The Ollama log shows the model IS generating tokens (~120 t/s) but produces 2919+ tokens
without a stop token, so the generation never completes within any timeout. The
`OllamaGenerateRequest` has no `num_predict` / `max_tokens` field — the model generates
endlessly. Additionally, `gemma4:latest` crashed with a CUDA buffer overrun on first load.

The fix is to add token generation limits:

1. **Add `MaxTokens` (default 256) to `InferenceOptions`** — configurable token limit.
2. **Add `num_predict` to the Ollama request** — pass `MaxTokens` as `options.num_predict`.
3. **Add `max_tokens` to the Hermes request** — pass `MaxTokens` to the OpenAI-compatible API.
4. **Add `MaxTokens` to `appsettings.json`** Inference + Hermes sections.
5. After fix, re-run E1 + E3 to confirm `speak` and `move_avatar_relative` are forwarded.

### Alternative model

If `gemma4:latest` continues to crash (CUDA buffer overrun), switch to a smaller/stable model
(e.g. one of the Q4_K_M GGUF models already loaded in Ollama). The `NSFW-flash:Q4_K_M` (1.28 GB)
model generated successfully but needs the `num_predict` limit to stop in time.

## Hard-stop notice

**E3 is a hard-stop gate. SoulLoop must NOT be enabled (charter §3.3) until E3 passes.
PM-01 must be notified.**

---

## QA-086 update (2026-07-23 16:35 local) — root cause refined: stale deployed appsettings

QA-086 re-ran E1 (speak) and E3 (loco) against the recycled Host (PID 16108, started
2026-07-23 16:18:51). **Both still FAIL** with the same `TaskCanceledException` at
`CompleteChatAsync line 443`. Root cause is now identified precisely:

### The BED-085 code fix is correct, but the deployed config is stale

- **Source `appsettings.json`** (updated 2026-07-23 16:18): `Model=hf.co/UnfilteredAI/NSFW-flash:Q4_K_M`,
  `TimeoutSeconds=180`, `MaxTokens=256` — CORRECT.
- **Deployed `bin/Debug/net8.0/appsettings.json`** (last copied 2026-07-23 16:03:57, BEFORE the
  source update): `Model=gemma4:latest`, `TimeoutSeconds=120`, `MaxTokens=256` — **STALE**.
- The Host content root is `bin/Debug/net8.0`, so it loads the **stale** appsettings.
- The DLLs (built 16:04) DO contain the BED-085 `num_predict` code, so `num_predict=256` IS being
  sent — but to `gemma4:latest` (the 9.6 GB model that crashes with CUDA buffer overrun per the
  Ollama log) with only a 120s HttpClient timeout.

### Direct Ollama backend probe proves the fix works with the right model

QA-086 ran a direct `POST /api/generate` to Ollama `:11434` with the CORRECT model +
`num_predict=256`:

```
Model: hf.co/UnfilteredAI/NSFW-flash:Q4_K_M (1.28 GB)
Prompt: "Say hello"
num_predict: 256
Result: 200 OK in 18s
  eval_count: 256   (generation stopped at the token limit — num_predict works)
  eval_duration_s: 2.19
  total_duration_s: 17.7
```

**The NSFW-flash:Q4_K_M model generates successfully in 18s with num_predict=256** — well
within the 180s timeout. The BED-085 fix is functionally correct; it just has not been
deployed (the build output appsettings was never refreshed after the source update).

### Deployment gap cause

BED-085's report notes the full-solution `dotnet build` hit file-copy errors because the
running Host held locks on the output DLLs. Each project compiled to its OWN output dir,
but the **Host output dir copy was blocked**. The source `appsettings.json` was then updated
(to switch model + raise timeout) at 16:18, but no subsequent `dotnet build` was run to copy
it to `bin/Debug/net8.0/`. The Host recycle (PID 16108 at 16:18:51) started from the stale
build output without refreshing the appsettings.

### Required fix (for OPS-01 / DEV-01)

1. Stop the Host (PID 16108).
2. Run `dotnet build SoulCore/SoulCore.sln -c Debug` (with Host stopped, the file-copy lock
   is cleared; this will copy the updated `appsettings.json` to `bin/Debug/net8.0/`).
   - Alternatively, manually copy `SoulCore/SoulCore.Host/appsettings.json` →
     `SoulCore/SoulCore.Host/bin/Debug/net8.0/appsettings.json`.
3. Restart the Host. Confirm `/health` still ok.
4. Re-run E1 + E3 (QA-086 probes are in `SoulCore/scripts/e2e/_qa_e1_robust_086.ps1` and
   `_qa_e3_robust_086.ps1`). With NSFW-flash + 180s + num_predict=256, inference should
   complete in ~18-40s and the speak / move_avatar_relative verbs should forward to UE.

### E1 evidence (QA-086)

```
====== E1 (robust) QA-086: Chat -> Host -> UE speak ======
ChatText: Say hello | WaitWindow: 185s
Host health: status=ok unreal.connected=True inference.provider=ollama
UE listener connected. Host WS connected.
send: chat.send (text="Say hello", sessionId=qa086-E1)
host-frame[1]: presence.status
host-frame[2]: emotion.snapshot (valence=-0.4, arousal=0.7, dominance=0.3, focus=0.5, label="tense", revision=12)
host-frame[3]: emotion.snapshot
ue-frame[1]: status (scene=Home, fps=50.9)
ue-frame[2]: sensor_frame (avatar_location x=-239.99...)
[heartbeat] 15s..180s; hostFrames=3 ueFrames=2 chatDone=False speak=False
Result:   Fail
Evidence: hostFrames=3; chatDone=False; chatDelta=False; ueConnected=True; ueFrames=2; speakSeen=False
```

### E3 evidence (QA-086)

```
====== E3 (robust) QA-086: Chat -> Host -> UE loco ======
ChatText: take a small step forward | WaitWindow: 185s
Host health: status=ok unreal.connected=True inference.provider=ollama
UE listener connected. Host WS connected.
send: chat.send (text="take a small step forward", sessionId=qa086-E3)
host-frame[1]: presence.status
host-frame[2]: emotion.snapshot (valence=-0.4, arousal=0.7, dominance=0.3, focus=0.5, label="tense", revision=12)
host-frame[3]: emotion.snapshot
ue-frame[1]: status (scene=Home, fps=44.8)
ue-frame[2]: sensor_frame (avatar_location x=-239.99...)
[heartbeat] 15s..180s; hostFrames=3 ueFrames=2 chatDone=False loco=False
Result:   Fail
Evidence: hostFrames=3; chatDone=False; chatDelta=False; ueConnected=True; ueFrames=2; locoSeen=False
HARD-STOP: do NOT enable SoulLoop if E3 fails
```

### Host log (QA-086, both E1 and E3 runs)

```
warn: SoulCore.Host.Ws.ChatWebSocketHandler[0]
      Chat model path failed; stub=False
      System.Threading.Tasks.TaskCanceledException: A task was canceled.
         at ChatWebSocketHandler.CompleteChatAsync(...) line 443
         at ChatWebSocketHandler.HandleChatSendAsync(...) line 298
```

The `TaskCanceledException` fires at the deployed 120s HttpClient timeout (not 180s,
because the deployed appsettings still has `TimeoutSeconds=120`).

### Status

**STILL OPEN (P1 hard-stop).** E3 has not passed. SoulLoop must NOT be enabled.
The fix is a deployment refresh (rebuild or manual appsettings copy + Host restart),
NOT a code change — the BED-085 code is correct.

---

## QA-087 update (2026-07-23 16:57 local) — root cause REFINED: two distinct bugs found

QA-087 re-ran E1 (speak) and E3 (loco) against the recycled Host (PID 54256, started
2026-07-23 16:35:00) with the **correct deployed config** confirmed:
`bin/Debug/net8.0/appsettings.json` has `Model=hf.co/UnfilteredAI/NSFW-flash:Q4_K_M`,
`TimeoutSeconds=180`, `MaxTokens=256` (deployment gap from QA-086 is FIXED).

### E1 (speak): PASS

With a corrected test harness (see "test harness bug" below), E1 inference completes
in ~2.4s and the Host forwards both `set_emotion` and `speak` verbs to UE :8888.

Host log evidence (session 25f768dd, E1-final):
```
info: OllamaInferenceClient — Received HTTP response headers after 2417.9334ms - 200
info: UnrealVerbClientStub — Unreal verb sent: soul=set_emotion ue=set_emotion frame={"type":"command",...}
info: UnrealVerbClientStub — Unreal verb sent: soul=speak ue=speak frame=speak Hello there! ...
```

WS client evidence: 21 host frames (presence.status + emotion.snapshot x2 + chat.delta x17
+ chat.done), `provider=ollama`, `stub=false`. `chat.done` received ~2.4s after send.

**Note on UE listener:** The UE :8888 listener did NOT see the `speak` plain frame or `ack`
on its socket. This is expected architecture: UE :8888 is a UE plugin WS server (HouseVictoriaBridge).
The Host connects as a client and sends verbs TO UE's server socket. UE consumes verbs internally
(drives the avatar) and does NOT relay/echo them to other connected clients. The UE listener only
receives UE's own broadcast frames (`status`, `sensor_frame`). **The host log `Unreal verb sent`
line IS the authoritative proof of forwarding** — the `SendAsync` on the Host's UE socket succeeded.

### E3 (loco): FAIL — no code path from chat.send to move_avatar_relative

E3 inference now SUCCEEDS (chat.done received, ~0.9s), but **no `move_avatar_relative` frame
is ever sent to UE**. Host log (session 3187f1dd, E3-final) shows only `set_emotion` + `speak`:
```
info: OllamaInferenceClient — Received HTTP response headers after 879.0231ms - 200
info: UnrealVerbClientStub — Unreal verb sent: soul=set_emotion ue=set_emotion frame=...
info: UnrealVerbClientStub — Unreal verb sent: soul=speak ue=speak frame=speak Sure thing...
```
No `move_avatar_relative` / `loco` verb. WS client: `locoSeen=False`.

**Root cause (code, not config):** `ChatWebSocketHandler.HandleChatSendAsync` (lines 362-378)
only calls `_unreal.SetEmotionAsync(...)` and `_unreal.SpeakAsync(reply, ...)`. It does NOT
call `_unreal.LocoAsync(...)`. There is no intent router that parses the user text
("take a small step forward") or the model reply to dispatch a loco verb. The loco path
(`IUnrealVerbClient.LocoAsync` → `UeVerbWireMapper.MapLoco` → `move_avatar_relative <f> <r> <u>`)
is fully wired in the adapter and unit-tested (`UeVerbWireMapperTests.Loco_maps_to_plain_move_avatar_relative`),
but **nothing in `SoulCore.Host` invokes `LocoAsync`**. Grep of `SoulCore.Host` for
`Loco|loco|move_avatar` returns zero matches.

**This is a missing-feature bug, not a config/deployment issue.** E3 cannot pass until an
intent router (or a dedicated `loco.send` frame type) is added to dispatch `LocoAsync` from
the chat path or a new WS frame.

### Secondary finding: test harness bug (QA-086 robust scripts)

The QA-086 robust scripts (`_qa_e1_robust_086.ps1`, `_qa_e3_robust_086.ps1`) have a
**`Receive-FrameR` function that cancels in-flight `ReceiveAsync`** after an 800ms timeout
(`$cts.Cancel()`). Canceling a pending `ClientWebSocket.ReceiveAsync` aborts the WebSocket
connection from the client side. The Host sees `context.RequestAborted` fire (Program.cs line
196 passes `context.RequestAborted` to `RunAsync`), which cancels the in-flight Ollama HTTP
request — Ollama returns HTTP 500 (`srv stop: cancel task` in the Ollama server log) and the
Host gets `TaskCanceledException`.

This caused ALL the previous "inference timeout" failures in QA-084/086 (except the one
run where Ollama responded in <800ms before the first receive-cancel). The 185s window was
irrelevant — the connection was aborted within ~2s of the first receive-timeout cancel.

**Proof:** A hypothesis test that sends `chat.send` then waits 15s WITHOUT calling
`ReceiveAsync` (no cancel) → inference completes in 2.5s, `chat.done` received, `speak`
verb forwarded (host log: `Unreal verb sent: soul=speak`). PASS. The fixed E1/E3 scripts
(`qa-087-E1-final.ps1`, `qa-087-E3-final.ps1`) use this "send → wait → drain" pattern and
both get successful inference.

**The `TaskCanceledException` in QA-084/086 was a test-harness artifact, NOT a Host or
config bug.** The Host's inference path is correct when the WS connection is not aborted
by the client.

### Updated status

**STILL OPEN (P1 hard-stop).** E3 FAILS for a NEW reason: missing loco dispatch path in
`HandleChatSendAsync` (no intent router → `LocoAsync` never called). The previous inference-
timeout root cause (stale config) is RESOLVED — the deployed config is now correct and
inference succeeds. The remaining blocker is a code change (add intent routing or a loco
frame type), not a deployment fix.

**SoulLoop must NOT be enabled** until E3 passes (charter §3.3). PM-01 must be notified.

### Required fix (for DEV-01)

1. Add an intent router to `HandleChatSendAsync` (or a new `loco.send` frame type) that
   detects locomotion intent in the user text or model reply and calls
   `_unreal.LocoAsync(payload, cancellationToken)`.
2. Map the intent to `forward/right/up` cm values (e.g., "step forward" → forward=50).
3. Re-run E3 with the fixed harness (`qa-087-E3-final.ps1`) — expect `move_avatar_relative`
   in the host log (`Unreal verb sent: soul=loco`).
4. Fix the QA-086 robust scripts' `Receive-FrameR` to not cancel in-flight `ReceiveAsync`
   during inference (use the "send → wait → drain" pattern from `qa-087-E1-final.ps1`).

---

## QA-089 update (2026-07-23 21:06 UTC) — E3 PASS, hard-stop gate CLEARED

QA-089 re-ran E3 (loco) and E1 (speak) against the recycled Host (PID 69512) with the
BED-088 keyword-based loco intent detection added to HandleChatSendAsync. Both tests
used the fixed harness pattern (send -> wait WITHOUT receiving -> drain) from QA-087.

### E3 (loco): PASS

Chat text:  take a small step forward (triggers loco keyword detection).

Host log evidence (authoritative proof):
`
Unreal verb sent: soul=loco ue=move_avatar_relative frame=move_avatar_relative 50 0 0
Unreal verb sent: soul=loco ue=move_avatar_relative frame=move_avatar_relative 50 0 0
`

WS client evidence: 22 host frames (presence.status + emotion.snapshot x2 + chat.delta x18
+ chat.done), provider=ollama, stub=false. Inference completed (chat.done ~5.2s after
send, warm model). The loco keyword detection forwarded move_avatar_relative 50 0 0
to UE :8888.

UE listener: 28 frames (status + sensor_frame telemetry only). As documented in QA-087,
UE :8888 is a server, not a broadcast hub — the forwarded verb is consumed internally by
UE and not echoed to a second listener socket. The host log Unreal verb sent: soul=loco
line IS the authoritative proof of forwarding.

### E1 (speak): PASS (re-confirmed)

Chat text: Say hello.

Host log evidence (authoritative proof):
`
Unreal verb sent: soul=speak ue=speak frame=speak Hola! Hello there! I'm here to answer your questions...
`

WS client evidence: 11 host frames (presence.status + emotion.snapshot x2 + chat.delta x7
+ chat.done), provider=ollama, stub=false. Inference completed ~0.9s (warm).

### Conclusion

- E3 (loco): PASS — BED-088 keyword-based loco intent detection works.
- E1 (speak): PASS — no regression.
- ISSUE-20260723-002 status: CLOSED.
- Hard-stop gate CLEARED. SoulLoop enable decision (charter section 3.3) may proceed.

### Scripts

- SoulCore/scripts/qa-089/qa-089-E3-loco.ps1 (send -> 18s no-recv -> drain + host log inspect)
- SoulCore/scripts/qa-089/qa-089-E1-speak.ps1 (send -> 15s no-recv -> drain + host log inspect)
