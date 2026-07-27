---
type: issue
id: ISSUE-20260726-001
severity: P1
status: closed
created: 2026-07-26
filed_by: PM-01
related_task: TASK-126, TASK-129, TASK-128
gate: QA-130 (Phase A exit — recall_memory tool_call round trip)
updated: 2026-07-26 (closed by BED-01 in TASK-128: Ollama path back-filled with the fallback parser; Hermes path done in BED-127. Fallback now symmetric across both backends.)
---

# ISSUE-001 — BED-126 Ollama tool-loop missing qwen2.5 content-embedded-JSON fallback parser

## Severity

**P1 — Reliability gap. The agent loop will drop tool calls on flaky `qwen2.5:14b` runs where the model leaks the tool call as bare JSON in `message.content` instead of `message.tool_calls`.**

Not P0 because: the model does emit structured `tool_calls` reliably with a strong imperative system prompt (BED-129 confirmed 3/3 structured passes). The flakiness is intermittent and prompt-dependent, not a hard failure. But in production, without the fallback, Victoria will silently fail to act on the flaky runs — she'll reply with text containing the leaked JSON instead of executing the tool. This defeats the purpose of the agent loop.

## Summary

BED-129 (model switch verify) empirically confirmed a **known Ollama flakiness with the `qwen2.5` model family** (ollama/ollama #13968, #12174; NousResearch/hermes-agent #5867):

- The model's chat template instructs it to wrap tool calls in XML-like tags.
- The model frequently emits bare JSON without the tags, e.g. `{"name": "echo", "arguments": {"text": "hello"}}` in `message.content`.
- Ollama's parser does not promote this to `message.tool_calls` — it stays in `content` with `tool_calls: null`.

BED-129's report explicitly recommended BED-126 implement a **content-embedded-JSON fallback parser**: when `message.tool_calls` is null/empty, attempt to parse `message.content` as JSON (or extract a JSON object matching `{"name":"...","arguments":{...}}`); if parse succeeds and `name` matches a registered tool, dispatch it as a tool_call.

**BED-126 landed without this fallback.** BED-126's report only documents defensive `arguments` parse (object/string forms) — that handles the `arguments` field shape, NOT the bare-JSON-in-`content` leak case. PM attempted to inject the requirement mid-flight but the subagent was still running and could not be resumed.

## Impact

- **Symptom**: On flaky `qwen2.5:14b` runs, Victoria receives a text reply containing `{"name":"...","arguments":{...}}` instead of a structured `tool_calls` array. The current BED-126 loop treats this as a normal text reply (no tool call this round) and returns the leaked-JSON text to the user.
- **User-visible failure**: Victoria says something like `{"name":"recall_memory","arguments":{"query":"..."}}` instead of actually recalling memory. The agent loop is bypassed.
- **Frequency**: Intermittent, prompt-dependent. BED-129 saw it on the first softer-prompt attempt; a strong imperative prompt avoided it 3/3 times. But chat prompts in production will vary, so the flakiness will surface.

## Reproduction

1. Pull `qwen2.5:14b` (already pulled — 9.0 GB, ID `7cdf5a0187d5`).
2. POST to `http://127.0.0.1:11434/api/chat` with a `tools[]` array and a **soft** system prompt (e.g. just "You are a helpful assistant." — no imperative tool-calling instruction).
3. Observe: some runs return `message.content` containing `{"name":"...","arguments":{...}}` with `tool_calls: null`.
4. Reference logs: `SoulCore/scripts/smoke-tool-call-129-run1.log` (the structured pass) vs. the first softer-prompt attempt documented in BED-129's report §3 ("Known qwen2.5 tool-call flakiness").

## Recommended fix

In `SoulCore/SoulCore.Inference/OllamaInferenceClient.cs` `CompleteWithToolsAsync`:

1. After parsing the Ollama `/api/chat` response, check `message.tool_calls` first (current behavior).
2. If `tool_calls` is null/empty, attempt to parse `message.content` as JSON. Accept either:
   - A pure JSON object `{"name":"...","arguments":{...}}` (the whole content).
   - A JSON object embedded in text (extract via regex or brace-matching).
3. If parse succeeds and `name` matches a tool in the registry, dispatch it as a tool_call (same path as structured `tool_calls`).
4. If parse fails or `name` is not a registered tool, treat as a normal text reply (no tool call this round) — current behavior.
5. Log when the fallback fires at Information level (`"tool call recovered from content-embedded JSON"`) so we can track flakiness frequency in production.
6. Add a unit test: mock HTTP returns `content: '{"name":"echo","arguments":{"text":"hello"}}'` with `tool_calls: null` → verify the tool still dispatches and the loop continues.

## Affected files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Inference/OllamaInferenceClient.cs` | Add content-embedded-JSON fallback parser to `CompleteWithToolsAsync` |
| `SoulCore/SoulCore.Protocol.Tests/OllamaToolLoopTests.cs` | Add test: `ToolCall_LeakedInContent_FallbackParsesAndDispatches` |

## Related tickets

- **BED-126** (landed without the fallback) — `reports/TASK-20260726-126-BED01-to-PM01.md`
- **BED-129** (flagged the flakiness + recommended the fallback) — `reports/TASK-20260726-129-BED01-to-PM01.md`
- **BED-127** (Hermes tool-loop, in flight) — should also implement the same fallback for symmetry (Hermes may route through Ollama too). PM to note in BED-127 dispatch.
- **QA-130** (Phase A exit gate) — should test the fallback path: use a soft prompt that triggers the leak, verify Victoria still recalls memory via the fallback.

## Upstream references

- ollama/ollama #13968 — "Qwen2.5:14b output json tool call leak - INCORRECT TOOL CALL"
- ollama/ollama #12174 — `qwen3:8b` works through the same pipeline but qwen2.5 leaks
- NousResearch/hermes-agent #5867 — same; workaround is content-JSON fallback parse

## Status

**Closed (2026-07-26) by BED-01 in TASK-128.** The Ollama path back-filled the
`TryRecoverToolCallsFromContent` fallback parser into
`OllamaInferenceClient.CompleteWithToolsAsync` (mirroring BED-127's Hermes
implementation). The Hermes path was done in BED-127. **The fallback is now
symmetric across both backends** — both Ollama (`/api/chat`) and Hermes
(`/v1/chat/completions`) recover qwen2.5 content-embedded-JSON tool-call leaks.

### Closure evidence

- `SoulCore/SoulCore.Inference/OllamaInferenceClient.cs` — `TryRecoverToolCallsFromContent` + `TryParseRecoveryObject` + `ExtractFirstJsonObject` + `ContainsRecoverableToolCall` + `ParseStringArguments` added (mirrors `HermesHttpClient.TryRecoverToolCallsFromContent`).
- `SoulCore/SoulCore.Protocol.Tests/OllamaToolLoopTests.cs` — 5 new fallback tests (the 4 required by the dispatch + 1 bonus for string-form `arguments`):
  - `FallbackParser_ContentEmbeddedJson_DispatchesTool_WhenToolCallsNull` — core ISSUE-001 test
  - `FallbackParser_JsonEmbeddedInText_DispatchesTool` — brace-matching extraction
  - `FallbackParser_NameNotRegistered_TreatedAsTextReply` — no dispatch on unregistered name
  - `FallbackParser_NoToolsAdvertised_DoesNotAttemptRecovery` — no recovery without tools
  - `FallbackParser_StringFormArguments_IsParsedToObject` — string-form `arguments` in content-embedded JSON
- All 145 unit tests pass (132 existing + 13 new across TASK-128).
- Logs at Information level when the fallback fires: `"Ollama tool call recovered from content-embedded JSON at iteration {Iter} (count={Count})"`.

Originally filed as a follow-up to BED-126. PM folded the fix into BED-128
(ChatWebSocketHandler wiring) since BED-128 also owns the production chat
path that depends on the fallback for reliability.
