# House.CompanionAndroid — Victoria phone companion (Phase 0 shell)

Forked from LLMOD `AndroidRemoteCompanion/` (Kotlin) for SoulCore.

| Item | Value |
| --- | --- |
| Package / applicationId | `com.housevictoria.companion` |
| minSdk | 26 |
| targetSdk / compileSdk | 34 |
| UI | Jetpack Compose (chat list + compose box + settings) |
| Default endpoint | `ws://127.0.0.1:7700/ws` (SoulCore.Host) |
| Tailscale placeholder | `wss://<host>.<tailnet>.ts.net/ws` |

## What this is (TASK-147 / 149 / 150 / 151)

- Chat UI + settings (WS URL + API token)
- `SoulCoreWsClient` — OkHttp WS: `chat.send` → streaming `chat.delta` / `chat.done` (FED-148)
- Token in **EncryptedSharedPreferences** (Android Keystore `MasterKey` AES256_GCM) — FED-149
- Auth header helper: preferred `Authorization: Bearer`, alias `X-Api-Key` (Host BED-155)
- **Foreground service** (`CompanionWsService`) keeps WS alive when backgrounded — FED-150
- Persistent low-importance notification while connected; **Disconnect** from Settings or notification action
- **Reply notifications** on `chat.done` when backgrounded (custom sound + vibration) — FED-151
- HTTP `:17890` / `HouseVictoria.App` `/api/remote/v1/*` **removed**

## Auth + connect URL

| Setting | Storage | Notes |
| --- | --- | --- |
| WebSocket URL | plain `SharedPreferences` (`companion_prefs`) | Non-secret |
| API token | `EncryptedSharedPreferences` (`companion_secure_prefs`) | Keystore-backed; never logged raw |
| Clear token / Clear all | Settings buttons | Token wiped; “clear all” also resets URL to loopback default |

When Host has `SOULCORE_COMPANION_API_TOKEN` set, `/ws` requires Bearer or `X-Api-Key`. Loopback with token unset remains optional (desktop trust). `/health` stays unauthenticated.

## Background WS (FED-150)

| Piece | Detail |
| --- | --- |
| Service | `CompanionWsService` — `foregroundServiceType=dataSync` |
| Hub | `CompanionConnection` — process-scoped `SoulCoreWsClient` shared by UI + FGS |
| Notification channel | `victoria_connected` (IMPORTANCE_LOW, silent, ongoing) |
| Stop | Settings **Disconnect (stop background WS)** or notification **Disconnect** action |
| Keep-alive | OkHttp `pingInterval=30s` |

### OEM / lifetime caveats

Stock Android (Pixel / AOSP): with the FGS notification visible, the WS typically stays up for **hours** while the device is idle (screen off). Doze may delay non-priority work but does not normally tear down an active FGS socket immediately.

Aggressive OEM battery savers (Xiaomi MIUI, Huawei/Honor, Oppo/ColorOS, Samsung optimized battery) may still kill or freeze the process after **hours–days** unless the user exempts **Victoria Companion** from battery optimization / allows “autostart”. Document this for QA-154 on-device smoke.

## Reply notifications (FED-151)

| Piece | Detail |
| --- | --- |
| Channel | `victoria_replies` (IMPORTANCE_HIGH, lock-screen visible) |
| Trigger | `chat.done` while app process is backgrounded (`ProcessLifecycleOwner`) |
| Prefs keys | `notif_enabled`, `notif_sound_path`, `notif_vibration` in `companion_prefs` |
| Sound | System default, or custom file imported into app-private `files/sounds/` |
| Tap | Opens `MainActivity` (chat); clears reply alert; does **not** stop FGS |

FGS connected notification (`victoria_connected` / id `15001`) is unchanged and separate from reply alerts (id `15101`).

## What this is not

- No audio / MediaGen / Gallery (LLMOD features not ported)
- Does not change `SoulCore.Host` bind policy
- No quiet hours (follow-on)

## Build

```powershell
cd House\House.CompanionAndroid
.\gradlew.bat assembleDebug
```

APK: `app\build\outputs\apk\debug\app-debug.apk`

Requires Android SDK (`local.properties` → `sdk.dir`). Copy `local.properties` from a machine with Android Studio, or set `ANDROID_HOME`.

## Emulator / USB loopback

Host binds `127.0.0.1:7700`. Forward into the emulator or USB device:

```powershell
adb reverse tcp:7700 tcp:7700
```

Then the app default `ws://127.0.0.1:7700/ws` reaches the PC Host.

## Source lineage

- Gradle AGP/Kotlin line + wrapper: LLMOD `AndroidRemoteCompanion`
- Settings UX pattern (URL + token + test/save): adapted from LLMOD connection settings
- Default port/path: mirrors `House.ChatDesktop` `ConnectionDefaults` (`127.0.0.1:7700/ws`)
- Auth headers: align with `SoulCore.Host.Ws.CompanionWsAuth` (BED-155 / SEC-152)
- Reply alerts: Android channels mirroring desktop `NotificationService` intent
