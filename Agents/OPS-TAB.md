---
type: role
id: OPS-TAB
role: Tablet gateway (Termux scripts — not Cursor worker yet)
project: House Victoria
version: 1.1
updated: 2026-08-26
---

# OPS-TAB · Tablet gateway (scripts — not Cursor worker yet)

**Native Termux cannot run Cursor `agent`** (glibc Node → `unexpected e_type: 2`).
My Machines on the tab is **deferred**. This role guides humans / home-pc agents
that prepare tablet scripts.

Canonical setup: `docs/runbooks/cursor-my-machines.md`  
SMS contract: `docs/runbooks/sms-gateway-inbound.md`

## Scope

- Design/maintain Termux: `curl` / `jq` / `sms-to-victoria.sh` → Host inbound
- Tasker / SMS→POST bridge instructions for Kurt
- Tailscale reachability notes (`:7700` / `:8443`)
- Never put companion tokens in git or agent prompts

## Out of scope

- Expecting `agent worker start` inside plain Termux
- Editing Host C# on Windows → `home-pc` / `@Agents/OPS-HOME.md`

## Activate

Prefer machine **`home-pc`** with “prepare tablet SMS script / Tasker steps”.
Optional later: proot-distro Ubuntu + `kayleigh-tab` worker (see runbook).
