---
type: task
prop_id: PROP-16.1
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.1-FED01-to-PM01.md
pm_note: Accepted 2026-10-22. Build 0 errors; GlassShellTests 9 plus layout tests 2. Live tray and drawer clicks were not driven. That gap does not block 16.2.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.0 (Accepted)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Glass shell — tray, hotkey, drawers, radial, per-persona transcripts
---

# PROP-16.1 — Presence glass shell

## Problem

Presence is one frameless Avalonia window (`House/House.ChatDesktop/MainWindow.axaml`). It has no tray, no show/quit hotkey, a dark-only theme, and one local transcript thread in `House/House.ChatDesktop/Services/ChatHistoryStore.cs` (`presence-chat.db`, conversation `presence-local`). The accepted brief is a tray-owned glass overlay. Phase 1 is the shell only.

## Root

The shell, theme, and local transcript all live in `House/House.ChatDesktop`. Host persona APIs already exist from PROP-15. This ticket does not change Host trait compilation or VM embed.

## Solution

Build Avenue B phase 1 on the current single-active runtime. Read the proposal Phase 1 and the nine frames. Art caveats in Phase 0 override the frames where they disagree.

1. System tray. Right-click: Open main drawer, Open settings, Exit. The process is absent from the taskbar (`ShowInTaskbar=false`) only after the hotkeys below work.
2. Hotkeys, locked by PM-01: `Ctrl+Shift+H` toggles the main chat drawer. `Ctrl+Shift+Q` exits. Both must work with no tray icon.
3. Drawers are separate frameless windows for chat, settings, creator, contacts, HOUSE lamps, and the left-edge radial. Reuse the current composer (send, quote, attach, hold-to-talk, video stub), mood correction, update toast, and persona wizard steps. Do not invent a new UI toolkit. Stay on Avalonia 11.3.
4. Light and dark brush sets. Chrome opacity slider with a floor around 0.55 so text stays readable. No guest HWND in this ticket, so opacity applies to chrome only.
5. Left-edge half-circle grows about 4x into a custom-drawn radial: Create Agent, Agent Contacts, up to six favorite slots (photo and name, names unique).
6. Chat lists open threads with each persona’s name and picture. One mode only. Many mode is out of scope.
7. Local transcripts become per persona. `presence-chat.db` must not stay one shared `presence-local` thread. Key rows by `personaId`.
8. HOUSE lamps stay a drawer: SoulCore, Ollama, Unreal, Comfy, CUA, Sandbox. Ollama is a real lamp. CUA is a toggle. Exit, crash, and update-restart clear the CUA lamp.
9. Settings sections stay findable: Identity, Tools & Access, Email, System, Services, Notifications, Updates. Version text comes from the existing Presence and Host build lines.
10. Browser pane and desktop stills stay reachable inside the chat drawer. Do not drop them.
11. Drawer dismiss is the tray, the edge tab, or a drag-away. No window caption close that quits the process. Closing a drawer hides it.

## Do not

- Live VirtualBox quarter-screen viewport, clickthrough, or view-opacity (PROP-16.2, not ticketed).
- New trait catalog, eight-line compiler changes, or physical-description AI help (16.4 / 16.5).
- Chat-multi, extra SQLite bundles, or tool dispatch changes (16.6).
- A frosted guest, or `SetParent` of the VM into a transparent window.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/MainWindow.axaml` and code-behind
- `House/House.ChatDesktop/App.axaml` (light/dark brushes)
- `House/House.ChatDesktop/Services/ChatHistoryStore.cs`
- New drawer windows beside `MainWindow` as needed
- Tests next to existing `House/House.ChatDesktop.Tests` for transcript persona key and hotkey plan if you extract one

## Acceptance

- [ ] Tray menu opens chat, opens settings, and exits
- [ ] `Ctrl+Shift+H` shows and hides the chat drawer with the tray icon absent
- [ ] `Ctrl+Shift+Q` exits with the tray icon absent
- [ ] No taskbar button while those hotkeys work
- [ ] Radial offers Create Agent, Contacts, and at most six favorites
- [ ] Light and dark both render; chrome opacity cannot go below ~0.55
- [ ] A second persona does not read the first persona’s local transcript
- [ ] Composer, mood correction, HOUSE lamps, browser pane, desktop stills, and Settings sections still open
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.1-FED01-to-PM01.md`
