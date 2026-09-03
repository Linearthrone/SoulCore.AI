---
type: proposal
id: PROP-AGENT-LOOP-01
from: TT-01
created: 2026-07-26
status: unexecuted
title: Victoria agent loop — tool-calling + tool registry (restore LLMOD agency)
---

# Victoria agent loop: tool-calling + tool registry

## Why this exists

Victoria today is a chat partner with hardwired buttons. The model produces
text; a few C# keyword detectors (`DetectLocoIntent`, `DetectAnimationIntent`,
`DetectLookIntent`) fire side-effects off the *user's* words. Victoria never
decides to act. In LLMOD she had ~60 LLM-callable tools across memory, desktop,
browser, Unreal editor, trading, filesystem, and task/workflow systems — all
driven through a Hermes tool-loop where the model emitted structured
`tool_calls` and the host executed them.

This proposal restores that agency in SoulCore.

## Verified current state (SoulCore)

| Layer | Today |
| --- | --- |
| Inference | `IInferenceClient.CompleteAsync(prompt, system) → string`. No `tools`, no `tool_calls`, no tool-loop. |
| Hermes client | `HermesHttpClient.ChatAsync` — OpenAI-compatible `/v1/chat/completions`, no `tools` field, no `tool_choice`, no streaming, no tool-call parsing. |
| Body verbs | 5 keyword-detected side-effects (speak/loco/play_animation/look/set_emotion). Not LLM-callable. |
| Memory | Host-injected into preamble (`BuildContextPreamble`). Not model-callable. |
| Agency | None. Model cannot choose to act. |

## Verified prior state (LLMOD)

Two tool surfaces existed:

1. **Hermes tool-loop** (Python `:8642`) — OpenAI-compatible endpoint with
   `tools` + `tool_choice` + SSE `hermes.tool.progress`. Model emitted
   `tool_calls`; host executed; result fed back; model continued.
2. **MCP servers** wired into Hermes via stdio:
   - `house_victoria` — ~40 custom tools (memory, data banks, system, desktop,
     browser, Unreal editor RC, trading, task/workflow, agent step/state)
   - `house_victoria_data` — filesystem MCP (read/write/list whitelisted roots)
   - `computer_use` — desktop screenshot/click/type/scroll/key + window list/focus
   - Hermes native: `terminal`, `web`

### Full LLM-callable tool inventory (LLMOD)

**Body / Unreal Editor RC (8 tools)** — `unreal_editor_health/screenshot/
search_assets/get_property/set_property/call/console/spawn_actor`. Hits Editor
Remote Control `:30010`, separate from world `:8888`.

**Memory / data banks (13 tools)** — `memory_store/retrieve/search/stats`,
`memory_conversation_log/get`, `external_data_bank_get`, `app_memory_search`,
`project_bank_create/get`, `knowledge_bank_add`, `resource_bank_index`,
`config_bank_set/get`.

**Task / workflow (9 tools)** — `task_create/get/update_status/list`,
`workflow_create/execute/get`, `progress_get/update`.

**Desktop / browser (10 tools)** — `computer_use` (capture/click/type/scroll/
key), `list_desktop_windows`, `focus_desktop_window`, `browser_bridge_health/
capture_tab/click/type/key/scroll`.

**Trading / MT4 (11 tools)** — `mt4_status/list_symbols/get_market_data/
get_open_positions/execute_trade/close_position/verify_ticket/
marketwatch_status/export_history/get_historical_bars/run_backtest`.

**System / meta (4 tools)** — `system_info`, `list_categories`,
`list_house_victoria_tools` (self-discovery), `save_to_file_retrieval`.

**Filesystem** — read/write/list via `house_victoria_data` MCP.

**Hermes native** — `terminal` (shell), `web` (fetch/browse).

### Hardcoded side-effects (NOT agency — keyword/regex)

13 Unreal world verbs (`spawn_avatar`, `move_avatar`, `rotate_avatar`,
`update_pose`, `animate_avatar`, `look_at`, `focus_avatar`, `set_locomotion`,
`touch_interact`, `companion_remote_exchange`, `get_scene_info`,
`capture_scene`, `get_avatar_state`, `wander`, `status`). These fired from
regex over assistant text in `VictoriaEmbodimentIntentsParser`. SoulCore's 5
verbs inherit this pattern.

---

## Proposed architecture

