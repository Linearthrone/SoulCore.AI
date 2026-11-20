---
type: issue
id: ISSUE-20261117-001
severity: P1
status: Fixed
fixed: 2026-11-17
note: Code+QA+SLOP closed. Restart Presence if desk still shows old client; OPS-221 may still report.
created: 2026-11-17
component: House.ChatDesktop Identity
---

# Identity tab: Host unreachable — JSON id not a string

## Symptom

Identity tab shows Host unreachable; detail like `The JSON value could not be converted to System.String. Path: $.identityAnchors[0].id`.

## Cause

`GET /settings/identity` returns `"id": 11` (number). `SoulCoreIdentitySettingsClient.AnchorDto.Id` is `string?`. `JsonException` is mapped to `Reachable = false`.

## Fix

Deserialize `id` as number (or `JsonElement`) and stringify when mapping to `IdentityAnchorSnapshot`.
