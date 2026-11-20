---
type: accept
prop_id: PROP-22
from: PM-01
created: 2026-11-20
status: accepted
versions:
  presence: 1.1.3
  host: 1.1.3
---

# PROP-22 accept — Presence glass shell

## Shipped

- Style lock: v3 boards (`docs/agents/unexecuted_proposals/assets/prop22/v3/`) — polished glass fill, rim frost, bottom chrome **V · Call · Settings**, radial orbs outside upper-left, VM drawer protrudes ~1280×800.
- Host: migration 016, `/operator/work-items`, `/operator/agenda`, `/settings/inference/profiles`, `/settings/inference/apply`, `/settings/system-log`.
- Presence: glass slab 19.5:9, status orb + radial, VM drawer popup, Victoria drawer + in-glass screens, settings with masked tokens, performance/system log, advanced + mailbox confirm.
- Fix: `SetUpdateUiBusy` marshals to UI thread (was unobserved `Call from invalid thread`).

## Evidence (home PC 2026-11-20)

- `GET /health` → `status=ok`, `version=1.1.3`
- Auth CRUD: work-item + agenda create/delete 200
- Desktop unit tests (PROP-22 filters): **40 passed**
- Presence opened `460×996.8`, product `1.1.3`
- Velopack apply from local packages (Setup.exe was file-locked by a prior Setup process once; cleared and repacked)

## Operator notes

- Do not open a second Presence while the VM drawer is embedded (HWND steal).
- New Host routes need companion token (`X-Api-Key` / saved Presence token).
- Live Host was restarted on `:7710`; Presence installed under `%LocalAppData%\HouseVictoria.Presence\current\`.

## Splits

22.1–22.11 reports filed under `docs/agents/reports/PROP-22.*`. Next free PROP **N = 23**.
