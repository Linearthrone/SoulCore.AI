---
type: task
prop_id: PROP-20.2
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Done
created: 2026-11-17
title: Dated recall — occurred_at + today-first memory tape
depends_on: none
siblings: PROP-20.1, PROP-20.3, PROP-20.4
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.2 — Time / dated recall

**Activate:** `@Agents/BED-01.md` then execute this ticket.

## Problem

Memory lines fed into chat are relevance piles without reliable day ordering. Law (closed): tape is **today first**, then relevant older lines. Recall must carry `occurred_at` with content.

## Solution

1. Ensure episodic/chat memory retrieval exposes `occurred_at` (or equivalent UTC timestamp) with each line/content.
2. Order the memory block for the prompt: **all of today's lines first**, then older relevant hits.
3. Update `ChatContextBuilder.BuildMemoryBlock` (and callers that supply the list) so truncation still prefers keeping today's lines when budget is tight.
4. Add/adjust unit tests proving two dated episodes order by day and today precedes older semantic hits.

## Files to change (preferred)

- `SoulCore/SoulCore.Memory/` recall/search DTOs & queries (episodic/chat memory — **not** journal books; those are 20.3)
- `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` — `BuildMemoryBlock` + callers
- `SoulCore/SoulCore.Protocol.Tests/` targeted tests

## Coordination

- **20.4** owns mind-block / identity preamble. If you both edit `ChatContextBuilder.cs`, touch **only** memory-block helpers and leave identity/mind preamble to 20.4. Prefer extracting memory helpers if collision risk is high.
- Do not edit SMS Companion or LeisureRunner (20.5/20.6/20.7).

## Acceptance

- [ ] Recall returns dated content (`occurred_at` present in API or compose input)
- [ ] Prompt memory section: today first, then older
- [ ] Test evidence (dotnet test output pasted)
- [ ] Report: `docs/agents/reports/PROP-20.2-BED01-to-PM01.md`
