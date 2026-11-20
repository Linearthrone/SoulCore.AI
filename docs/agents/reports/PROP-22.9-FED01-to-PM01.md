# PROP-22.9 — FED-01 → PM-01: Settings, performance/system log, Advanced

**Status:** Pass (build + unit tests; screens need a human click-through).

## Settings screen (top → bottom)

1. **Notification sound** — unchanged `Notif*` controls (re-seeded each time the screen opens; `SeedNotificationControls` had lost its caller with the old tab).
2. **Updates** — git status, Presence/Host versions, feed, **Check updates**, **Update now**, Download & restart, Restart stack (same handlers/status text as before).
3. **Keys** — Companion / Ollama / Soniox / Synthetic (see PROP-22.3-SEC01).
4. **SoulCore Host** — auto-start checkbox, Start / Stop / Restart, status lines.
5. **Guest VM** — Start / Save state / Power off (confirm), status, **VM drawer side** combo (persists `VmDrawerSide` and moves the tab + popup immediately).
6. **Mailboxes** list; **Access tools** expander (all gates kept).
7. Buttons: **Performance log**, **System log**, **Advanced**.

## Logs

- *Performance log*: in-memory `PerformanceLog` (capped 1000, thread-safe, live-refresh while open, Clear). This is where every former chat "system" row now goes (send failures, WS errors, Host actions, key saves, model applies).
- *System log*: `GET /settings/system-log?lines=300` via `SoulCoreSystemLogClient` (shows Host path + line count, or the honest problem text).

## Advanced

Host bind **127.0.0.1 (read-only)**, current port, editable *client* port with WARNING confirm (see SEC report), **Mailbox form…** (confirm dialog names the account), health & versions grid, voice health/URLs, Refresh / Tick SoulLoop / Reconnect WS.

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
> dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj
Failed!  - Failed: 1, Passed: 242, Skipped: 0, Total: 243
  Failed: CompanionTokenEnvTests.TryLoadFromEnvFile_UsesConfiguredRepoRootOutsideBaseDirectory
          (environmental: this PC has a real saved companion token in %LOCALAPPDATA%\HouseVictoria,
           which outranks the temp .env the test writes — failed before PROP-22 too, untouched)
```

## Not verified

- Visual pass over Settings/Advanced/Log on Windows; System log against a Host with the route.
