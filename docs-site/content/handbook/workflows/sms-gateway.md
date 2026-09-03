# SMS / MMS gateway workflow

## Current (temporary) path

```text
Kurt phone --SMS--> Tab MDN
                      |
                   Tasker (Received Text) --HTTP POST--> Host /messages/inbound
                      |
                   Host chat (no tools) --> Presence WS --> ChatDesktop
                      |
                   Host enqueues reply SMS (+ MMS on screenshot ask)
                      |
                   Termux sms-outbound-poll.sh --loop --> termux-sms-send / still file
```

## Direction

**Tasker + Termux:API are temporary.** Product goal: one House-owned gateway on the tablet that receives SMS, talks to Host, and sends replies without paid automation or manual Play.

## Ops pointers

Full procedure: `docs/runbooks/sms-gateway-inbound.md`  
Tablet role: `Agents/OPS-TAB.md`

## Rate limits (Host defaults)

| Kind | Min gap | Max / hour |
| --- | --- | --- |
| SMS | 12 s | 30 |
| MMS | 60 s | 6 |