### Core: tool-loop inference

Replace the single-shot `CompleteAsync` path with an **agent loop**:

```text
User message
    │
    ▼
Host builds messages + tool schemas
    │
    ▼
Ollama /api/chat (tools=[...]) ──► model decides
    │                                │
    │  ◄── text reply ──────────────┤
    │                               │
    │  ◄── tool_calls[] ────────────┘
    │
    ▼
Host executes each tool_call → result
    │
    ▼
Append {role:"tool", tool_name, content:result} → re-prompt
    │
    ▼
Model continues (may call more tools or finish)
    │
    ▼
Final reply → chat.done + body side-effects
```

### Two inference paths

| Path | Engine | Tools | When |
| --- | --- | --- | --- |
| **Ollama native** | `/api/chat` with `tools` + `tool_calls` | SoulCore tool registry (C# implementations) | Local-first, no Hermes |
| **Hermes** | `/v1/chat/completions` with `tools` + `tool_choice` + SSE | MCP servers (stdio) + Hermes native | When Hermes is up + has MCP wired |

Both paths converge on the same **tool registry interface** so Victoria's
tools work regardless of which inference backend is active.

### Tool registry (SoulCore)

```csharp
public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> GetDefinitions();
    Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct);
}

public sealed record ToolDefinition(string Name, string Description, JsonElement Parameters);
public sealed record ToolResult(bool Success, string Content, object? Data = null);
```

Tools registered as DI singletons. Each tool is a small class implementing
the execute contract. The Host builds `tools[]` from the registry for every
chat turn, dispatches `tool_calls`, and feeds results back.

### Tool restoration priority

| Wave | Tools | Why |
| --- | --- | --- |
| **1 — Core agency** | `recall_memory`, `store_memory`, `speak`, `wave`/`play_animation`, `move_to`/`loco`, `look_at`, `set_emotion` | She can remember, talk, and act in her body. Promotes existing side-effects to real tools. |
| **2 — Environment** | `go_to`, `interact`, `sit`, `query_environment`, `list_objects` | Phase 3 interaction becomes tools, not keywords. |
| **3 — Self + system** | `list_tools` (self-discovery), `system_info`, `read_file`/`write_file` (scoped) | She knows what she can do; basic file ops. |
| **4 — Desktop + browser** | `desktop_screenshot`, `desktop_click`, `desktop_type`, `browser_capture`, `browser_click`, `browser_type` | Restore computer-use. **Security gate required** — see below. |
| **5 — Trading** | MT4 tools (11) | Highest risk; gate behind explicit user authorization per session. |
| **6 — Task/workflow** | `task_create/get/update/list`, `workflow_*` | She can manage her own work. |

### Body verbs become tools

Today's 5 keyword side-effects become tool implementations:

| Current | Becomes tool | Keyword kept as fallback? |
| --- | --- | --- |
| `DetectLocoIntent` → `LocoAsync` | `move_to` / `walk_forward` tool | Yes (user says "walk") |
| `DetectAnimationIntent` → `PlayAnimationAsync` | `play_animation` tool | Yes |
| `DetectLookIntent` → `LookAsync` | `look_at` tool | Yes |
| always `SpeakAsync` | `speak` tool (or auto after reply) | Auto |
| always `SetEmotionAsync` | `set_emotion` tool | Auto |

The model can now **decide** to wave goodbye, walk to a window, or look at
something — not just react to your keywords.

---

## Critical: model must support tool-calling

Ollama supports tool calling via `/api/chat`, but **not all models do it well**.

| Model | Tool calling | Installed? |
| --- | --- | --- |
| `gemma4:latest` | **Not confirmed** on tool-calling lists (gemma family not listed) | Yes (8.95 GB) |
| `qwen2.5:7b` | **Good** | No |
| `qwen2.5:14b` | **Very good** | No |
| `llama3.1:8b` | **Good** | No |
| `mistral` (7B) | **Good** | No |

**Decision needed:** `gemma4` is the current chat model but may not support
tool-calling reliably. Options:
- (A) Test `gemma4` tool-calling — if it works, keep it.
- (B) Switch to `qwen2.5:14b` for the agent loop (very good tool-calling, 9.5 GB
  Q4 fits the 16 GB card alongside context). Keep `gemma4` as fallback for
  non-tool chat.
- (C) Use Hermes (if restored) as the tool-loop gateway with its own model.

This is the single biggest technical risk in the proposal.

---

## Security gates (must design before desktop/trading tools)

| Tool class | Gate |
| --- | --- |
| Body / memory / emotion | No gate (safe, local) |
| Filesystem | Whitelisted roots only (like LLMOD `house_victoria_data`) |
| Desktop control | **Session opt-in** — user must enable "AllowComputerControl" per session |
| Browser | Session opt-in or always-on (read-only capture is safe; click/type gated) |
| Trading (MT4) | **Per-trade confirmation** + SL required (as LLMOD had) |
| Shell/terminal | Whitelisted commands or confirmation prompt |

The tool registry must enforce gates: a tool can refuse execution and return
"requires user authorization" to the model, which then asks the user.

---

## Workstreams

### Phase A — Agent loop foundation (blocking)

| # | Work | Role |
| --- | --- | --- |
| A.1 | `IToolRegistry` + `ToolDefinition` + `ToolResult` types | BED |
| A.2 | Tool-loop in `OllamaInferenceClient` (or new `OllamaAgentClient`): `/api/chat` with `tools`, parse `tool_calls`, execute, feed back, re-prompt (cap iterations) | BED |
| A.3 | Tool-loop in `HermesHttpClient`: add `tools` + `tool_choice` + `tool_calls` parsing to `/v1/chat/completions` | BED |
| A.4 | Wire tool-loop into `ChatWebSocketHandler` — replace keyword-only path with tool-driven dispatch (keep keywords as fallback) | BED |
| A.5 | Model tool-calling test: verify `gemma4` vs `qwen2.5` vs `llama3.1` — pick the one that works | BED + QA |

**Exit gate:** model emits a `tool_call` for `recall_memory` and the Host
executes it and feeds the result back; model uses the result in its reply.

### Phase B — Core tool implementations

| # | Work | Role |
| --- | --- | --- |
| B.1 | `recall_memory` tool (wraps `IMemoryStore.RecallSimilarAsync`) | BED |
| B.2 | `store_memory` tool (wraps episodic store) | BED |
| B.3 | Body tools: `speak`, `play_animation`, `move_to`/`walk_forward`, `look_at`, `set_emotion` (wrap existing `IUnrealVerbClient`) | BED |
| B.4 | `list_tools` self-discovery tool | BED |
| B.5 | `system_info` tool | BED |
| B.6 | Filesystem tools (scoped, whitelisted roots) | BED |

**Exit gate:** Victoria can recall a memory, wave, and walk — all by her own
decision, not keyword detection.

### Phase C — Desktop + browser + trading (gated)

| # | Work | Role |
| --- | --- | --- |
| C.1 | Desktop capture/click/type tools + session opt-in gate | BED |
| C.2 | Browser capture/click/type tools | BED |
| C.3 | MT4 bridge tools (11) + per-trade confirmation gate | BED |
| C.4 | Terminal tool (whitelisted or confirmed) | BED |

### Phase D — Task/workflow system

| # | Work | Role |
| --- | --- | --- |
| D.1 | `task_create/get/update_status/list` tools | BED |
| D.2 | `workflow_create/execute/get` tools | BED |

---

## Decisions needed from user

1. **Tool-calling model** — test `gemma4` first, or switch to `qwen2.5:14b`
   (recommended for reliable tool-calling on your 16 GB card)?
2. **Hermes restoration** — should we restore the Hermes tool-loop gateway
   (Python `:8642` with MCP servers), or build tool-calling natively in
   SoulCore's C# Host (Ollama `/api/chat` + C# tool registry)?
3. **Tool scope** — all 6 waves, or start with Phase A+B (agency + core tools)
   and defer desktop/browser/trading?
4. **Security gates** — confirm the gate model (session opt-in for desktop,
   per-trade for trading, whitelisted filesystem roots).

## Risks

- `gemma4` may not support tool-calling → forces model switch.
- Tool-loop adds latency (multiple round-trips) to chat; cap iterations.
- Desktop/trading tools are powerful — gates must be solid before enabling.
- Hermes restoration (if chosen) is a large infra piece (Python gateway, MCP
  server wiring, `~/.hermes/config.yaml`).
- Concurrent with embodiment phases — body tools depend on BED-114/117
  (Character + AIController) being done for `move_to` to actually walk.
