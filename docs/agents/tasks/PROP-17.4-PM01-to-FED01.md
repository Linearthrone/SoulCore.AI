---
type: task
prop_id: PROP-17.4
prop_root: PROP-17-victoria-between-talks
from: PM-01
to: FED-01
priority: P1
status: Pending
created: 2026-10-12
updated: 2026-10-12
wave: between-talks
title: Presence shell — three tabs, band, composer, VM, email dialog
depends_on: none for layout; parked/interest band copy needs 17.1 / 17.3 phrases
blocks: PROP-17.5 email schema follow-up
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
intake: docs/agents/tasks/PROP-17-TT01-to-PM01.md
urgency: same-day layout parallel — Victoria operable for bug-out; band honesty after BED
environment: TINA-main
---

# PROP-17.4 — Presence settings shell + activity band

**Activate:** `@Agents/FED-01.md` then `Start PROP-17.4.` (layout **now**, parallel with 17.1/17.2).

## Operator urgency

Collapse the dead settings chrome **today** so LinearThrone can drive Host/VM/tasks while bug-hunting. Do not claim “parked” or “researching” in the band until 17.1/17.3 expose those statuses — stub honestly until then. Keep PROP-16 update/restart behavior; **move it to System**, do not drop it.

## Problem

Settings tabs and title chrome pretend Email / Services / Notifications / Updates / Update / Restart stack / Correct mood / Refresh are separate useful surfaces. Composer sits outside `ChatPanel`. Activity band does not show park reason or research interest. Email is capped to three roles. PROP-16 pipeline must survive chrome removal.

## Solution (proposal §6.4)

### Tabs (exactly three)

Delete tabs: Email, Services, Notifications, Updates. Keep House drawer lamps.

1. **Identity (read-only)** — Drop essay + Save. Show charter anchors by kind; rank by priority; mark identity-kind “in her prompt”; mark safety/value/boundary/ritual “stored, not in the prompt yet.” Stats: memories, books (incl. life-hacks when 17.2 lands), SOP count, tasks by status, workflows, prompt-mix line, parked + next probe, current interest. Name = Host contact text.
2. **Tools & Access** — Keep gates. Add family switches (files on PC, guest VM files/shell, browser driving vs capture, body/Unreal, Chief Architect). list-tools / system info / her tasks-workflows always on. **Email setup button** → one dialog, one account, **no role cap**. After save, System lists mailbox name + owner (passwords never listed). Remove `EnsureCanonicalSlots` + UI/tool text hardcoded to `victoria|personal|business` (coordinate BED if store helpers live backend — FED owns dialog/UI; BED may need a thin follow if store is Host-only).
3. **System** — Drop endpoint (title bar) and repo folder from UI. Left: Start / Stop / Restart Host + companion token. Right (~half width): bind, port, inference, memory path/open, SoulLoop, desktop/browser/MT4 backends, Presence+Host versions, STT/TTS health. Under token: mailboxes. Then VM via existing `VBoxManage`: Start, Save state, Power off (confirm). Operator buttons — **do not auto-start VM**. One Refresh. Notifications + Updates as short blocks here. **PROP-16** git/Host update + Restart stack live in this System block (update-available mark beside presence/host opens it — **not** title-bar buttons).

### Title strip

Remove: Update, Restart stack, Correct mood, Refresh. Update-available mark → System block only.

### Chat + band

1. Composer **inside** `ChatPanel` (pop-out includes draft, Hold to talk, Send). Wrap; grow with draft; one line after send; Enter sends; Shift+Enter newline.
2. Activity band **always under the main split** (not where composer used to float):
   1. Doing
   2. On her mind (interest or quiet between talks — never last order restamped)
   3. Mood (word + warm/cool, calm/keyed-up, steady, focused)
   4. Influences (≤3)
3. Correct mood, if kept → disclosure in band, not chrome.
4. Task list in band/settings: title, surface, status, why, add / suspend / release (wire to 17.1 APIs when present; disable with honest empty state until then).

### Voice

No new tab. Health next to Hold to talk + System readout. Compact: enabled, voice name, health. URLs under Advanced.

## Do not

- Drop PROP-16 update/restart behavior
- Auto-start `victoria-sandbox`
- Phone redesign (17.5)
- Pretend non-identity charter kinds already weigh in the prompt
- Cards/chrome clutter beyond the three-tab spec

## Acceptance

- [ ] Exactly three settings tabs; deleted tabs gone
- [ ] Title strip without Update / Restart stack / Correct mood / Refresh; PROP-16 on System with update mark
- [ ] Identity read-only portrait + stats as specified
- [ ] Unlimited email accounts via dialog; no three-role cap in UI
- [ ] System Host controls + VM Start/Save/Power off (confirm); no auto-start
- [ ] Composer inside ChatPanel; band under main split with four lines
- [ ] Parked/interest copy honest (real after 17.1/17.3; stub until then)
- [ ] Report: `docs/agents/reports/PROP-17.4-FED01-to-PM01.md`

## Handoff

Start layout **immediately** in parallel with BED 17.1/17.2. Revisit band strings when 17.1/17.3 Pass. Unblocks 17.5 email follow-up.
