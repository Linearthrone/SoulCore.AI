# Kayleigh player PIE pipeline (REX-01)

End-to-end Editor Python for **possessing Kayleigh in PIE**, never Victoria.

## Run (Windows)

```powershell
cd C:\Users\kurtw\Soul_Core
.\tools\ue_nav\run_rex_pie_possess_kayleigh.ps1
```

Or in UE Output Log / Execute Python Script:

`tools/ue_nav/kayleigh_player/rex_pie_possess_kayleigh.py`

## Scripts

| File | Role |
| --- | --- |
| `rex_pie_possess_kayleigh.py` | Orchestrator + hard DefaultPawn assert |
| `create_bp_kayleigh_character.py` | `/Game/Characters/BP_KayleighCharacter` |
| `setup_kayleigh_gamemode.py` | `GM_HouseVictoria` DefaultPawn = Kayleigh only |
| `verify_kayleigh_player.py` | Asset / tag / GameMode checks |

## Hard rules

- Player DefaultPawn must contain **Kayleigh**
- Refuse any Victoria* DefaultPawn
- Do not reparent `BP_MHC_Kayleigh`

Seat: `Agents/REX-01.md` · Ticket: TASK-191 · Runbook: `docs/runbooks/kayleigh-player-pawn-setup.md`
