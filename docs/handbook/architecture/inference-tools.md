# Inference & tools

## Path

Chat uses **Ollama** (`Inference:BaseUrl` / `Model`) via `CompleteAsync` / `CompleteWithToolsAsync`.

**Hermes is retired (BED-185).** Host forces `Hermes:Enabled=false` and remaps legacy `hermes` tool backends to safe defaults (`cua` / `llmod` / `none`). Do not reintroduce PreferHermes as a live control plane.

## Tool areas (ITool registry)

| Area | Examples |
| --- | --- |
| Desktop | `desktop_screenshot`, click/type/key — gated; prefer Victoria sandbox scope |
| Browser | Playwright Victoria browser (`browser_*`) — not Kurt’s Chrome |
| Body / UE | `speak`, `loco`, `look`, `play_animation`, eye capture |
| Memory | `recall_memory`, `store_memory` |
| Email | `email_*` (IMAP/SMTP accounts in config) |
| MT4 | `llmod` HTTP bridge (default) |
| SMS | `send_screenshot_mms` (opt-in still to Kurt) |
| Workflow / CA | task/workflow tools, Chief Architect playbooks |

## Consistency rules

- Register tools as `ITool` singletons collected by `ToolRegistry`.
- Prefer indexes / typed options; no `any`-shaped tool args.
- SMS inbound path must **never** call `CompleteWithToolsAsync` (no tools from carrier).

## Defaults to remember

- Browser backend in Host appsettings: **playwright**
- Desktop backend: **cua** (with native fallback)
- MT4 backend: **llmod** (not hermes)
