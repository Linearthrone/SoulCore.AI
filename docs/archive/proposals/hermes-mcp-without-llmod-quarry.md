---
type: proposal
status: sent-to-pm
tt_id: TT-01
created: 2026-07-29
updated: 2026-07-29
title: Hermes MCP without LLMOD quarry (Linux cloud unblock)
need: Restore usable MCP/tool path for Phase C/D/F without Kurt's Windows LLMOD tree on cloud agents
sent_at: 2026-07-29
pm_intake: docs/agents/tasks/TASK-20260729-159-TT01-to-PM01.md
stuck_tickets:
  - docs/agents/tasks/TASK-20260726-143-PM01-to-OPS01.md
related_issue: ISSUE-20260729-003 (referenced by OPS-143; file missing from workspace at TT eval)
---

# Hermes MCP without LLMOD quarry

## 1. Need / Want

OPS-143 brought `hermes-agent` up on Linux cloud (`:8642` health 200) but could
not register LLMOD MCP servers (`house_victoria`, `house_victoria_data`,
`computer_use`) because the quarry
`C:\Users\kurtw\LLMOD\LLMOD-max-master` is absent. Phase C/D/F stay gated.
Need an actionable route that does **not** require every cloud agent to mount
Kurt's Windows box, while honoring the user decision of **both backends**
(native C# + Hermes) and SEC-004 (loopback-only Host bind).

## 2. Goal & Success Criteria

| # | Criterion |
| --- | --- |
| G1 | Phase C/D desktop/browser/MT4 tools can land on cloud **without** LLMOD quarry |
| G2 | Hermes `:8642` remains a first-class future path (not abandoned) |
| G3 | No Host bind off loopback; no secretful quarry dumps into git |
| G4 | Clear PM ticket split: what ships now vs what waits for Windows/quarry |
| G5 | Conscious scope language — sequencing, not silent drop of "both backends" |

## 3. Context & Constraints

### Facts (OPS-143)

- Gateway process OK: `hermes-agent` 0.18.2, `/health` 200, 5-min smoke pass.
- `hermes mcp list` → **No MCP servers configured**.
- Built-in Hermes `computer_use` toolset ≠ LLMOD MCP `computer_use`.
- OpenAI `tools` / `tool_choice` accepted, but responses were final `content`
  without client-visible `tool_calls[]` (SoulCore BED-127/144 expects that
  shape) — **second defect**, independent of missing MCP packages.
- `Hermes.Enabled` left `false` (correct).
- Runbook/scripts added under `SoulCore/scripts/` + Hermes runbook (OPS).
- `~/.hermes/config.yaml` documents commented MCP stubs awaiting quarry sync.
- ISSUE-20260729-003 path cited by OPS but **not present** under
  `docs/agents/issues/` at TT-159 eval time — recreate or attach when ticketing.

### Product decisions already locked (PRODUCT_ROOT)

- Backend: **both** (native C# Ollama tool-loop + Hermes gateway).
- Scope: all waves A–F ticketed (125–145).
- Phase C tickets (135/136/138) currently prefer Hermes MCP and treat native
  as optional — that preference is what deadlocks cloud.

### Constraints from TASK-159

- Must not bind Host off loopback (SEC-004).
- Prefer not requiring Kurt's Windows box for CI cloud agents.
- Keep native C# tool backends as fallback where already planned (BED-135+).
- TT-01 must not implement product code.

## 4. Clarifying Q&A (answered)

| Q | Answer / source |
| --- | --- |
| Is quarry on this VM? | No — OPS-143 workspace search + PRODUCT_ROOT Windows path |
| Can we use Hermes built-ins as HV MCP? | No — OPS explicitly: different surface; AC requires MCP servers |
| User chose both backends? | Yes — PRODUCT_ROOT open gate #10 cleared 2026-07-26 |
| Is native path already designed? | Yes — BED-135/136/138 dual `*Backend: hermes \| native` |
| Does Hermes return `tool_calls` today? | No evidence of client-visible `tool_calls` on OPS smoke |

### Open (do not block recommendation)

| Q | Why it can wait |
| --- | --- |
| Will Kurt sync a redacted `MCPServer/` tarball this week? | Avenue A is parallel track; native-first unblocks either way |
| Windows-only MT4 / SendInput on cloud? | Native desktop/MT4 may still need Windows runtime for full E2E; Linux can still land schemas + gates + mocks |

## 5. Thinktank seats (this session)

Parallel perspective pass (facilitator-run; no nested Task agents). Seats:
**STRAT**, **CONTRA**, **SYS** (light **RISK** folded into CONTRA/SYS).

### STRAT

- Goal fit: agency for Phase C/D matters more than MCP process purity on day 1.
- Sequence: (1) **native-first** reticket Phase C/D → (2) quarry/MCP artifact
  track for Phase F Hermes → (3) BED-144 Hermes wiring after MCP **or** after
  Hermes `tool_calls` mode is proven with a minimal MCP.
- Kill "wait for full LLMOD tree on Linux" as the critical path.

### CONTRA

- Full quarry sync into cloud git is wrong: secret history (`MCPServer/.env`),
  Windows-only APIs, false greens (`hermes mcp list` with broken stdio).
- Stub MCP that only echoes names is wrong for QA-145 — invents Pass.
- Rewriting all ~40 HV tools as Linux MCP *before* native C# duplicates BED-135+
  and delays both backends.
- Leaving BED-135 depends_on OPS-143 MCP AC is the deadlock — must break.

### SYS

- Feasibility: SoulCore already owns Ollama tool-loop + `ITool` registry;
  desktop/browser/MT4 as C# tools is the low-integration-cost path on this repo.
- Hermes path cost: need (a) portable MCP packages, (b) `mcp_servers` in
  `~/.hermes/config.yaml`, (c) gateway mode that returns OpenAI `tool_calls` to
  SoulCore (OPS gap — may need Hermes config / API-server mode, not just MCP).
- Integration shape: keep `DesktopBackend`/`BrowserBackend`/`Mt4Backend`
  switches; default cloud/CI → `native`; Windows desk with quarry → `hermes`.
- Rough cost: native-first = reticket + BED work already scoped; quarry sync =
  Kurt one-shot + OPS register; Hermes tool_calls fix = small OPS/BED spike.

## 6. Avenues Explored

### Avenue A — Redacted quarry sync / portable MCP artifact

**Idea:** Kurt (or OPS on Windows) exports `MCPServer/` (+ hermes MCP config,
secrets redacted) as a portable artifact: private tarball, submodule, or
`third_party/hv-mcp/` with Linux-runnable subsets. OPS re-runs `hermes mcp add`
and re-proves tool list + `tool_calls`.

| Pros | Cons |
| --- | --- |
| Preserves literal Hermes+MCP path | Requires human Windows action; not cloud-self-serve |
| Reuses battle-tested HV tool surface | Many tools are Win32/MT4 — may not run on Linux even after sync |
| Unblocks BED-144/QA-145 on a machine that can run MCP | Secret scrub + license/path hygiene mandatory |

**Fit:** Parallel Phase F track. Not the cloud critical path for C/D.

### Avenue B — Reimplement HV MCP servers in-repo (Linux stdio)

**Idea:** Port FastMCP `house_victoria*` packages into SoulCore (or
`SoulCore/mcp/`) with schemas from PROP-AGENT-LOOP-01; register with Hermes.

| Pros | Cons |
| --- | --- |
| Reproducible on Linux CI | Large rewrite; overlaps native C# tools |
| Decouples from Kurt's disk path | Desktop/MT4 still need OS backends |
| Clean git history | Delays Phase C while reinventing MCP transport |

**Fit:** Only if product insists Hermes MCP is the *sole* execution path.
Conflicts with "prefer native fallback" and doubles work.

### Avenue C — Conscious scope cut: native-first Phase C/D; Hermes MCP deferred

**Idea:** Supersede OPS-143 MCP acceptance for cloud. Reticket BED-135/136/138
so **native C# backends are required**; Hermes MCP routing becomes
best-effort/`hermes` backend when quarry+MCP present. Keep gateway process up
(`Hermes.Enabled=false` until Phase F). BED-144/QA-145 become Windows/quarry
gated or split into "client wiring" vs "MCP E2E".

| Pros | Cons |
| --- | --- |
| Unblocks cloud agents immediately | Temporary imbalance vs "both backends" |
| Matches dual-backend design already in tickets | Full Hermes E2E slips until Avenue A |
| Avoids secretful quarry dump | Windows-native tools still need a Windows desk for visual MT4/desktop E2E |

**Fit:** **Primary recommendation** for unblocking.

### Avenue D — Minimal "wiring MCP" + Hermes tool_calls spike (parked hybrid)

**Idea:** Ship a tiny in-repo MCP with 1–2 harmless tools (`echo`,
`system_info`) to prove Hermes→MCP→`tool_calls` before full HV inventory;
separately fix client-visible `tool_calls` mode.

| Pros | Cons |
| --- | --- |
| Isolates OPS "no tool_calls" defect | Does not deliver desktop/browser/MT4 |
| Cheap proof for BED-144 smoke | Risk of mistaking stub for Phase F done |

**Fit:** Optional companion spike under OPS follow-up; not a substitute for C/D.

## 7. Recommended Route

**Primary: Avenue C (native-first Phase C/D), with Avenue A as parallel Phase F
track, and a small Avenue D spike only if PM wants Hermes wiring proven early.**

### Sequencing

```text
Now (cloud)
  PM supersedes OPS-143 MCP ACs for Linux cloud
  Reticket BED-135 / 136 / 138 → native required; hermes optional
  Keep Hermes gateway runbooks; Hermes.Enabled=false
  Phase C/D proceed on ITool + native backends + existing security gates

Parallel (Kurt / Windows OPS)
  Avenue A: redacted MCPServer export + config template
  OPS follow-up: hermes mcp add ×3; tool-list evidence; tool_calls round-trip
  Then BED-144 hermes routing + QA-145 E2E on a machine with MCP live

Optional spike
  Avenue D: echo MCP + fix client-visible tool_calls (unblocks BED-144 partial)
```

### Why this honors "both backends"

Both backends remain product truth:

- **Native** becomes the cloud/CI and interim production path.
- **Hermes+MCP** remains the restored LLMOD path once quarry artifacts exist —
  not cancelled, not silently replaced by Hermes built-in toolsets.

### What PM should decide first

1. Accept native-first sequencing for Phase C/D (recommended: **yes**).
2. Whether Avenue A is Kurt-manual this week or "when available" backlog.
3. Whether to fund Avenue D spike before full quarry sync.

## 8. Alternatives (parked)

| Alt | Status |
| --- | --- |
| Avenue B full MCP reimplement first | Parked — cost duplicate; revisit only if native impossible for a tool family |
| Wait indefinitely for full LLMOD tree on Linux VM | **Rejected** — kills cloud momentum |
| Treat Hermes built-in computer_use as OPS-143 Pass | **Rejected** — false Pass |
| Enable `Hermes.Enabled=true` now | **Rejected** — no MCP; tool_calls unproven |

## 9. Risks & Kill Criteria

| Risk | Mitigation | Kill / escalate |
| --- | --- | --- |
| "Both backends" feels abandoned | Document sequencing in PRODUCT_ROOT + ticket text | User rejects native-first → revisit Avenue A urgency |
| Native desktop/MT4 incomplete on Linux | Schemas + gates + mocks on Linux; full E2E on Windows desk | If native Win32 path also missing and no quarry → Phase C blocked again → TT reopen |
| Quarry sync brings secrets | SEC scrub; `.env.example` only; never commit real `.env` | Any secret in PR → SEC stop-ship |
| Hermes never returns `tool_calls` to SoulCore | Avenue D / OPS spike on API mode | If unfixable → Hermes path becomes agent-side tools only; SoulCore stays Ollama-native |
| Double maintenance (C# tools + MCP) | Prefer C# as source of truth; MCP wraps or stays LLMOD legacy | If both diverge — pick one execution owner per tool family |

## 10. Open Questions for User / PM

1. Confirm native-first Phase C/D while Hermes MCP waits on quarry sync? *(recommend yes)*
2. Kurt: can a redacted `MCPServer/` (+ config) drop happen within ~7 days, or backlog?
3. Fund a 1–2 day Hermes `tool_calls` + echo-MCP spike (Avenue D) before full HV MCP?

## 11. Suggested PM Handoff

Stuck to supersede / annotate:

- `TASK-20260726-143-PM01-to-OPS01` — accept **Partial**: gateway Pass; MCP ACs
  moved to follow-up; do not leave Phase C depends_on full MCP forever.

Suggested next tickets (non-binding on PM):

| Suggested | Role | One-line |
| --- | --- | --- |
| OPS-160 | OPS-01 | Recreate/file ISSUE-20260729-003; annotate OPS-143 Partial; keep Hermes scripts green; document MCP-absent Linux state in runbook |
| BED-161 | BED-01 | Reticket/amend BED-135: **native desktop backend required**; Hermes path optional when MCP present; drop hard depends_on OPS-143 MCP AC |
| BED-162 | BED-01 | Same for BED-136 browser — native/fallback bridge required for cloud |
| BED-163 | BED-01 | Same for BED-138 MT4 — native bridge or explicit Windows-only deferral with mock + gate tests on Linux |
| OPS-164 | OPS-01 | (Avenue A) Intake redacted MCPServer artifact from Kurt; `hermes mcp add`; tool-list + smoke evidence |
| OPS-165 | OPS-01 | (Avenue D, optional) Minimal echo MCP + prove client-visible `tool_calls` on `:8642` |
| BED-144 | BED-01 | Split or gate: client wiring vs MCP E2E; do not start full hermes routing until OPS-164 or OPS-165 Pass |
| QA-145 | QA-01 | Remains Hermes MCP E2E gate — stays gated on MCP restore |
| SEC-166 | SEC-01 | Review any MCPServer artifact / config template before it lands in repo |

**Order:** OPS-160 → BED-161/162/163 (parallel) → Phase C QA gates → (parallel)
OPS-164/165 → BED-144 → QA-145.

**PM should decide first:** accept Avenue C sequencing (yes/no).
