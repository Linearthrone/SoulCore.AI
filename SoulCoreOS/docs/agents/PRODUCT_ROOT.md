---
type: config
id: soulcoreos-product-root
updated: 2026-07-29
owner: OPS-01
---

# SoulCoreOS Product Root

| Field | Value |
| --- | --- |
| Product | SoulCoreOS (House Victoria soul-machine OS) |
| Code home | `SoulCoreOS/` (this tree) in monorepo `soulcore.ai` |
| Sister app | `SoulCore/` (.NET 8 Host + protocol) + `House/` |
| Base image | `ghcr.io/ublue-os/bazzite-nvidia-open:stable` |
| Target GPU | NVIDIA RTX 5070 Ti (Blackwell) → **nvidia-open only** |
| Body host | Shadow PC `house-victoria` — Unreal bridge `ws://house-victoria:8888` |
| Soul services | SoulCore.Host · Hermes · Ollama · Tailscale · Steam (Bazzite) · Distrobox · Opera (if RPM) · p4d (later) |
| Home layer | Cursor AppImage · Stability Matrix Flatpak/AppImage |
| Explicitly excluded | Unreal Engine · proprietary `bazzite-nvidia` base |

## Related monorepo product root

SoulCore.AI companion product declaration:
[`../../docs/agents/PRODUCT_ROOT.md`](../../../docs/agents/PRODUCT_ROOT.md)
(workspace root).

## Active OPS ticket

`docs/agents/tasks/TASK-20260729-001-PM01-to-OPS01.md` — image definition +
report.
