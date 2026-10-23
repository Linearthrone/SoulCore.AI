---
type: task
prop_id: PROP-16.8b
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: OPS-01
priority: P0
status: Pass
report: docs/agents/log/PROP-16.8b-OPS01-to-PM01.md
pm_note: Accepted 2026-10-23. Parsed, not run. Stop targets are 7720, 7721, and 17920. Soul_Core on 7700 and the shared bridge and voice ports were still listening after the edit.
created: 2026-10-23
updated: 2026-10-23
depends_on: PROP-16.8 (Pass)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: ALLSTOP targets the Creator Edition ports only
---

# PROP-16.8b — ALLSTOP

## Problem

`ALLSTOP.ps1` still stops Host ports 7700 and 7701, the browser bridge on 17891, and voice STT/TTS on 8000 and 8881. Those processes belong to the other checkout and the shared voice servers. Running stop from this tree would take them down. Tailscale `-Off` in `tailscale-serve-soulcore.ps1` already removes only 7720 and 8453.

## Solution

1. Default stop ports are **7720** and **7721**. Do not include 7700 or 7701.
2. Stop the bridge on **17920** only. Do not stop 17891.
3. Do not stop STT 8000 or TTS 8881. This tree does not own those processes.
4. Keep the Tailscale teardown as `-Off` on the script that now targets 7720 and 8453 only.

## Do not

- Run `ALLSTOP.ps1`.
- Stop any live process, including the Host already on 7720 (pid 36096) and the Soul_Core Host on 7700.
- Commit or push.

## Acceptance

- [ ] With no arguments, the script’s host ports are 7720 and 7721
- [ ] The bridge stop is 17920, and 17891 is not named as a stop target
- [ ] 8000 and 8881 are not stop targets
- [ ] The script was not executed
- [ ] Report lists the parser output

## Report

`docs/agents/reports/PROP-16.8b-OPS01-to-PM01.md`
