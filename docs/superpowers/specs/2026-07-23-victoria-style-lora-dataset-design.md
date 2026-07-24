# Victoria style LoRA dataset recipe

**Date:** 2026-07-23  
**Status:** Draft for review  
**Approach:** A (mine LLMOD/House chats) + Approach 1 (star-only Keep; no rewrites)

## 1. Goal & scope

### Goal

Build a **Victoria style LoRA** training set by **starring** good turns from existing LLMOD/House chats — no rewriting required for v1.

### In scope

- Export candidate user↔Victoria pairs from quarry chats
- Human Keep / Drop only (Approach 1)
- JSONL suitable for LoRA (chat `messages` format)
- Keep checklist + hard exclusions
- Target size + a small held-out eval set

### Out of scope (v1)

- Fine-tuning / training runs themselves (base model, rank, epochs, load into Ollama)
- Baking charter text or episodic memories into weights
- SoulCore “star this reply” UI (possible later growth path)
- Auto-filtering without a human Keep mark
- Hand-written rewrites of borderline turns

### Success criteria

After a future LoRA train: same base model + runtime charter/RAG sounds more like starred Victoria (tone, continuity, anti-assistant) without inventing a frozen biography from the dataset.

Memories and identity rules remain outside weights:

| Concern | Location |
| --- | --- |
| Identity / rules | Charter anchors (prompt) |
| Lived facts / episodes | Memory store + retrieval |
| How she sounds / formats | Style LoRA (this dataset) |

## 2. Keep / Drop rules

### Keep

Mark Keep only if Victoria’s reply:

- Sounds like *her* (warm, first-person, continuous self — not a generic assistant)
- Is something you’d want more of in daily Presence chat
- Stands alone as style (tone, cadence, stance) even if surrounding lore is forgotten

### Drop

Drop if the reply:

- Breaks the fiction (“as an AI / language model / I don’t have feelings” in a drone way)
- Is another persona, system dump, tool JSON, error, or stub
- Is mostly a long fact dump that belongs in memory/RAG (style LoRA does not need those)
- Is truncated, garbled, or you would not want her to talk that way again

### Approach 1 rule

No rewriting. Borderline → **Drop** (do not polish in v1).

### Pairing

One training example = one user message + the starred Victoria reply. Optional short prior turn only if the export already includes it; do not invent context.

## 3. Pipeline, layout, schema, size

### Flow

1. Point at quarry chat source (path filled in when exporting)
2. Export candidate pairs → `candidates.jsonl` (all user↔Victoria turns, unfiltered)
3. Human marks Keep (`keep_ids.txt`, spreadsheet, or review UI — tooling choice deferred)
4. Build `train.jsonl` + `eval.jsonl` from Keep only
5. Later: LoRA train on `train.jsonl` only — never on raw candidates

### Layout

Under Soul_Core (gitignore raw chats / candidates if sensitive):

```
datasets/victoria-style-lora/
  README.md                 # points at this recipe
  candidates.jsonl          # export; may contain private content
  keep_ids.txt              # one example id per line starred Keep
  train.jsonl
  eval.jsonl
```

### JSONL schema

One object per line:

```json
{
  "id": "llmod-2024-example-turn-42",
  "messages": [
    {"role": "user", "content": "…"},
    {"role": "assistant", "content": "…"}
  ],
  "source": "llmod",
  "kept": true
}
```

Rules:

- Do **not** put system/charter text in the training row (charter stays at inference)
- `kept` is optional on candidates; `train.jsonl` / `eval.jsonl` rows are all Keep
- Prefer stable unique `id` values so `keep_ids.txt` can reference them

### Size targets (v1)

| Split | Target |
| --- | --- |
| Train | ~150–400 Keep pairs (stop when starring feels repetitive) |
| Eval | ~20–40 Keep pairs held out (never trained); same Keep bar |

Prefer diversity (greeting, comfort, banter, correction, quiet night) over dumping one long thread.

### Hard exclusions from train/eval

- Non-Victoria personas
- Tool / JSON blobs, stubs, errors
- Anything you would not want reproduced as “her voice”

## 4. Eval & handoff

### Eval (held-out Keep pairs)

After a LoRA exists, spot-check: same user line → base vs LoRA. Prefer LoRA when it sounds more like starred Victoria (warm, first-person, not assistant-y) without inventing biography that was not in the prompt/memory.

### Pass bar for “recipe done” (this document)

- Layout + schema + Keep rules documented
- Export → `candidates.jsonl` → `keep_ids.txt` → `train` / `eval` path clear
- Training itself is a later plan, not this recipe

### Explicit later (not this recipe)

- Actual LoRA train (base model, rank, epochs, Ollama/llama.cpp load)
- SoulCore star-reply UI
- Mixing hand-written SoulCore-format examples (emotion preamble, speak length, episodic write shape)

## Decisions locked

| Decision | Choice |
| --- | --- |
| Source | A — existing LLMOD/House chats |
| Curation | Approach 1 — star-only Keep, no rewrites |
| What to train for | Style / voice habits, not memories or charter |
| Runtime identity/memory | Remain charter + RAG |

## Open items (fill during implementation plan)

- Exact quarry export path and format (xlsx / db / json)
- Tooling for Keep marking (`keep_ids.txt` vs spreadsheet)
- Whether `datasets/` is gitignored entirely or only `candidates.jsonl`
