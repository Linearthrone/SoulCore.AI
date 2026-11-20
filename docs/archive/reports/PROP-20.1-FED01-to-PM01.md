---
type: report
prop_id: PROP-20.1
prop_root: PROP-20-victoria-mind-path
from: FED-01
to: PM-01
status: Completed
created: 2026-11-17
updated: 2026-11-17
task: docs/agents/tasks/PROP-20.1-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.1 — FED-01 report (Identity live anchors)

## Verdict

**Completed** — Desk Identity tab now fetches `GET /settings/identity` and paints live anchor **titles + bodies**. It no longer treats `/health` lock fraction as the only identity content. Empty/error Host responses stay honest (no fabricated biography).

## Delivered

1. **`SoulCoreIdentitySettingsClient`** — thin companion HTTP GET to `/settings/identity` (same loopback + auth header pattern as tools/email settings).
2. **`IdentityAnchorsFormatter`** — renders charter summary + identity anchors + other kinds with title/body; empty/unreachable → explicit “not invented / no anchors fabricated”.
3. **`RefreshIdentityDetailAsync`** — Identity tab / open / health poll call sites refresh from the identity endpoint; display name prefers Host `displayName`, else local UI setting.
4. Stopped static placeholder copy that only echoed `/health` locked/total as charter content.

## Acceptance

| Criterion | Result |
| --- | --- |
| Desk Identity lists live Host anchor titles + bodies from `/settings/identity` | **Pass** (wired + formatter unit-tested against Host DTO shape) |
| Empty/error Host response does not fabricate anchors | **Pass** (unit + client unreachable returns empty lists) |
| Local build evidence for ChatDesktop | **Pass** |
| Report with request/response or UI evidence | **Pass** (below) |

## Verify

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release
  → Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Release --filter FullyQualifiedName~SoulCoreIdentitySettingsClientTests
  → Passed!  Failed: 0, Passed: 2, Total: 2
```

### Live Host probe (this machine)

```text
GET http://127.0.0.1:7710/settings/identity → FAIL (Host down / timeout)
```

Host Release rebuild blocked outside FED scope (`SqliteMemoryStore` missing `RecallSimilarDatedAsync` / `RecallRecentDatedAsync`). UI path when Host is down paints:

`Host unreachable — …` + `(No anchors fabricated.)`

Expected paint when Host is up (Host contract from TASK-177 / BED):

```json
{
  "displayName": "Victoria",
  "identityAnchors": [ { "kind":"identity", "title":"…", "body":"…" } ],
  "anchors": [ { "kind":"…", "title":"…", "body":"…" } ],
  "charter": { "anchors": N, "locked": M, "mode": "…" }
}
```

Formatter sample (unit evidence):

```text
Charter: calibration · 1/2 anchors locked
…
Identity anchors (in prompt)
[identity] Who I am · locked
I am Victoria.

Other anchors (stored)
[safety] Do no harm · unlocked
Refuse harm requests.
```

## Files (ChatDesktop + report only)

- `House/House.ChatDesktop/Services/SoulCoreIdentitySettingsClient.cs` (new)
- `House/House.ChatDesktop/Services/IdentityAnchorsFormatter.cs` (new)
- `House/House.ChatDesktop/MainWindow.axaml.cs` (`RefreshIdentityDetailAsync` + dispose)
- `House/House.ChatDesktop.Tests/SoulCoreIdentitySettingsClientTests.cs` (new)
- `docs/agents/reports/PROP-20.1-FED01-to-PM01.md`

## Out of scope (per ticket)

- Host / Memory / SMS / sibling PROP-20.2–20.6 paths untouched
- No Host charter schema changes

## Naming

- Operator: **LinearThrone**. No forbidden personal names.
