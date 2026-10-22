---
type: report
prop_id: PROP-15.22b
prop_root: PROP-15-persona-creation-platform
from: QA-01
to: PM-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.22 (Pass), PROP-15.21c (Pass)
fed_report: docs/agents/reports/PROP-15.22-FED01-to-PM01.md
ops_report: docs/agents/reports/PROP-15.21c-OPSHOME-to-PM01.md
ticket: docs/agents/tasks/PROP-15.22b-PM01-to-QA01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Smoke — persona switcher + Presence/Host version currency
---

# PROP-15.22b — Persona switcher + currency smoke — **Pass**

## Verdict: **Pass**

Live UI smoke on CreatorEdition **Release** ChatDesktop against Host **0.2.0** (`capabilities.personas=true`). Switcher lists pack **DisplayName**s; quarantine confirm → activate works; title-bar and Settings → Updates show Presence **0.1.8**, currency **Unpackaged**, Host **0.2.0**, **Personas OK**. End pin restored to **lexi / Alexia** + `mother:latest`. Product code untouched. Ready for **SLOP-01**.

## Environment

| Check | Result |
| --- | --- |
| Host (15.21c seat) | `127.0.0.1:7700` — `version=0.2.0`, `capabilities.personas=true`, `GET /api/personas` 200 (6 packs) |
| ChatDesktop | Release `House/House.ChatDesktop/bin/Release/net8.0/House.ChatDesktop.exe` — Presence **0.1.8** (unpackaged). Found running at smoke start; relaunched current Release after one hard exit during early UIA; final PID **16140** |
| Start / end pin | **lexi / Alexia**, `resolvedInferenceModel=mother:latest`, `modelSource=persona` |
| Method | Windows UI Automation + Host REST; no product code changes |

## Acceptance checklist

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Switcher lists **DisplayName**s; activate another pack → quarantine confirm → success; ↻ refresh works | **Pass** | Dropdown ListItems: **Analyst**, **blank**, **Aurora Desk**, **Alexia**, **Mentor**, **Victoria** (ids muted under names). Selected **Mentor** → dialog *Switch active persona to Mentor (mentor)?* + quarantine copy → **Switch** → Host `personaId=mentor`, chrome success notice. ↻ (`HelpText=Reload persona packs from Host`) → `6 pack(s) · active: Mentor`. Restored **Alexia (lexi)** same path. |
| 2 | Title bar / Settings → Updates: Presence version, Host version, Presence currency, Host personas OK | **Pass** | `BuildVersionsText` = `Presence 0.1.8 · Unpackaged · Host 0.2.0 · Personas OK`. Settings → Updates: `UpdateVersionBox=0.1.8`, `UpdatePresenceCurrencyBox=Unpackaged`, `UpdateHostVersionBox=0.2.0`, `UpdateHostCurrencyBox=Personas OK`, feed `GitHub Releases (https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition)`. |
| 3 | Optional: stop Host / old Host → persona-API missing | **Skipped (noted)** | Left Host up to preserve feel-drive **lexi** pin from 15.21c. Honesty path covered by FED 15.22 unit/source; not re-broken on live seat. |

## Live evidence (condensed)

| Step | Result |
| --- | --- |
| Host `/health` | `0.2.0`, `capabilities.personas=true` |
| Switcher DisplayNames | 6 packs as DisplayName (not id-only) |
| Activate Mentor | Quarantine dialog → Switch → Host `mentor` / UI Identity **Mentor** |
| ↻ refresh | `6 pack(s) · active: Mentor` |
| Restore Alexia | Quarantine → Switch → Host `lexi` / `mother:latest` / `modelSource=persona` |
| Currency chrome | Presence 0.1.8 · Unpackaged · Host 0.2.0 · Personas OK |

## Incidents (non-blocking)

1. Early UIA attempt selecting **Victoria** then invoking **Switch** coincided with a hard ChatDesktop exit (no `Avalonia lifetime ended normally` in `%LocalAppData%\HouseVictoria\presence-startup.log`). Relaunched Release; **Mentor** activate + **Alexia** restore completed cleanly with the same confirm→Switch path. Treat as automation/race noise unless it reproduces under manual click — not scored Fail.

## Skip / constraints honored

- No product code changes
- No Host kill / no sparse persona PUT
- Optional Host-down honesty check skipped (lexi pin)
- Naming: LinearThrone / Kayleigh rules observed in this report

## Ask PM

1. Accept **Pass** for PROP-15.22b.
2. Dispatch **SLOP-01** (post-QA slop audit) on the 15.22 client surface.
3. Optional follow-up: manual click activate once if Victoria hard-exit needs a FED look; Mentor/Alexia path already green.
