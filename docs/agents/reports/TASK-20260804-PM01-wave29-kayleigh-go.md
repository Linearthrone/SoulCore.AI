---
type: report
from: PM-01
to: user
created: 2026-08-04
wave: 29
title: Wave 29 Kayleigh player pawn — GO + tickets
---

# Wave 29 — Kayleigh grounded player (GO)

## Decision

User asked TINA to set up **MHC_Kayleigh** with eye-level camera, walking + anims + collision, env hearing, and proximity speak. This **clears** the open PRODUCT_ROOT gate:

> Player embodiment: grounded Kayleigh body vs free-fly `ADefaultPawn` → **grounded**.

## Constraint

Canonical Unreal project is on the **shadow PC** (body only). This cloud agent cannot open the editor or edit `.uasset` files. Execution is **BED-172** on that machine, using the runbook below.

## Issued

| ID | Role | Work |
| --- | --- | --- |
| **BED-172** | BED-01 | Build `BP_KayleighCharacter` + GameMode + loco + audio + prox chat |
| **QA-173** | QA-01 | PIE verify after 172 Pass |

**Runbook:** `docs/runbooks/kayleigh-player-pawn-setup.md`

## Pattern

Same as Victoria BED-114: **do not reparent MHC** — Character host BP + mesh from `MHC_Kayleigh`. Tag `KayleighPlayer`. Leave `VictoriaAvatar` alone for the bridge.
