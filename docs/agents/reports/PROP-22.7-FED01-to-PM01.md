# PROP-22.7 — FED-01 → PM-01: Victoria drawer, portrait card, Bond, Journal/Activities/Research

**Status:** Pass (build; drawer + navigation smoke-tested; portrait/Bond need a human click-through).

## Implementation

- **Victoria drawer** (bottom-left overlay + scrim, opaque): portrait button (`Assets/house-victoria.png`), `DoingNowText` (= Host `currentActivity` or **"Nothing right now"**), `LastActivityText` (newest `GET /activities` row; "Nothing recorded yet." when empty) + `OpenJournalLink` (only when the row carries a journal entry id → opens Journal and `OpenEntryAsync`), on-her-mind / honesty / mood lines, *Correct mood…* (existing panel), and nav tiles Journal / Activities / Research / Bond / Tasks / Calendar.
- **Portrait card** (`PortraitCard` overlay): model `ComboBox` from `GET /settings/inference/profiles` (active model preselected), **Apply** → `POST /settings/inference/apply` (status line shows applied/problem; health re-probed), charter synopsis from the identity anchors, resting traits as `ProgressBar.gauge` rows. Updated whenever `RefreshIdentityDetailAsync` runs (`_lastIdentity` / `ApplyPortraitIdentity`).
- **Bond screen**: bond counters, pending band-cross list with **Confirm / Dismiss** (existing handlers), soak, growth ledger; remaining identity read-outs in a *More identity detail* expander. Reuses `SoulCoreIdentitySettingsClient` data.
- **Journal / Activities / Research** are in-glass `Views/JournalView|ActivitiesView|ResearchView` `UserControl`s (logic lifted from the old pop-up windows, which were deleted — no references remained). Opened from the drawer or Settings; `ReloadAsync` on entry.
- Removed: house lamp drawer, lamp tray, Video button, family-switch stubs, Identity/Tools/System tab wall.

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Windows smoke: Victoria button opened the drawer (portrait, doing-now, last activity, mood, tiles); the Tasks tile navigated to the Tasks screen and back. A first smoke showed the drawer too translucent over chat bubbles → fixed (`Background=#F5111821` on drawer and portrait card) and rebuilt (0 warnings / 0 errors).

## Not verified

- Portrait Apply against a Host that has the PROP-22.2 routes (live Host on :7710 predates them → Apply/profiles show the honest "Host does not have this route yet" text).
- Bond Confirm/Dismiss round-trip and Journal `OpenEntryAsync` deep link.
