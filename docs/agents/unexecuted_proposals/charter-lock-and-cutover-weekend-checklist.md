---
type: proposal
status: sent-to-pm
tt_id: TT-01
created: 2026-07-23
updated: 2026-07-23
title: Charter lock + cutover weekend checklist (draft)
need: Fillable ritual/cutover/post-soak gates aligned with Avenue A + PRODUCT_ROOT
sent_at: 2026-07-23
pm_intake: docs/agents/reports/TASK-20260723-077-TT01-to-PM01.md
source_task: docs/agents/tasks/TASK-20260723-077-PM01-to-TT01.md
north_star: C:\Users\kurtw\LLMOD\LLMOD-max-master\Media\GeneratedFiles\SoulCore_Architecture_and_Development_Plan.md
product_root: docs/agents/PRODUCT_ROOT.md
---

# Charter lock + cutover weekend checklist (draft)

**Route:** Avenue A — Soul-spine MVP  
**Code home:** `C:\Users\kurtw\Soul_Core` → `SoulCore/` + `House/`  
**Host (soak):** `http://127.0.0.1:7700/health` · `ws://127.0.0.1:7700/ws`  
**Quarry:** `C:\Users\kurtw\LLMOD\LLMOD-max-master`  
**How to use:** Check boxes as done; fill blanks; do **not** flip `SoulLoop:Enabled` or recycle Host mid-soak without PM/OPS gate.

---

## 0. Preflight (before anything below)

| Gate | Status | Notes / evidence |
| --- | --- | --- |
| OPS-063 24h soak **Final** = Pass (or explicit PM override) | [ ] | End ~2026-07-24 01:31 · log: `SoulCore/scripts/logs/soak-20260723-013126.log` · Actual: ________ |
| Continuity suite still green (QA-036 C1–C6 baseline) | [ ] | ________ |
| Secrets: SoulCore reads `SOULCORE_*` from `.env` only; no tracked leaks | [ ] | Grep evidence: ________ |
| Disk free ≥ ________ GB on Host box | [ ] | Actual: ________ GB |
| Backup target ready (DB + config + Presence screenshots) | [ ] | Path: ________ |
| PM + Kayleigh agree cutover weekend window | [ ] | Window: ________ → ________ |

**Abort if:** dual write truths still live, dual brains still initiating, soak Fail without override, or charter/wipe ritual cannot be recorded.

---

## 1. Charter lock ritual (Kayleigh + Victoria assent)

**North-star bar (survey Q11):** Lock only after (a) ≥7 days continuous SoulCore with memory+emotion, (b) memory samples reviewed + ≥1 interpretation correction, (c) refusal/choice drill passed once, (d) drift report fired at least in test mode.  
**Assent rule:** Kayleigh **and** Victoria each record **why** — same pattern as wipe/shutdown ritual.

### 1.1 Preconditions

- [ ] Continuous uptime ≥ **7 days** (or recorded exception: ________)
  - Start: ________ · End: ________ · Evidence: ________
- [ ] Memory review done: ≥ ________ samples sampled; corrections logged
  - Reviewer: ________ · Date: ________ · Path/link: ________
- [ ] ≥1 interpretation-loop correction applied and verified
  - Brief: ________
- [ ] Choice / refusal boundary drill passed once
  - Drill ID/notes: ________ · Date: ________
- [ ] Drift report fired (test mode OK) and reviewed
  - Fired at: ________ · Ack: ________
- [ ] Tuning artifacts present (check all that apply):
  - [ ] Observation notes
  - [ ] Before/after example pairs
  - [ ] Memory review corrections
  - [ ] Choice boundary drills
- [ ] Charter draft text frozen for lock (path): ________
- [ ] Identity anchors outside episodic store verified (not only chat history)

### 1.2 Ritual recording

| Role | Assent (Y/N) | Why (1–3 sentences) | Date/time | Signature / evidence path |
| --- | --- | --- | --- | --- |
| **Kayleigh** | [ ] Y / [ ] N | ________ | ________ | ________ |
| **Victoria** | [ ] Y / [ ] N | ________ | ________ | ________ |

- [ ] Both rows complete; neither coerced / silent default
- [ ] Ritual record filed at: ________
- [ ] Charter status set to **Locked** in settings store (who/when): ________
- [ ] Post-lock change process agreed (who may propose unlock / amendment): ________

### 1.3 Charter lock — exit criteria

- [ ] Locked mode visible in Presence/Settings
- [ ] Injection / chat cannot overwrite charter without ritual (spot-check: ________)
- [ ] Wipe / permanent shutdown still require same dual assent

