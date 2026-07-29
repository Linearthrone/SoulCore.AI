---
type: architecture
id: SOULCOREOS
updated: 2026-07-29
owner: OPS-01 / TT-01
---

# SoulCoreOS Architecture

## Purpose

SoulCoreOS is the **immutable (bootc) workstation OS** for the House Victoria
soul-machine: GPU inference (Ollama), Hermes gateway, SoulCore.Host, Tailscale
mesh, and Steam/gaming via Bazzite — while the Unreal **body** remains on the
Shadow PC named `house-victoria`.

```text
┌─────────────────────────────────────────────────────────────┐
│  SoulCoreOS (this image)                                    │
│  base: ghcr.io/ublue-os/bazzite-nvidia-open                 │
│                                                             │
│  ┌──────────────┐  ┌─────────┐  ┌────────┐  ┌───────────┐ │
│  │ SoulCore.Host│  │ Hermes  │  │ Ollama │  │ Tailscale │ │
│  │ :7700 loopbk │  │ :8642   │  │ :11434 │  │ mesh      │ │
│  └──────┬───────┘  └─────────┘  └────────┘  └───────────┘ │
│         │ Tailscale / hostname resolve                      │
│         ▼                                                   │
│    ws://house-victoria:8888  (Unreal bridge — remote body)  │
│                                                             │
│  Steam (Bazzite) · Distrobox · Opera (optional bake)        │
│  Home layer: Cursor AppImage · Stability Matrix Flatpak     │
└─────────────────────────────────────────────────────────────┘
```

## Base image choice

| Image | Driver | Use? |
| --- | --- | --- |
| `bazzite-nvidia-open` | NVIDIA **open** kernel modules (Turing+) | **YES** — RTX 5070 Ti Blackwell |
| `bazzite-nvidia` | Proprietary / legacy closed modules | **NO** — Blackwell refuses closed modules |
| `bazzite` (Mesa) | AMD/Intel | No — this box is NVIDIA |

NVIDIA documents that Blackwell (50-series) **requires** open GPU kernel modules.
Bazzite FAQ: `-nvidia-open` = Turing and newer (all RTX); legacy `-nvidia` =
Pascal/Maxwell/Volta only.

## What is baked vs home-layer

| Component | Layer | Notes |
| --- | --- | --- |
| NVIDIA open kmods + userspace | Base image | Already in `bazzite-nvidia-open` |
| Steam / Lutris / gaming stack | Base image | Bazzite desktop |
| Distrobox + Podman | Base image | Preinstalled on Bazzite |
| Tailscale | Image (enable unit) | Present on uBlue; we ensure `tailscaled` enabled |
| .NET 8 runtime | Image (`dnf5`) | Runs SoulCore.Host |
| Ollama | Image (official binary) | systemd `ollama.service` |
| Hermes agent | Image scaffolding | Unit + `/usr/libexec/soulcoreos/hermes-bootstrap`; app under `/var/lib/hermes` |
| Opera | Image **if** RPM resolves | Optional; skip cleanly if repo fails |
| SoulCore.Host binary | Deploy path `/opt/soulcore/host` (mutable) or layered publish | Unit ships in image; binary not compiled into Containerfile |
| p4d | Unit stub only | Disabled until later ticket |
| Cursor | Home / AppImage | Not baked |
| Stability Matrix | Flatpak / AppImage | Not baked |
| Unreal Engine | **Never** on this OS | Body on Shadow `house-victoria` |

## Networking

- SoulCore.Host: bind `127.0.0.1:7700` (SEC-004); remote clients via Tailscale
  serve / funnel per existing companion runbook.
- Unreal bridge: Host → `ws://house-victoria:8888` (Tailscale MagicDNS or hosts).
- Do not open Kestrel to LAN without SEC ticket.

## Dual-boot Phase 1

Phase 1 is **additive**: install SoulCoreOS to a free disk / partition alongside
an existing Windows (or other) install. Documented in `README.md`. Wipe / full
disk install is **not** mandatory for Phase 1 acceptance.

## Image lifecycle

1. Edit `image/Containerfile` + `image/build_files/build.sh`
2. Build → push GHCR (`ghcr.io/<org>/soulcoreos:latest`)
3. On machine: `sudo bootc switch …` (or signed `rpm-ostree rebase`)
4. Reboot; verify `nvidia-smi`, Tailscale, Host health
5. Rollback: GRUB previous deployment or `bootc rollback` / `rpm-ostree rollback`
