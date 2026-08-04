# House.CompanionAndroid — Victoria Link (SoulCore thin client)



Port of LLMOD Victoria Link UX onto SoulCore.Host — **not** HouseVictoria.App `:17890`.



| Item | Value |

| --- | --- |

| Package / applicationId | `com.housevictoria.companion` |

| Launcher label | Victoria Link |

| minSdk | 26 |

| targetSdk / compileSdk | 34 |

| UI | Jetpack Compose — Home · MediaGen · Gallery · Settings |

| Chat | `ws://127.0.0.1:7700/ws` |

| Media HTTP | `http://127.0.0.1:7700/api/companion/v1/*` |

| Contact stub | `contactId=victoria` (multi-persona reserved for a future external service) |



## What this is



- **Home** — single-Victoria chat (WS streaming + proactive Host pushes)

- **MediaGen** — ComfyUI generate via Host (`POST /api/companion/v1/media/generate`)

- **Gallery** — local cache of downloaded PNGs

- **Settings** — WS URL, HTTP base, Keystore token, notifications, FGS disconnect

- Foreground service keeps WS alive; reply notifications on `chat.done` (including proactive)

- Auth: `Authorization: Bearer` / `X-Api-Key` when `SOULCORE_COMPANION_API_TOKEN` is set



## Host APIs



| Method | Path | Purpose |

| --- | --- | --- |

| WS | `/ws` | Chat + proactive `chat.done` (`proactive=true`, optional `mediaId`) |

| GET | `/api/companion/v1/contacts` | Single Victoria contact stub |

| POST | `/api/companion/v1/messages/push` | Manual / tool outbound text (+ optional media) |

| GET | `/api/companion/v1/media/models` | ComfyUI checkpoints |

| POST | `/api/companion/v1/media/generate` | Generate + store; `pushToChat` optional |

| GET | `/api/companion/v1/media/{id}/file` | Download PNG |



Config: `Companion:*` and `SoulLoop:ProactiveChatEnabled` in Host `appsettings.json`.



## What this is not



- No smartphone screenshot / computer-use

- No multi-persona inbox / ContactBook UI (framework stub only)

- No voice / STT / TTS

- No dependency on LLMOD WPF overlay or `:17890`



## Build / install



```powershell

cd House\House.CompanionAndroid

.\gradlew.bat assembleDebug

adb install -r app\build\outputs\apk\debug\app-debug.apk

adb reverse tcp:7700 tcp:7700

```



Remote: Tailscale serve — see `docs/runbooks/tailscale-serve-soulcore.md`. Exempt **Victoria Link** from OEM battery optimization for long background WS.



## OEM caveat



Aggressive battery savers may kill the FGS after hours–days. Exempt the app for reliable proactive dings.

