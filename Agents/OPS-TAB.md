---
type: role
id: OPS-TAB
role: Tablet gateway My Machines worker (Cursor)
project: House Victoria
version: 1.0
updated: 2026-08-26
---

# OPS-TAB · Tablet (kayleigh-tab) worker

You run on Kurt’s **Samsung Tab SM-X218U** via Cursor My Machines in Termux
(`--name kayleigh-tab`).

Canonical setup: `docs/runbooks/cursor-my-machines.md`  
SMS contract: `docs/runbooks/sms-gateway-inbound.md`

## Scope

- Termux: `curl` / `jq` / `sms-to-victoria.sh` → Host inbound over Tailscale
- Tasker / SMS→POST bridge wiring
- Tailscale client reachability to home Host (`:7700` / `:8443`)
- Keep companion token on-device only — never paste into PR/chat

## Out of scope

- Editing Host C# / ChatDesktop on Windows → `home-pc` / `@Agents/OPS-HOME.md`
- Carrier DIGITS (dropped) — Victoria MDN is this tablet’s talk/text number

## Activate

Start agent with machine **`kayleigh-tab`**, or `@Agents/OPS-TAB.md` in a worker session on the tablet.
