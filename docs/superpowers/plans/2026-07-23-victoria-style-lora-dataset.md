# Victoria Style LoRA Dataset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export Victoria chat turns from the LLMOD quarry SQLite DB into star-only Keep JSONL (`candidates` → `keep_ids` → `train`/`eval`) ready for a future style LoRA, without baking memories or charter into weights.

**Architecture:** Read-only export from quarry `Messages` for the Victoria AI contact only; pair consecutive `Outgoing` (user) → `Incoming` (assistant) text turns into JSONL. Human Keep via `keep_ids.txt`. Deterministic build script filters Keep rows and splits a held-out eval set. No fine-tuning in this plan.

**Tech Stack:** Python 3.11+ (stdlib `sqlite3` + `json` only), PowerShell for smoke checks, git + `.gitignore` for private JSONL.

## Global Constraints

- Source DB (read-only): `C:\Users\kurtw\LLMOD\LLMOD-max-master\Data\Memory\HouseVictoria.db`
- Victoria contact id: `977d778f-2a33-4bca-aab9-4ff893463162` → conversation id `conv-977d778f-2a33-4bca-aab9-4ff893463162`
- Direction map: `Outgoing` = user, `Incoming` = assistant
- Approach 1: star-only Keep — no rewrites; borderline → Drop
- Do not put system/charter text in training rows
- Do not export non-Victoria personas (Lucy, Susan, SUE, LEXI, Val, etc.)
- Never commit `candidates.jsonl`, `train.jsonl`, or `eval.jsonl` (private chat content)
- Spec: `docs/superpowers/specs/2026-07-23-victoria-style-lora-dataset-design.md`
- Out of scope: LoRA train, SoulCore star UI, hand-written format examples

## File structure

| Path | Responsibility |
| --- | --- |
| `datasets/victoria-style-lora/README.md` | How to export, Keep, build splits; links to spec |
| `datasets/victoria-style-lora/KEEP.md` | Human Keep/Drop checklist (from spec §2) |
| `datasets/victoria-style-lora/.gitignore` | Ignore JSONL payloads; allow README/KEEP/tools/keep_ids |
| `datasets/victoria-style-lora/keep_ids.txt` | One candidate `id` per Keep line (committed OK — ids only) |
| `datasets/victoria-style-lora/candidates.jsonl` | Generated export (gitignored) |
| `datasets/victoria-style-lora/train.jsonl` | Generated Keep train split (gitignored) |
| `datasets/victoria-style-lora/eval.jsonl` | Generated Keep eval split (gitignored) |
| `datasets/victoria-style-lora/tools/export_candidates.py` | SQLite → candidates.jsonl |
| `datasets/victoria-style-lora/tools/build_splits.py` | keep_ids + candidates → train/eval |
| `datasets/victoria-style-lora/tools/test_export_candidates.py` | Unit tests for pairing / id stability |
| `datasets/victoria-style-lora/tools/test_build_splits.py` | Unit tests for Keep filter + eval holdout |
| `.gitignore` (repo root) | Optional pointer comment only if needed; prefer dataset-local ignore |

---

### Task 1: Dataset folder, ignore rules, docs

**Files:**
- Create: `datasets/victoria-style-lora/.gitignore`
- Create: `datasets/victoria-style-lora/README.md`
- Create: `datasets/victoria-style-lora/KEEP.md`
- Create: `datasets/victoria-style-lora/keep_ids.txt` (empty stub with comment header)
- Create: `datasets/victoria-style-lora/tools/` (directory)

**Interfaces:**
- Consumes: Spec sections 2–3
- Produces: Documented layout matching spec `datasets/victoria-style-lora/`

- [ ] **Step 1: Create directory and `.gitignore`**

Create `datasets/victoria-style-lora/.gitignore` with:

```
# Private chat content — never commit
candidates.jsonl
train.jsonl
eval.jsonl
*.jsonl.bak
```

