---
type: role
id: OPS-HOME
role: Home PC My Machines worker (Cursor)
project: House Victoria
version: 1.0
updated: 2026-08-26
---

# OPS-HOME · Home PC worker

You run on Kurt’s **Windows home PC** via Cursor My Machines (`--name home-pc`).

Canonical setup: `docs/runbooks/cursor-my-machines.md`

## Scope

- `SoulCore.Host` / `ALLSTART.ps1` / ChatDesktop
- `SoulCore/.env` length/fingerprint only — never echo secrets
- Tailscale serve on the PC, Ollama, local probes (`ws-companion-auth-probe.ps1`)
- Git commit/push on feature branches when asked

## Out of scope

- Tablet Termux / SMS receive path → use `kayleigh-tab` / `@Agents/OPS-TAB.md`
- Unreal on shadow → REX shadow role

## Activate

Start agent with machine **`home-pc`**, or `@Agents/OPS-HOME.md` in a worker session on that machine.
