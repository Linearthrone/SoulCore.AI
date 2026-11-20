# PROP-22.8 — FED-01 → PM-01: Tasks (Yours / Hers), work-item form, week calendar

**Status:** Pass (build + unit tests for request bodies/parsers; Tasks screen smoke-tested).

## Implementation

- New clients (`Services/`): `SoulCoreOperatorWorkClient` (`/operator/work-items` list/create/delete; snake_case body, kind/title required, priority clamped 1–5, deadline as UTC ISO-8601, project-only JSON columns omitted for tasks), `SoulCoreAgendaClient` (`/operator/agenda`; weekday 0 = Monday, `time_local` `HH:mm` validated), shared `OperatorHttp` (Bearer + `X-Api-Key` from `CompanionToken`, loopback-only, 404 → "Host does not have this route yet — restart Host").
- **Tasks screen**: *Yours* = operator work items (kind label, priority/deadline meta line, Delete with confirm) with **Create New**; *Hers* = existing `/tasks` ledger via `SoulCoreTaskClient` (Add/Suspend/Release/Delete handlers unchanged).
- **Work item form**: task | project `ToggleSwitch`; **phase checkpoints and deliverables are disabled for a task** (project-only columns); fields: title (required), description, details, deadline date + `HH:mm` time, priority 1–5, no-check-in, needs extra permissions, outside collaboration. Save → create → back to Tasks; errors stay on the form.
- **Calendar**: Mon–Sun week, today highlighted, per-day agenda cards with Remove, add form (title, weekday combo, `HH:mm`, notes). Agenda items are weekly recurring (matches the backend contract), so there is no week pager.

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`OperatorSurfaceClientsTests`: task body omits project fields; project body serialises phases/deliverables and converts the deadline to UTC (`2026-12-01T17:00-05:00` → `2026-12-01T22:00:00.000Z`); list parsing incl. garbage → problem text; time validation; week starts Monday (2026-11-20 Fri → index 4, week start 2026-11-16); agenda parsing.

Windows smoke: Tasks screen rendered with *Yours* = "Host does not have this route yet — restart Host." (live Host predates PROP-22.2) and *Hers* listing the real task #32 with Suspend/Release/Delete.

## Not verified

- Create/Delete round-trip and the Calendar screen against a Host that has the routes (needs the rebuilt Host — see 22.11).
