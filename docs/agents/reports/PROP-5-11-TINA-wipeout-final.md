---
type: pm-final
from: PM-01 (TINA)
created: 2026-09-05
title: Architecture-eval wipeout — final scoreboard
---

# TINA wipeout final — PROP-5..11 (+ product seats)

## Wipeout core (architecture eval)

| PROP | Verdict | Branch |
| --- | --- | --- |
| PROP-5 SQLite gate + SoulLoop + charter | **Pass** | `cursor/prop5-sqlite-gate-8a1f` |
| PROP-6 Desktop async delay | **Pass** | `cursor/prop6-desktop-delay-8a1f` |
| PROP-7 Hermes dead surface | **Pass** | `cursor/prop7-hermes-cleanup-8a1f` |
| PROP-11 Memory repos | **Pass** | `cursor/prop11-memory-repos-8a1f` |
| PROP-9 Host DI modules | **Pass** | `cursor/prop9-di-modules-8a1f` |
| PROP-10 Inference Clients/Tooling/Tools | **Pass** | `cursor/prop10-inference-split-8a1f` |
| PROP-8 Chat strangler | **Pass** | `cursor/prop8-chat-strangler-8a1f` |

## Product seats advanced

| PROP | Verdict | Notes |
| --- | --- | --- |
| PROP-1.4 SMS SEC | **Partial** | Code+tests Pass; live tablet SMS = PROP-1.5 |
| PROP-4.1 Presence drawer | **Partial** | Drawer+honesty+tests; Windows visual QA pending |
| PROP-2 UE | Open | Needs shadow PIE (REX) |
| PROP-1.5 / 1.6 | Open | After tablet QA / Link shrink |

## Integrate tip

`cursor/tina-wipeout-integrate-8a1f` = PROP-8 stack + PROP-6 merged. PROP-10/11/4/1.4 may need manual stack merge on Windows (add/add conflicts on Hosting modules) — prefer merge order: 5→7→11→9→10→8→6 then product branches.

## Human gates left

1. Open/merge PRs (cloud token cannot create PRs)
2. Windows Presence visual QA (4.1)
3. Tablet SMS round-trip (1.5)
4. Shadow UE possess (2.1)

TT kill criteria not violated: one Host lane at a time during execution; no IMAP/vector/docs-merge minting.
