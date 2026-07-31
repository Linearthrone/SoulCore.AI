# House Victoria — Android companion releases

Sideload APKs for Phase 0 (chat + settings). Not published to Play Store.

| File | Version | Notes |
|------|---------|--------|
| `HouseVictoria-Companion-0.1.0-phase0-release.apk` | 0.1.0-phase0 | Signed release; package `com.housevictoria.companion` |

## Install (phone)

1. Download the `.apk` from this folder (GitHub → raw / download).
2. Allow install from unknown sources for your browser/Files app.
3. Open the APK and install.
4. In **Settings**, set WebSocket URL + API token to match your Host (Tailscale IP or `adb reverse`).

## Rebuild locally

```bash
cd House/House.CompanionAndroid
# optional: copy signing.properties.example → signing.properties + keystore (gitignored)
./gradlew :app:assembleRelease
```

Do **not** commit keystores or `signing.properties`.
