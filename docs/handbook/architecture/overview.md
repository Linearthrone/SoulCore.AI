# Architecture overview

SoulCore.AI is the backend and desk/phone clients for **House Victoria**: Kurt talks to Victoria on the desk (ChatDesktop) and by SMS (tablet gateway). A separate Unreal “body” lives on the shadow PC.

```text
Kurt phone ──SMS──► Samsung Tab (gateway) ──HTTPS/Tailscale──► SoulCore.Host :7700
Kurt desk  ──WS/HTTP─────────────────────► SoulCore.Host :7700
                                              │
                                              ├─ Ollama inference + tools
                                              ├─ SQLite memory / charter
                                              ├─ Presence WS → ChatDesktop
                                              └─ (optional) Unreal bridge → shadow UE
```

## Major modules

| Module | Path | Job |
| --- | --- | --- |
| Host | `SoulCore/SoulCore.Host` | Composition root: WS `/ws`, companion HTTP API, SMS, voice hooks, soul loop |
| Inference | `SoulCore/SoulCore.Inference` | Ollama client, tool registry (desktop/browser/email/MT4/body/…) |
| Memory | `SoulCore/SoulCore.Memory` | SQLite episodic memory, tasks, workflows |
| Config | `SoulCore/SoulCore.Config` | Options + `.env` loader (`SOULCORE_*`) |
| Protocol | `SoulCore/SoulCore.Protocol` | Wire frame types (`chat.done`, etc.) |
| Adapters.Ws | `SoulCore/SoulCore.Adapters.Ws` | Presence hub, Unreal verb stubs |
| Hermes | `SoulCore/SoulCore.Hermes` | **Retired** — archived package; Host no longer references it (PROP-7) |
| ChatDesktop | `House/House.ChatDesktop` | Presence desk UI |
| Companion Android | `House/House.CompanionAndroid` | Victoria Link |
| Voice | `House/House.Voice` | STT/TTS helpers started by ALLSTART |

## What is temporary vs permanent

| Temporary (bridge) | Permanent (product) |
| --- | --- |
| Tasker HTTP SMS trigger | Host companion API + allowlist |
| Termux outbound poller | Self-sufficient House SMS gateway app (goal) |
| DIGITS line identity | Tablet SM-X218U MDN |

Hermes PreferHermes is **gone** (not a temporary bridge): Host uses `NullHermesClient` / Ollama tool-loop only.

## Related

- PROP registry: `docs/agents/PROP_NUMBERING.md`
- Agent seats: `Agents/`
- Detailed ops: `docs/runbooks/`
