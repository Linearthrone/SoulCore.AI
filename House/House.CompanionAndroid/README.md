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

## What this is (TASK-147 / 149)

- Chat UI + settings (WS URL + API token)
- `SoulCoreWsClient` — OkHttp WS: `chat.send` → streaming `chat.delta` / `chat.done` (FED-148)
- Token in **EncryptedSharedPreferences** (Android Keystore `MasterKey` AES256_GCM) — FED-149
- Auth header helper: preferred `Authorization: Bearer`, alias `X-Api-Key` (Host BED-155)
- HTTP `:17890` / `HouseVictoria.App` `/api/remote/v1/*` **removed**
- Notifications / foreground service — **placeholder** (FED-150/151)

## Auth + connect URL

| Setting | Storage | Notes |
| --- | --- | --- |
| WebSocket URL | plain `SharedPreferences` (`companion_prefs`) | Non-secret |
| API token | `EncryptedSharedPreferences` (`companion_secure_prefs`) | Keystore-backed; never logged raw |
| Clear token / Clear all | Settings buttons | Token wiped; “clear all” also resets URL to loopback default |

When Host has `SOULCORE_COMPANION_API_TOKEN` set, `/ws` requires Bearer or `X-Api-Key`. Loopback with token unset remains optional (desktop trust). `/health` stays unauthenticated.

## What this is not

- No audio / MediaGen / Gallery (LLMOD features not ported)
- Does not change `SoulCore.Host` bind policy

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
