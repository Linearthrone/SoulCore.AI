---
type: report
prop_id: PROP-17.4
prop_root: PROP-17-victoria-between-talks
from: FED-01
to: PM-01
status: Pass
created: 2026-10-12
updated: 2026-10-12
branch: cursor/prop17-4-presence-shell-e3de
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
task: docs/agents/tasks/PROP-17.4-PM01-to-FED01.md
environment: TINA-main
---

# PROP-17.4 — FED-01 report (Presence shell)

## Verdict

**Pass** — Presence settings collapsed to three tabs; title chrome cleaned; composer in `ChatPanel`; activity band under the main split; email setup dialog (unlimited ids); System hosts PROP-16 update/restart + VM operator controls. Parked/interest/task ledger copy is honest-stub until 17.1/17.3 Host APIs.

## Delivered

### Settings (exactly three)

- Deleted tabs: Email, Services, Notifications, Updates.
- **Identity** — read-only name; charter kinds labeled (identity = in prompt; safety/value/boundary/ritual = stored, not in prompt yet); stats + prompt-mix with honest stubs for memories/books/SOP/tasks/parked/interest.
- **Tools & Access** — existing gates kept; family switches present but **disabled** (Host gates not exposed — not invented); Email setup button → dialog; email permission checkboxes stay here.
- **System** — left: Host Start/Stop/Restart, companion token, mailbox list (name + owner, no passwords), VM Start / Save state / Power off (confirm), Notifications short block, **PROP-16 Updates** block (Check / Update now / Download & restart / Restart stack). Right: bind/port/inference/memory/SoulLoop/backends/versions + STT/TTS health (URLs under Advanced). Endpoint + repo folder removed from UI (repo still resolved via settings/env). One Refresh. No VM auto-start.

### Title strip

- Removed Update, Restart stack, Correct mood, Refresh.
- Update-available mark beside Presence/Host versions → opens Settings → System (PROP-16 preserved).

### Chat + band

- Composer (Attach, Hold to talk, voice health, Quote, Send, multi-line draft) inside `ChatPanel` so pop-out includes it. Enter sends; Shift+Enter newline; height grows with draft.
- Activity band under main split: Doing / On her mind / Mood (+ Correct mood disclosure) / Influences; task add/suspend/release disabled until 17.1.

### Email

- `EmailSetupWindow` — one dialog, unlimited mailbox ids (no `victoria|personal|business` UI cap).
- `EmailMailboxChoice.BuildList` lists Host accounts only.

### VM

- `LocalStackControl` Start / Save state / Power off via `VBoxManage` for `victoria-sandbox`. Operator-only; never auto-start on open.

## Verify

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj  → succeeded
dotnet test  House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj → 89 passed
```

## Blockers / follow-ups

- Band parked / interest / task ledger strings stay stub until PROP-17.1 / 17.3 expose statuses.
- Family switches await Host `/settings/tools` fields (FED left disabled — BED thin follow if needed).
- Host `EnsureCanonicalSlots` (if still forcing three roles server-side) is outside FED scope; UI no longer caps.
- Windows smoke (VM buttons, email dialog against live Host) pending on Home PC.

## Naming

- Docs/UI operator: **LinearThrone**. Victoria-facing copy uses **Kayleigh** where relevant. No Kurt.
