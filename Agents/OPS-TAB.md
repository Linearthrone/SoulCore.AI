---
type: role
id: OPS-TAB
role: Tablet gateway (Tasker HTTP primary — Termux optional)
project: House Victoria
version: 1.2
updated: 2026-08-29
---

# OPS-TAB · Tablet gateway (Tasker HTTP · Termux optional)

**Native Termux cannot run Cursor `agent`** (glibc Node → `unexpected e_type: 2`).
My Machines on the tab is **deferred**. This role guides humans / home-pc agents
that prepare the SMS → Host bridge.

Canonical SMS contract: `docs/runbooks/sms-gateway-inbound.md`  
My Machines: `docs/runbooks/cursor-my-machines.md`

## Kill #1 (now)

**Done when:** real SMS from Kurt’s allowlisted phone → Tab MDN appears in ChatDesktop + Victoria replies.

**Primary path (preferred):** Tasker **HTTP Request** + JavaScriptlet JSON body — **no Termux:Tasker plugin**.  
Use **HTTPS** Tailscale URL (`…:8443`), not cleartext `:7700` (Android often blocks HTTP).  
Dry-run uses `%sc_from` / `%sc_text` Variable Set — **not** Vars `%SMSRF`. See runbook **§A**.

**Fallbacks:** Termux `sms-to-victoria.sh` smoke (**§B**); Tasker **Send Intent** → `com.termux.RUN_COMMAND` with `sms-ping.sh` first (**§C**).

## Scope

- Maintain Tasker HTTP steps + Termux scripts (`sms-to-victoria.sh`, `sms-ping.sh`)
- Tailscale reachability notes (`:7700` / `:8443`)
- Never put companion tokens in git or agent prompts
- Debug Intent with `~/sms-forward.log` before blaming Host

## Out of scope

- Expecting `agent worker start` inside plain Termux
- Editing Host C# on Windows → `home-pc` / `@Agents/OPS-HOME.md`
- PROP-1.3 outbound SMS (BED) until inbound #1 Pass

## Activate

Prefer machine **`home-pc`** with “verify Host inbound + walk Kurt through Tasker §A”.
Optional later: proot-distro Ubuntu + `kayleigh-tab` worker (see My Machines runbook).
