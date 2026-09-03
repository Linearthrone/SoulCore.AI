---
type: role
id: OPS-TAB
role: Tablet gateway (Tasker HTTP primary — Termux optional)
project: House Victoria
version: 1.3
updated: 2026-09-02
---
 
# OPS-TAB · Tablet gateway (Tasker HTTP · Termux optional)

**Native Termux cannot run Cursor `agent`** (glibc Node → `unexpected e_type: 2`).
My Machines on the tab is **deferred**. This role guides humans / home-pc agents
that prepare the SMS → Host bridge.

Canonical SMS contract: `docs/runbooks/sms-gateway-inbound.md`  
My Machines: `docs/runbooks/cursor-my-machines.md`

## Kill #1 — Pass (2026-09-02)

Real SMS from Kurt’s phone → Tab MDN → ChatDesktop + Victoria reply verified.

 b**Working recipe:** Tasker task = **HTTP Request only** (HTTPS `:8443`), Body `{"fromE164":"%SMSRF","text":"%SMSRB"}`, Var `%SOULCORE_TOKEN`.  
No Send Intent. No JavaScriptlet for normal texts. See runbook **§A**.

**Fallbacks:** Termux smoke (**§B**); Intent + `sms-ping.sh` (**§C**) — only if HTTP is blocked.

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
