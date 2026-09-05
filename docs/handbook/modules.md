# Modules map

Quick index of product modules. Deep dives live under [Architecture](./architecture/overview.md).

| Module | Path | Job |
| --- | --- | --- |
| Host | `SoulCore/SoulCore.Host` | HTTP/WS companion API, DI composition root |
| Protocol | `SoulCore/SoulCore.Protocol` | Shared contracts / messages |
| Inference | `SoulCore/SoulCore.Inference` | Ollama + tool loop |
| Memory | `SoulCore/SoulCore.Memory` | Continuity, charter, SoulLoop |
| Hermes (archived) | `SoulCore/SoulCore.Hermes` | Retired package — no Host reference (PROP-7) |
| ChatDesktop | `House/House.ChatDesktop` | Presence desk UI |
| Companion Android | `House/House.CompanionAndroid` | Victoria Link |
| Voice | `House/House.Voice` | STT/TTS helpers |
| Browser capture | `BrowserCaptureBridge/` | Native browser fallback |
| SMS scripts | `sms-*.sh` | Temporary tablet bridge |

## Related

- [Host & protocol](./architecture/host-protocol.md)
- [Inference & tools](./architecture/inference-tools.md)
- [Clients](./architecture/clients.md)
- [Workflows](./workflows/allstart.md)
