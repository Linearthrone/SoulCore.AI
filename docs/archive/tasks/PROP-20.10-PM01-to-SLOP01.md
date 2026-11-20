---
type: task
prop_id: PROP-20.10
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: SLOP-01
priority: P0
status: Pending
created: 2026-11-17
title: Post-QA slop audit — PROP-20 Wave A
related_qa: docs/agents/reports/PROP-20.9-QA01-to-PM01.md
---

# PROP-20.10 — SLOP Wave A

**Activate:** `@Agents/SLOP-01.md`

## Scope paths

- `SoulCore/SoulCore.Core/` (VictoriaMindBlock)
- `SoulCore/SoulCore.Host/Ws/` (ChatContextBuilder, MemoryTape)
- `SoulCore/SoulCore.Host/Companion/` (SMS inbound/outbound composer)
- `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs`
- `SoulCore/SoulCore.Inference/Presence/` (SmsToolGate, LeisureMindBlock, LeisureTools)
- `SoulCore/SoulCore.Memory/` (dated recall, journal 012)
- `House/House.ChatDesktop/` (Identity client, Her journal window)

## Do not

Modify code. Report only: clean | findings with evidence + remove|dedupe|ask-user.

## Report

`docs/agents/reports/PROP-20.10-SLOP01-to-PM01.md`
