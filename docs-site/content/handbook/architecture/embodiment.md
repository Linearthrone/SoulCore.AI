# Unreal embodiment

## Split

| Machine | Role |
| --- | --- |
| **soul@main** (home PC) | Host, ChatDesktop, inference, memory |
| **body@shadow** | Unreal Editor / built game — Kayleigh / Victoria pawn |

REX owns shadow Play. Host talks to Unreal via `UnrealBridge` options + verb client (stub/null when disabled).

## Active work (PROP-2)

- Possess Kayleigh 1P (not DefaultPawn)
- Measured locomotion / AnimBP
- One Presence eye still
- Call camera held until possess Pass

## Docs

- Runbook: `docs/runbooks/kayleigh-player-pawn-setup.md`
- Roles: `Agents/REX-01.md`, `Agents/REX-01-SHADOW.md`
- Tools: `tools/ue_nav/` (PIE/nav helpers — keep scripts, not log dumps)