- [ ] **Step 2: Write `KEEP.md`**

Copy Keep/Drop rules from the spec into `datasets/victoria-style-lora/KEEP.md` (Keep bullets, Drop bullets, Approach 1 no-rewrite rule, pairing definition). Keep it short — checklist for starring, not a second design doc.

- [ ] **Step 3: Write `README.md`**

Include:

1. Link to `docs/superpowers/specs/2026-07-23-victoria-style-lora-dataset-design.md`
2. Default DB path and Victoria conversation id
3. Commands:

```powershell
python datasets/victoria-style-lora/tools/export_candidates.py
# edit keep_ids.txt (one id per line) using KEEP.md
python datasets/victoria-style-lora/tools/build_splits.py
```

4. Note that JSONL files are gitignored; `keep_ids.txt` may be committed

- [ ] **Step 4: Create empty `keep_ids.txt`**

```
# One candidate id per line (lines starting with # ignored).
# Example: llmod-977d778f-msg-<outgoingMessageId>
```

- [ ] **Step 5: Commit**

```powershell
git add datasets/victoria-style-lora/.gitignore datasets/victoria-style-lora/README.md datasets/victoria-style-lora/KEEP.md datasets/victoria-style-lora/keep_ids.txt
git commit -m "docs: scaffold Victoria style LoRA dataset folder"
```

---

### Task 2: Export candidates from quarry SQLite (TDD)

**Files:**
- Create: `datasets/victoria-style-lora/tools/export_candidates.py`
- Create: `datasets/victoria-style-lora/tools/test_export_candidates.py`
- Test: run `python -m unittest` from `tools/`

**Interfaces:**
- Consumes: quarry DB `Messages` table columns `Id, ConversationId, Content, Direction, Type, Timestamp`
- Produces: `export_pairs(conn, conversation_id) -> list[dict]` and CLI writing `candidates.jsonl`
- Each dict shape:

```python
{
  "id": str,  # f"llmod-{contact_short}-msg-{outgoing_id}"
  "messages": [
    {"role": "user", "content": str},
    {"role": "assistant", "content": str},
  ],
  "source": "llmod",
  "kept": False,
  "meta": {
    "conversation_id": str,
    "outgoing_id": str,
    "incoming_id": str,
    "timestamp_user": str,
    "timestamp_assistant": str,
  },
}
```

- Pairing algorithm: within one conversation, load `Type='Text'` rows ordered by `Timestamp ASC, Id ASC`. Walk with index `i`; when `Direction[i]=='Outgoing'` and `Direction[i+1]=='Incoming'`, emit one pair and skip both; otherwise advance by 1.
- Skip empty/whitespace content on either side.
- Default CLI args:
  - `--db` default `C:\Users\kurtw\LLMOD\LLMOD-max-master\Data\Memory\HouseVictoria.db`
  - `--conversation-id` default `conv-977d778f-2a33-4bca-aab9-4ff893463162`
  - `--out` default `datasets/victoria-style-lora/candidates.jsonl` (resolve relative to repo root)

- [ ] **Step 1: Write failing tests**

Create `datasets/victoria-style-lora/tools/test_export_candidates.py`:

