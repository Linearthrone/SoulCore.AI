---
type: report
prop_id: PROP-17.5
prop_root: PROP-17-victoria-between-talks
from: FED-01
to: PM-01
status: Pass
created: 2026-10-12
updated: 2026-10-12
branch: cursor/prop17-5-phone-mood-6372
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
task: docs/agents/tasks/PROP-17.5-PM01-to-FED01.md
environment: TINA-main
---

# PROP-17.5 — FED-01 report (phone mood + doing-line; email follow)

## Verdict

**Pass** — Companion Home shows Host mood word + doing-line from the same WS/health sources as the Presence band; Android email editor lists Host accounts only (unlimited ids, no invented `victoria|personal|business` slots). No Victoria Link redesign.

## Delivered

### Mood + doing-line

- `PresenceHud` process hub: `emotion.snapshot` → mood `label`; `presence.status` → `activity` doing-line; `/health` `currentActivity` bootstrap when WS connects (does not clobber fresher presence pushes).
- Installed from `CompanionApp` beside `ChatStore`.
- Thin two-line strip under Home top bar: `Mood — …` / `Doing — …`. No other chrome.
- Frames confirmed present on Host (`EmotionSnapshotSender` on connect; `presence.status` activity pushes; desk band already consumes both).

### Email (17.4 follow)

- Removed hardcoded three-slot picker / empty-list invent of `victoria|personal|business`.
- `EmailMailboxLabels.buildList` mirrors desk `EmailMailboxChoice.BuildList`: Host accounts only.
- Editable mailbox id + Host dropdown; save upserts any safe id (letters/numbers/hyphen/underscore).

## Verify

```text
./gradlew -p House/House.CompanionAndroid test
→ BUILD SUCCESSFUL · 25 unique unit tests (debug+release = 50 runs) · 0 failures
```

Unit coverage: PresenceHud frame apply (4), EmailMailboxLabels list/id rules (3), HealthClient `currentActivity` parse (2), plus prior ChatStore hydrate/merge tests.

## Blockers / follow-ups

- Device/emulator UI smoke against live Host pending on Home PC (unit tests green here; no Android SDK emulator in this agent image if Gradle SDK missing).
- Host-side `EnsureCanonicalSlots` (if still forcing three roles server-side) remains outside FED scope; phone no longer assumes or writes a three-role schema.

## Naming

- Docs/UI operator: **LinearThrone**. Victoria-facing copy uses **Kayleigh** where relevant. No Kurt.
