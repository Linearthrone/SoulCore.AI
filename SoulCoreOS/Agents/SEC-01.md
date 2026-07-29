---
type: role
id: SEC-01
role: Security
project: SoulCoreOS / House Victoria
---

# SEC-01 · SoulCoreOS

Canonical handbook: monorepo [`../../Agents/SEC-01.md`](../../Agents/SEC-01.md).

Host bind remains loopback / Tailscale-only; no LAN expose of SoulCore without a
ticket. Secrets stay in `/etc/soulcore/` or user `.env` (gitignored), never in
the image.