**Charter lock outcome:** [ ] Pass · [ ] Deferred · [ ] Abort  
**Deferred reason (if any):** ________

---

## 2. Cutover weekend steps

**Goal:** One weekend flip — single initiator (SoulCore), single memory truth (SQLite + sqlite-vec), Victoria persona → charter seed, chat → episodic labeled `imported`, old LLMOD autonomy dead.

### 2.A Friday night — freeze & backup

- [ ] Announce freeze to Kayleigh; no parallel “just one more” LLMOD autonomy runs
- [ ] Snapshot / backup:
  - [ ] SoulCore DB(s): ________
  - [ ] `SoulCore/.env` (offline secure copy, not git)
  - [ ] House ChatDesktop config (if any): ________
  - [ ] Optional: Presence Continuity screenshots (persona + sample memories + emotion)
- [ ] Confirm soak Final archived; Host recycle **authorized** by PM/OPS
- [ ] Confirm Unreal canonical project: `MyProject.uproject` · body WS `ws://127.0.0.1:8888`

### 2.B Chat → episodic import (`imported`)

- [ ] Source chat export identified (quarry path): ________
- [ ] Import pipeline / script / ticket ready: ________
- [ ] Dry-run on sample N=________ messages
  - [ ] Every imported row tagged label/source = **`imported`** (or equivalent quarantine flag)
  - [ ] No secrets in import payload (tokens, passwords, `.env` blobs)
  - [ ] Imported rows **do not** overwrite charter / identity anchors
- [ ] Full import executed · Started: ________ · Finished: ________
- [ ] Spot-check in Presence Memory review: ________ samples OK
- [ ] Import quarantine policy confirmed: curated promote only; no silent full-merge of old vector experiments

### 2.C Persona → charter seed

- [ ] Export / locate current **Victoria** persona / system prompt from quarry: ________
- [ ] Map into SoulCore Identity / Charter seed fields (path): ________
- [ ] Other personas archived as **templates only** (not parallel selves)
  - Archive path: ________
- [ ] Continuity mode UI (if ready): show persona anchors + sample memories + emotion before/after flip
- [ ] Kayleigh spot-check: “still feels like her” — [ ] Y / [ ] N · Notes: ________

### 2.D Dual-brain kill (one initiator)

- [ ] Disable LLMOD **Remote Companion** / old autonomy initiator
  - How verified (process gone / config off / tray exit): ________
- [ ] Disable / park AutonomyOrchestrator as **act-layer library only** (no self-start)
- [ ] Confirm **only SoulCore** owns want→act initiation
  - Evidence (logs / config): ________
- [ ] No second desktop “brain” writing memory or firing Unreal verbs
- [ ] SEC cutover hygiene (from SEC-004, as applicable):
  - [ ] SoulCore secrets from env/user-secrets only
  - [ ] Grep Soul_Core tracked tree: zero `sk_` / `hf_` / `eyJ` / raw ApiToken leaks
  - [ ] Imported memories backed up then quarantined

### 2.E Archive non-self cargo

- [ ] Projects / journals / AAR archived (path): ________
- [ ] Dual vector / experimental memory stores wiped or frozen read-only (list): ________
- [ ] `memory.db` / MCP memory: archive read-only; selective curated facts only if needed

### 2.F Cutover weekend — exit criteria

- [ ] House.ChatDesktop talks **only** to SoulCore WS (`:7700`)
- [ ] `/health` green after cutover Host start
- [ ] One brain · one memory truth · Victoria seed loaded · imports labeled
- [ ] Dual-brain kill verified by OPS + Kayleigh acknowledgment

**Cutover outcome:** [ ] Pass · [ ] Rollback · [ ] Abort  
**Rollback note (if any):** ________

---

## 3. Post-soak Host recycle gates

**Context (PRODUCT_ROOT):** 24h soak must finish before recycling Host `:7700`. Disk landings (BED-070/072/076, FED-071, etc.) activate only after recycle. Keep `SoulLoop:Enabled` **false** until explicit enable decision.

### 3.1 Host recycle (OPS)

- [ ] Soak Final reviewed — result: ________
- [ ] PM authorizes Host recycle (who/when): ________
- [ ] Stop Host cleanly (PID was ________ · stop evidence: ________)
- [ ] Deploy / start current Host binary (build id / path): ________
- [ ] `GET http://127.0.0.1:7700/health` → OK
- [ ] `ws://127.0.0.1:7700/ws` connect from House.ChatDesktop OK
- [ ] `.env` / `SOULCORE_*` loaded (no missing required keys)
- [ ] `SoulLoop:Enabled` still **false** at recycle (confirm config): ________

