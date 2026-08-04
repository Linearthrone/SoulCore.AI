# KayleighPlayer — C++ drop-in character (BED-172)

`AKayleighPlayerCharacter` is a grounded `ACharacter` with eye-level camera, WASD movement,
and a V1 prox-talk audio path (mic capture start/stop + spatial `ProxVoice` placeholder).
No Blueprint Event Graph wiring is required for walk or hold-to-talk once the class is compiled
and `BP_KayleighCharacter` is parented to it.

## Files

| File | Purpose |
| --- | --- |
| `KayleighPlayerCharacter.h` / `.cpp` | Character implementation |
| `KayleighPlayer.Build.cs` | Module build rules |

## Option A — Merge into `Plugins/HouseVictoriaBridge`

1. Copy `KayleighPlayerCharacter.h` and `KayleighPlayerCharacter.cpp` into
   `Plugins/HouseVictoriaBridge/Source/HouseVictoriaBridge/`.
2. In `HouseVictoriaBridge.Build.cs`, add to `PublicDependencyModuleNames`:
   `EnhancedInput`, `AudioCapture`; add `AudioMixer` to `PrivateDependencyModuleNames` if needed.
3. Change the class API macro from `KAYLEIGHPLAYER_API` to `HOUSEVICTORIABRIDGE_API` in the header.
4. Rebuild the editor target (Live Coding may not pick up new classes — prefer a full rebuild).
5. Parent `/Game/Characters/BP_KayleighCharacter` to
   `/Script/HouseVictoriaBridge.KayleighPlayerCharacter`.

## Option B — Second module inside the same plugin

1. Create `Plugins/HouseVictoriaBridge/Source/KayleighPlayer/` and copy all four files here.
2. Add `"KayleighPlayer"` to the `Modules` array in `HouseVictoriaBridge.uplugin`:

   ```json
   {
     "Name": "KayleighPlayer",
     "Type": "Runtime",
     "LoadingPhase": "Default"
   }
   ```

3. Regenerate project files and rebuild `MyProjectEditor` (Win64, UE 5.8).
4. Parent `BP_KayleighCharacter` to `/Script/KayleighPlayer.KayleighPlayerCharacter`.

## Option C — Standalone plugin folder

Copy `plugin-src/KayleighPlayer/` to `Plugins/KayleighPlayer/` with a minimal `.uplugin` that
lists the `KayleighPlayer` runtime module. Enable the plugin in the editor, rebuild, then parent
the Blueprint to `/Script/KayleighPlayer.KayleighPlayerCharacter`.

## After rebuild

1. Open `BP_KayleighCharacter` → Class Settings → parent class → `KayleighPlayerCharacter`.
2. Assign mesh / AnimBP / MetaHuman assets on the Blueprint (Python scripts under
   `House/UnrealBridge/Content/Python/` can automate this).
3. On the CDO or Blueprint defaults, set Enhanced Input assets if used:
   - `DefaultMappingContext` → `/Game/Input/IMC_Kayleigh`
   - `MoveAction` → `IA_Kayleigh_Move` (Vector2D)
   - `LookAction` → `IA_Kayleigh_Look` (Vector2D)
   - `ProxTalkAction` → `IA_Kayleigh_ProxTalk` (Bool, hold)
4. `GM_HouseVictoria` should set `DefaultPawnClass` to `BP_KayleighCharacter`
   (`AutoPossessPlayer` stays Disabled on the C++ class; GameMode possesses the pawn).

## Live Coding / rebuild notes

- New `UCLASS` types and new modules require a **full editor rebuild**, not Live Coding alone.
- After copying sources, run `build_bridge.ps1` or UE `Build.bat MyProjectEditor Win64 Development`.
- Confirm the class loads: Output Log should show no `KayleighPlayerCharacter` link errors;
  `create_bp_kayleigh_character.py` logs which parent class it selected.

## Prox-talk V1 scope

C++ starts/stops `UAudioCaptureComponent` and activates/deactivates spatialized `ProxVoice`.
Routing captured audio into `ProxVoice` for true proximity voice may need additional
`VoiceModule` / EOS / OnlineSubsystem integration — out of scope for this drop-in.
