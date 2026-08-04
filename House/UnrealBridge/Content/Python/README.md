# Kayleigh player pawn — UE 5.8 Python automation (BED-172)

Scripts in this folder automate **grounded `BP_KayleighCharacter`** setup on the Shadow PC
MyProject (UE 5.8). They mirror Victoria BED-114/115 patterns: separate `ACharacter` Blueprint,
MetaHuman body mesh from `MHC_Kayleigh`, tag `KayleighPlayer` — **do not reparent** `BP_MHC_*`.

There is **no Unreal Editor in the Linux SoulCore workspace**; these files are authored here and
executed on Windows via `UnrealEditor-Cmd`.

## Copy to MyProject

From the SoulCore repo (this folder):

```text
House/UnrealBridge/Content/Python/*.py
House/UnrealBridge/Content/Python/run_task172.ps1
```

Copy to:

```text
C:\HouseVictoriaUE5.8\MyProject\Content\Python\
```

(or your synced MyProject `Content/Python/`)

`run_task172.ps1` can auto-copy from a sibling SoulCore checkout when paths align.

## Run on Shadow PC

Prerequisites:

- UE **5.8** with `PythonScriptPlugin` and `EditorScriptingUtilities` enabled in `MyProject.uproject`
- `MHC_Kayleigh` body mesh synced under `/Game/MetaHumans/`
- Victoria `BP_VictoriaCharacter` + `VictoriaAvatar` on `/Game/Home` (unchanged)
- Close interactive Unreal Editor before headless Cmd runs

### One-shot (recommended)

```powershell
cd C:\HouseVictoriaUE5.8\MyProject\Content\Python
$env:MYPROJECT_ROOT = "C:\HouseVictoriaUE5.8\MyProject"   # optional override
.\run_task172.ps1
```

### Individual scripts

```powershell
$Engine = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
$Proj   = "C:\HouseVictoriaUE5.8\MyProject\MyProject.uproject"
$Py     = "C:\HouseVictoriaUE5.8\MyProject\Content\Python"

& $Engine $Proj /Game/Home -ExecutePythonScript="$Py/create_bp_kayleigh_character.py" -unattended -nosplash
& $Engine $Proj /Game/Home -ExecutePythonScript="$Py/setup_kayleigh_gamemode.py" -unattended -nosplash
& $Engine $Proj /Game/Home -ExecutePythonScript="$Py/setup_kayleigh_prox_audio.py" -unattended -nosplash
& $Engine $Proj /Game/Home -ExecutePythonScript="$Py/verify_kayleigh_player.py" -unattended -nosplash
```

Logs: `MyProject/Saved/Logs/create_bp_kayleigh_character.log`, etc.

## Script order

| Script | Purpose |
| --- | --- |
| `create_bp_kayleigh_character.py` | `BP_KayleighCharacter`, capsule/mesh/CharMove, camera, tag, Home placement |
| `setup_kayleigh_gamemode.py` | `GM_HouseVictoria`, `DefaultPawnClass`, Home GameMode override (best effort) |
| `setup_kayleigh_prox_audio.py` | `AudioCapture` + `ProxVoice`, attenuation stubs, Input manual steps |
| `verify_kayleigh_player.py` | Headless PASS/FAIL checklist |
| `run_task172.ps1` | Runs all four in order |

## Expected assets

| Asset | Path |
| --- | --- |
| Character | `/Game/Characters/BP_KayleighCharacter` |
| GameMode | `/Game/Characters/GM_HouseVictoria` |
| Loco (reuse) | `/Game/Animations/Victoria/Locomotion/ABP_Victoria_Locomotion` |
| Post-process | `/Game/MetaHumans/Common/Body/ABP_Body_PostProcess` |
| Kayleigh mesh | probe under `/Game/MetaHumans/MHC_Kayleigh/...` |
| Map | `/Game/Home` |

## Manual follow-ups

1. **GameMode** — If Cmd cannot set World Settings, open Home → World Settings → GameMode Override → `GM_HouseVictoria`.
2. **Enhanced Input** — Wire `IMC_Kayleigh` / hold **V** for prox talk (scripts print `MANUAL_INPUT` steps).
3. **PIE** — Confirm possessed Kayleigh (not flying `DefaultPawn`), eye camera, walk anims, Victoria bridge still finds `VictoriaAvatar`.

## Runbook

Full product notes: `docs/runbooks/kayleigh-player-pawn-setup.md`