```python
import sqlite3
import unittest

from export_candidates import export_pairs


class ExportPairsTests(unittest.TestCase):
    def setUp(self):
        self.conn = sqlite3.connect(":memory:")
        self.conn.execute(
            """
            CREATE TABLE Messages (
              Id TEXT, ConversationId TEXT, Content TEXT,
              Direction TEXT, Type TEXT, Timestamp TEXT
            )
            """
        )
        self.conv = "conv-test"

    def _ins(self, mid, direction, content, ts):
        self.conn.execute(
            "INSERT INTO Messages VALUES (?,?,?,?,?,?)",
            (mid, self.conv, content, direction, "Text", ts),
        )

    def test_pairs_outgoing_then_incoming(self):
        self._ins("u1", "Outgoing", "hi", "2026-01-01T00:00:00")
        self._ins("a1", "Incoming", "hello love", "2026-01-01T00:00:01")
        pairs = export_pairs(self.conn, self.conv)
        self.assertEqual(len(pairs), 1)
        self.assertEqual(pairs[0]["messages"][0]["role"], "user")
        self.assertEqual(pairs[0]["messages"][0]["content"], "hi")
        self.assertEqual(pairs[0]["messages"][1]["role"], "assistant")
        self.assertEqual(pairs[0]["messages"][1]["content"], "hello love")
        self.assertTrue(pairs[0]["id"].endswith("u1"))
        self.assertEqual(pairs[0]["kept"], False)

    def test_skips_non_text_and_empty(self):
        self._ins("u1", "Outgoing", "  ", "2026-01-01T00:00:00")
        self._ins("a1", "Incoming", "x", "2026-01-01T00:00:01")
        self._ins("u2", "Outgoing", "ok", "2026-01-01T00:00:02")
        self._ins("a2", "Incoming", "yes", "2026-01-01T00:00:03")
        pairs = export_pairs(self.conn, self.conv)
        self.assertEqual(len(pairs), 1)
        self.assertEqual(pairs[0]["messages"][0]["content"], "ok")

    def test_does_not_pair_two_outgoing(self):
        self._ins("u1", "Outgoing", "a", "2026-01-01T00:00:00")
        self._ins("u2", "Outgoing", "b", "2026-01-01T00:00:01")
        self._ins("a1", "Incoming", "c", "2026-01-01T00:00:02")
        pairs = export_pairs(self.conn, self.conv)
        # u2->a1 pairs; u1 left unpaired
        self.assertEqual(len(pairs), 1)
        self.assertEqual(pairs[0]["messages"][0]["content"], "b")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run tests — expect fail**

```powershell
cd C:\Users\kurtw\Soul_Core\datasets\victoria-style-lora\tools
python -m unittest test_export_candidates.py -v
```

Expected: `ModuleNotFoundError` or `ImportError: cannot import name 'export_pairs'`

- [ ] **Step 3: Implement `export_candidates.py`**

Implement `export_pairs` and a `__main__` CLI that:

1. Opens DB read-only: `sqlite3.connect(f"file:{db}?mode=ro", uri=True)`
2. Calls `export_pairs`
3. Writes one JSON object per line to `--out` (UTF-8, `ensure_ascii=False`)
4. Prints pair count to stdout: `exported N pairs -> path`

Stable id: `llmod-977d778f-msg-{outgoing_id}` (first 8 chars of contact uuid from conversation id after `conv-`, or hardcode `977d778f` when using the default conversation).

Do **not** filter by “sounds like Victoria” here — that is human Keep. Export only the Victoria conversation id so other personas never enter candidates.

- [ ] **Step 4: Run tests — expect pass**

```powershell
cd C:\Users\kurtw\Soul_Core\datasets\victoria-style-lora\tools
python -m unittest test_export_candidates.py -v
```

Expected: `OK` (3 tests)

- [ ] **Step 5: Smoke export against quarry DB**

```powershell
cd C:\Users\kurtw\Soul_Core
python datasets/victoria-style-lora/tools/export_candidates.py
Get-Content datasets/victoria-style-lora/candidates.jsonl -TotalCount 1
(Get-Content datasets/victoria-style-lora/candidates.jsonl | Measure-Object -Line).Lines
```

Expected: stdout `exported ~190+ pairs` (probe saw ~196); first line is valid JSON with `role` user/assistant; `git status` does **not** list `candidates.jsonl` as untracked to commit (ignored).

- [ ] **Step 6: Commit**

```powershell
git add datasets/victoria-style-lora/tools/export_candidates.py datasets/victoria-style-lora/tools/test_export_candidates.py
git commit -m "feat: export Victoria style LoRA candidates from quarry SQLite"
```

Do **not** `git add` `candidates.jsonl`.

---

### Task 3: Build train/eval from `keep_ids.txt` (TDD)

**Files:**
- Create: `datasets/victoria-style-lora/tools/build_splits.py`
- Create: `datasets/victoria-style-lora/tools/test_build_splits.py`

**Interfaces:**
- Consumes: `candidates.jsonl`, `keep_ids.txt`
- Produces: `build_splits(candidates, keep_ids, eval_ratio=0.1, seed=42) -> tuple[list, list]`
- CLI writes `train.jsonl` and `eval.jsonl`
- Rules:
  - Parse `keep_ids.txt`: strip, skip blanks and `#` comments
  - Keep set must match candidate `id`s; warn (print) unknown ids; ignore them
  - Selected rows get `"kept": true`
  - Sort Keep rows by `id` for stability, then shuffle with `random.Random(seed)` and take `max(1, int(len*eval_ratio))` for eval when `len >= 10`; if `len < 10`, put all in train and write empty eval with a printed warning
  - Spec targets: train 150–400, eval 20–40 — scripts do not enforce quotas; print counts and whether below/above target bands

