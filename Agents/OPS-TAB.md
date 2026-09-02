---
type: role
id: OPS-TAB
role: Tablet gateway (Tasker HTTP primary — Termux optional)
project: House Victoria
version: 1.4
updated: 2026-09-02
---

# OPS-TAB · Tablet gateway (Tasker HTTP · Termux optional)

**Native Termux cannot run Cursor `agent`** (glibc Node → `unexpected e_type: 2`).
My Machines on the tab is **deferred**. This role guides humans / home-pc agents
that prepare the SMS ↔ Host bridge.

Canonical SMS contract: `docs/runbooks/sms-gateway-inbound.md`  
My Machines: `docs/runbooks/cursor-my-machines.md`

## Kill #1 — Pass (2026-09-02)

Real SMS from Kurt’s phone → Tab MDN → ChatDesktop + Victoria reply verified.

**Working recipe:** Tasker **HTTP Request only** (HTTPS `:8443`), Body `{"fromE164":"%SMSRF","text":"%SMSRB"}`, Var `%SOULCORE_TOKEN`.  
No Send Intent. No JavaScriptlet for normal texts.

## PROP-1.3 outbound (ops)

Host enqueues auto-reply SMS + screenshot MMS. Drain with:

```bash
~/bin/sms-outbound-poll.sh --loop 10
```

Or Tasker **Send SMS** from inbound `replyText` — **not both**.

## Scope

- Maintain Tasker HTTP + Termux scripts (`sms-to-victoria.sh`, `sms-ping.sh`, `sms-outbound-poll.sh`)
- Tailscale reachability notes (`:7700` / `:8443`)
- Never put companion tokens in git or agent prompts

## Out of scope

- Expecting `agent worker start` inside plain Termux
- Editing Host C# on Windows → `home-pc` / `@Agents/OPS-HOME.md`

## Activate

Prefer machine **`home-pc`** with “restart Host + start outbound poller”.
