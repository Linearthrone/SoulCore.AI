# PROP-22.10 — QA-01 → PM-01: minimal PROP-22 desktop checklist

**Scope:** Presence glass shell (22.3–22.9). Written by the FED/SEC pass, not a full QA round — it separates what a build/unit test/smoke proved from what still needs a Windows human.

Legend: **B** = verified by build, **U** = unit test, **S** = smoke on this PC (isolated `%LOCALAPPDATA%`, live Host read-only), **H** = needs human Windows visual pass.

| # | Check | Result | How |
|---|---|---|---|
| 1 | `dotnet build House.ChatDesktop` 0 warnings / 0 errors (`--no-incremental`) | Pass | B |
| 2 | Unit tests | 242 / 243 pass | U — one pre-existing environmental failure (`CompanionTokenEnvTests…OutsideBaseDirectory`: this PC has a real saved companion token that outranks the test's temp `.env`) |
| 3 | Window opens 460×~997, aspect lock, min 360 | Pass | S (`presence-startup.log`: `size 460x996.8`) |
| 4 | Chat transcript shows no system rows | Pass | S (history loaded; none shown) + code (`IsSystem` filtered, `AppendSystemNotice` → PerformanceLog) |
| 5 | Orb colours follow priority; off ≠ down | Pass | U (aggregator, 22 cases) + S (pink pulse on real activity; popup lists dim Comfy/Mailbox) |
| 6 | Radial popup outside upper-left, closes on outside click/Esc, toggles on orb | Pass (open + list) / H (outside-click close, Esc) | S |
| 7 | VM drawer opens docked beside window, embeds KsDevice, ✕ closes cleanly | Pass | S |
| 8 | VM drawer on the **left** | — | H |
| 9 | Her-screen input overlay (soft cursor/click) while drawer open | — | H |
| 10 | Victoria drawer opens; opaque over chat | Pass (after fix) | S |
| 11 | Tasks screen: Yours/Hers render; honest message when Host lacks routes | Pass | S |
| 12 | Create task / project, deadline, project-only fields disabled for task | — | H (needs Host with PROP-22.2 routes) + U for body shape |
| 13 | Calendar week add/remove | — | H (needs Host with routes) + U for parsing/validation |
| 14 | Portrait card: models list/Apply, traits | — | H |
| 15 | Bond confirm/dismiss | — | H |
| 16 | Settings: token rows show only Saved/Not set; replace + Save; no value ever visible | — | H (logic: U + code review) |
| 17 | Mailbox confirm dialog names the account; Cancel writes nothing | — | H |
| 18 | Advanced: bind 127.0.0.1 fixed; port change shows WARNING; env-pinned port refused | — | H |
| 19 | Call: connecting = full scrim, connected = top third | — | H (starts a real voice session) |
| 20 | Performance log / System log screens | — | H |
| 21 | Intentionally-off services never yellow/amber/red | Pass | U |
| 22 | No NativeControlHost in MainWindow / no TabControl | Pass | U (`PresenceNativeHostManifestTests`) |

**Failures found:** none open. One defect found and fixed during smoke: Victoria drawer translucent over chat text (now opaque). One latent bug fixed: `CompanionTokenStore.Save` threw on the second save (hidden file).

**Recommended next step:** restart the Host from this checkout (see PROP-22.11) so `/operator/*`, `/settings/inference/*` and `/settings/system-log` exist, then run rows 12–20 by hand.
