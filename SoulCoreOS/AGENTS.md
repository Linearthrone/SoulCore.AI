# SoulCoreOS — Agent Notes

SoulCoreOS is the **bootc / Bazzite** host OS for the House Victoria soul-machine
stack (GPU workstation). Sister product: **SoulCore.AI** (.NET 8) under
`../SoulCore/` in the monorepo.

## Role routing

| Role | Path | Owns |
| --- | --- | --- |
| PM-01 | `Agents/PM-01.md` | Priority, tickets, acceptance |
| OPS-01 | `Agents/OPS-01.md` | Image build, deploy, `bootc` / GHCR, systemd |
| BED-01 | `Agents/BED-01.md` | SoulCore.Host / protocol (sister app) |
| QA-01 | `Agents/QA-01.md` | Gates after image boots |
| SEC-01 | `Agents/SEC-01.md` | Bind policy, Tailscale, secrets |
| TT-01 | `Agents/TT-01.md` | Architecture / OS proposals |
| FED-01 | `Agents/FED-01.md` | Desktop / companion UI (not UE) |
| DBD-01 | `Agents/DBD-01.md` | SQLite / memory schema |
| SLOP-01 | `Agents/SLOP-01.md` | Cleanup / hygiene |

Canonical role handbooks may live in the monorepo root `Agents/` — stubs here
point at those files when present.

## Hard constraints (OPS)

1. Base image: **`ghcr.io/ublue-os/bazzite-nvidia-open`** (open NVIDIA modules).
   Do **not** use `bazzite-nvidia` (proprietary) — target GPU is RTX 5070 Ti
   Blackwell, which requires open kernel modules.
2. Do **not** bake Unreal Engine. Body stays on Shadow PC `house-victoria`
   (`ws://house-victoria:8888`).
3. Cursor + Stability Matrix stay on the **home layer** (AppImage / Flatpak),
   not the OS image, unless a later ticket explicitly asks to bake them.
4. `p4d` is Phase-later — ship a disabled unit stub only.

## Docs

- Architecture: `docs/architecture/SOULCOREOS.md`
- Product root (OS slice): `docs/agents/PRODUCT_ROOT.md`
- Build / switch / rollback: `README.md`