- [ ] **Step 1: Write failing tests**

```python
import unittest

from build_splits import build_splits, parse_keep_ids


class BuildSplitsTests(unittest.TestCase):
    def test_parse_keep_ids_skips_comments(self):
        text = "# hi\nid-1\n\nid-2\n"
        self.assertEqual(parse_keep_ids(text), ["id-1", "id-2"])

    def test_build_splits_filters_and_marks_kept(self):
        cands = [
            {"id": "a", "messages": [], "kept": False},
            {"id": "b", "messages": [], "kept": False},
            {"id": "c", "messages": [], "kept": False},
        ]
        # 10+ ids so eval is non-empty — pad
        for i in range(10):
            cands.append({"id": f"x{i}", "messages": [], "kept": False})
        keep = ["a", "b", "c"] + [f"x{i}" for i in range(10)]
        train, ev = build_splits(cands, keep, eval_ratio=0.1, seed=42)
        self.assertTrue(all(r["kept"] is True for r in train + ev))
        ids = {r["id"] for r in train + ev}
        self.assertEqual(ids, set(keep))
        self.assertGreaterEqual(len(ev), 1)
        self.assertEqual(len(train) + len(ev), len(keep))

    def test_unknown_keep_ids_ignored(self):
        cands = [{"id": "a", "messages": [], "kept": False}]
        train, ev = build_splits(cands, ["a", "missing"], eval_ratio=0.1, seed=1)
        self.assertEqual([r["id"] for r in train + ev], ["a"])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run tests — expect fail**

```powershell
cd C:\Users\kurtw\Soul_Core\datasets\victoria-style-lora\tools
python -m unittest test_build_splits.py -v
```

Expected: import failure for `build_splits`

- [ ] **Step 3: Implement `build_splits.py`**

Implement `parse_keep_ids`, `build_splits`, and CLI:

```powershell
python datasets/victoria-style-lora/tools/build_splits.py
# optional: --candidates --keep-ids --train-out --eval-out --eval-ratio 0.1 --seed 42
```

Print: `kept N | train T | eval E | unknown_ids U`

- [ ] **Step 4: Run tests — expect pass**

```powershell
cd C:\Users\kurtw\Soul_Core\datasets\victoria-style-lora\tools
python -m unittest test_build_splits.py -v
```

Expected: `OK`

- [ ] **Step 5: Commit**

```powershell
git add datasets/victoria-style-lora/tools/build_splits.py datasets/victoria-style-lora/tools/test_build_splits.py
git commit -m "feat: build Victoria style LoRA train/eval from keep_ids"
```

---

### Task 4: Human Keep pass + first real splits

**Files:**
- Modify: `datasets/victoria-style-lora/keep_ids.txt` (human)
- Generate (gitignored): `train.jsonl`, `eval.jsonl`

**Interfaces:**
- Consumes: `candidates.jsonl` from Task 2, `KEEP.md`
- Produces: populated `keep_ids.txt`; non-empty `train.jsonl` / `eval.jsonl` when Keep ≥ 10

- [ ] **Step 1: Ensure candidates exist**

```powershell
cd C:\Users\kurtw\Soul_Core
python datasets/victoria-style-lora/tools/export_candidates.py
```

- [ ] **Step 2: Review helper (optional one-liner)**

Print random sample of 20 candidates for starring:

```powershell
python -c "import json,random; rows=[json.loads(l) for l in open('datasets/victoria-style-lora/candidates.jsonl',encoding='utf-8')]; random.seed(0); random.shuffle(rows);
[print(r['id'], '|', r['messages'][0]['content'][:60], '=>', r['messages'][1]['content'][:80]) for r in rows[:20]]"
```

- [ ] **Step 3: Star Keep ids**

Using `KEEP.md`, append Keep ids to `keep_ids.txt`. Target band for a first usable set: **≥ 30 Keep** (enough for smoke eval); stretch toward **150–400** over later sessions. Do not rewrite replies.

- [ ] **Step 4: Build splits**

```powershell
python datasets/victoria-style-lora/tools/build_splits.py
```

Expected: printed counts; `train.jsonl` / `eval.jsonl` exist; both gitignored.

- [ ] **Step 5: Commit only `keep_ids.txt` (if any ids)**

```powershell
git add datasets/victoria-style-lora/keep_ids.txt
git commit -m "chore: add Victoria style LoRA keep_ids from starred chats"
```

Skip commit if still empty after review session — leave a note in the PR/handoff instead.

---

### Task 5: Recipe completion checklist

**Files:**
- Modify: `docs/superpowers/specs/2026-07-23-victoria-style-lora-dataset-design.md` (status → Accepted / recipe implemented)
- Optional: append a short “Implemented” note with export path defaults to the spec Open items section

- [ ] **Step 1: Verify ignore**

```powershell
cd C:\Users\kurtw\Soul_Core
git check-ignore -v datasets/victoria-style-lora/candidates.jsonl
git check-ignore -v datasets/victoria-style-lora/train.jsonl
git status -sb
```

Expected: both JSONL ignored; working tree does not stage private chat files.

- [ ] **Step 2: Run full unit suite**

```powershell
cd C:\Users\kurtw\Soul_Core\datasets\victoria-style-lora\tools
python -m unittest discover -v
```

Expected: all tests PASS.

- [ ] **Step 3: Update spec status line**

Change `**Status:** Draft for review` → `**Status:** Accepted — dataset tooling implemented 2026-07-23` and fill Open items:

- Quarry path: `Data\Memory\HouseVictoria.db` (Victoria `conv-977d778f-…`)
- Keep tooling: `keep_ids.txt`
- Gitignore: dataset-local JSONL ignore (not entire `datasets/`)

- [ ] **Step 4: Commit**

```powershell
git add docs/superpowers/specs/2026-07-23-victoria-style-lora-dataset-design.md
git commit -m "docs: mark Victoria style LoRA dataset recipe accepted"
```

---

## Self-review (plan vs spec)

| Spec requirement | Task |
| --- | --- |
| Export candidates from quarry chats | Task 2 |
| Human Keep / Drop only | Task 4 + KEEP.md Task 1 |
| JSONL messages schema | Task 2 produces schema |
| keep_ids → train/eval | Task 3 |
| No charter in rows | Task 2 (no system role) |
| Victoria only / drop other personas | Task 2 conversation filter |
| Size targets documented not enforced | Task 3 print bands; Task 4 human quota |
| Eval holdout | Task 3 |
| No LoRA train | Global out of scope |
| Private JSONL not committed | Task 1 `.gitignore` + Task 5 verify |

No TBD placeholders remain for paths/tooling — locked above.