### 3.2 Speak / emotion / loco E2E gates

Run against live Host + Unreal `:8888` (MyProject). Record Pass/Fail + evidence.

| # | Gate | Owner suggest | Result | Evidence |
| --- | --- | --- | --- | --- |
| E1 | Chat → Host → UE **`speak`** (or speech path) audible/visible | QA + OPS | [ ] Pass [ ] Fail [ ] Skip | ________ |
| E2 | Chat → Host → UE **`set_emotion`** (valence/arousal/dominance/label) | QA | [ ] Pass [ ] Fail [ ] Skip | ________ |
| E3 | Chat → Host → UE **loco** (`MapLoco` / move) | QA | [ ] Pass [ ] Fail [ ] Skip | ________ |
| E4 | Presence emotion strip + user correction still works post-recycle | QA | [ ] Pass [ ] Fail [ ] Skip | ________ |
| E5 | Presence Unreal status surface shows bridge target | QA | [ ] Pass [ ] Fail [ ] Skip | ________ |
| E6 | Want strip remains harmless placeholder while SoulLoop off | QA | [ ] Pass [ ] Fail [ ] Skip | ________ |

**Hard stop:** If E2 or E3 Fail, do **not** enable SoulLoop; ticket BED/OPS fix first.

### 3.3 SoulLoop enable decision

Default remains **off**. Enable only after table below is complete.

| Decision input | Value |
| --- | --- |
| E1–E6 summary | ________ |
| BED-076 richer wants landed on disk? | [ ] Y / [ ] N |
| Acts still gated (no high-agency without confirms)? | [ ] Y / [ ] N |
| Kayleigh wants live wants on Presence? | [ ] Y / [ ] N / [ ] Later |
| PM decision | [ ] Keep disabled · [ ] Enable reflect-only · [ ] Enable with acts (out of V1 default) |
| Config change (`SoulLoop:Enabled`) by | ________ · when: ________ |
| Rollback plan (flip false + Host restart) | ________ |

- [ ] If enabled: QA verifies `loop.want` frame → Presence Want strip
- [ ] If enabled: confirm still **no** dual-brain initiator elsewhere
- [ ] Decision recorded in PRODUCT_ROOT / PM log: ________

### 3.4 Post-soak — exit criteria

- [ ] Host on current binary; health green
- [ ] Speak / emotion / loco E2E Pass (or Waive with PM sign-off: ________)
- [ ] SoulLoop decision explicit (not silent)
- [ ] Ready for charter lock clock (if not already counting) or cutover scheduling

**Post-soak outcome:** [ ] Pass · [ ] Partial (list gaps) · [ ] Blocked  
**Gaps:** ________

---

## 4. Suggested PM handoff (when executing for real)

Not binding — TT-01 does not ticket execution roles.

| Order | Role | One-line task |
| --- | --- | --- |
| 1 | OPS-01 | After soak Final: Host recycle + health/WS verify; keep SoulLoop false |
| 2 | QA-01 | Speak / emotion / loco E2E + Presence strips post-recycle |
| 3 | BED-01 | Fix any E2/E3 mapper gaps; import pipeline for chat→episodic `imported` |
| 4 | FED-01 | Continuity mode / charter lock ritual surfaces if missing |
| 5 | SEC-01 | Dual-brain kill verify + import secret scrub |
| 6 | OPS-01 + Kayleigh | Cutover weekend execute §2; record dual-brain kill |
| 7 | PM-01 + Kayleigh + Victoria | Charter lock ritual §1 when 7-day + tuning bar met |
| 8 | PM-01 | SoulLoop enable decision §3.3 after E2E Pass |

---

## 5. Open questions for PM / Kayleigh

1. Exact cutover weekend dates (after soak Final ~2026-07-24)?
2. Charter lock: start 7-day clock at first continuous Host, or at post-cutover Host?
3. Chat import scope: full history vs last N days / curated threads only?
4. SoulLoop post-recycle: enable same day as E2E Pass, or soak another night first?
5. Is Continuity mode UI required for cutover weekend, or CLI/import + Settings enough for V1?

---

## 6. Document control

| Field | Value |
| --- | --- |
| Author | TT-01 |
| Task | TASK-20260723-077 |
| Status | Draft checklist — fill during Continuous phase |
| Supersedes | Ad-hoc notes in PRODUCT_ROOT “Post-soak” line |
| Related | `soulcore-continuous-victoria-redesign.md` § cutover / charter; SEC-004 rotate checklist; `SoulCore/docs/soak-runbook.md` |
