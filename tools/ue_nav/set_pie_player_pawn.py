"""
BED-184 — PIE starts as Kurt's grounded avatar, not the flying DefaultPawn ghost.

Run inside UE 5.8 Editor (Output Log → Python, or File → Execute Python Script)
with /Game/Home open.

What it does:
1) Finds Character actors that are NOT tagged VictoriaAvatar (your body on the floor).
2) Ensures a PlayerStart near that body.
3) Creates/updates /Game/Blueprints/BP_HouseGameMode with DefaultPawnClass =
   that Character's class (so PIE possesses a grounded body).
4) Sets the current world's GameMode Override to BP_HouseGameMode.

It does NOT possess BP_VictoriaCharacter (she stays AI-controlled).

Manual fallback (Editor UI):
  World Settings → GameMode Override → BP_HouseGameMode
  (or any GameMode whose Default Pawn Class is your Kayleigh/player Character BP)
  Place/move PlayerStart onto your grounded avatar.
"""
from __future__ import annotations

import unreal

VICTORIA_TAG = "VictoriaAvatar"
PLAYER_TAGS = ("PlayerAvatar", "Kayleigh", "Kurt", "Player")
GAME_MODE_PATH = "/Game/Blueprints/BP_HouseGameMode"
LOG_PREFIX = "[set_pie_player_pawn]"


def log(msg: str) -> None:
    line = f"{LOG_PREFIX} {msg}"
    unreal.log(line)
    print(line)


def actor_label(actor) -> str:
    try:
        return actor.get_actor_label()
    except Exception:
        return str(actor)


def has_any_tag(actor, tags) -> bool:
    try:
        for t in actor.tags:
            if str(t) in tags:
                return True
    except Exception:
        pass
    name = actor_label(actor).lower()
    class_name = str(actor.get_class().get_name()).lower()
    for t in tags:
        if t.lower() in name or t.lower() in class_name:
            return True
    return False


def is_victoria(actor) -> bool:
    return has_any_tag(actor, (VICTORIA_TAG, "Victoria"))


def find_player_character_candidates():
    """Prefer tagged/named player Characters; fall back to non-Victoria Characters."""
    subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    actors = subsystem.get_all_level_actors()
    characters = []
    for a in actors:
        try:
            if not isinstance(a, unreal.Character):
                # Some BP Characters report as Pawn
                if not isinstance(a, unreal.Pawn):
                    continue
        except Exception:
            continue
        if is_victoria(a):
            continue
        characters.append(a)

    preferred = [c for c in characters if has_any_tag(c, PLAYER_TAGS)]
    return preferred or characters


def ensure_player_start_near(actor) -> None:
    loc = actor.get_actor_location()
    subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    existing = [
        a for a in subsystem.get_all_level_actors()
        if isinstance(a, unreal.PlayerStart)
    ]
    if existing:
        ps = existing[0]
        ps.set_actor_location(loc, False, True)
        log(f"Moved existing PlayerStart to {loc}")
        return

    ps = unreal.EditorLevelLibrary.spawn_actor_from_class(
        unreal.PlayerStart, loc, unreal.Rotator(0, 0, 0)
    )
    if ps:
        log(f"Spawned PlayerStart at {loc}")
    else:
        log("WARN: could not spawn PlayerStart — place one manually near your avatar")


def load_or_create_game_mode(pawn_class):
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    asset_path = GAME_MODE_PATH
    existing = unreal.EditorAssetLibrary.load_asset(asset_path)
    if existing:
        log(f"Loaded existing {asset_path}")
        gm = existing
    else:
        factory = unreal.BlueprintFactory()
        factory.set_editor_property("parent_class", unreal.GameModeBase)
        folder = "/Game/Blueprints"
        if not unreal.EditorAssetLibrary.does_directory_exist(folder):
            unreal.EditorAssetLibrary.make_directory(folder)
        gm = asset_tools.create_asset(
            "BP_HouseGameMode",
            folder,
            unreal.Blueprint,
            factory,
        )
        if not gm:
            raise RuntimeError("Failed to create BP_HouseGameMode")
        log(f"Created {asset_path}")

    # Set Default Pawn Class on the generated class CDO when possible.
    try:
        generated = gm.generated_class()
        cdo = unreal.get_default_object(generated)
        cdo.set_editor_property("default_pawn_class", pawn_class)
        log(f"Set DefaultPawnClass → {pawn_class.get_name()}")
    except Exception as ex:
        log(
            f"WARN: could not set DefaultPawnClass via Python ({ex}). "
            f"Open {asset_path} and set Default Pawn Class to your player Character BP."
        )

    unreal.EditorAssetLibrary.save_asset(asset_path)
    return gm


def set_world_game_mode(gm_asset) -> None:
    world = unreal.EditorLevelLibrary.get_editor_world()
    settings = world.get_world_settings()
    try:
        generated = gm_asset.generated_class()
        settings.set_editor_property("default_game_mode", generated)
        log("World Settings GameMode Override → BP_HouseGameMode")
    except Exception as ex:
        log(f"WARN: set GameMode Override manually — {ex}")


def main():
    log("Scanning /Game/Home for grounded player avatar (non-Victoria Character)…")
    candidates = find_player_character_candidates()
    if not candidates:
        log(
            "FAIL: no non-Victoria Character/Pawn found. "
            "Place your Kayleigh/player Character in Home (not VictoriaAvatar), "
            "tag it PlayerAvatar, then re-run."
        )
        return

    for i, c in enumerate(candidates):
        log(f"  candidate[{i}] label={actor_label(c)} class={c.get_class().get_name()} loc={c.get_actor_location()}")

    chosen = candidates[0]
    log(f"Using {actor_label(chosen)} as PIE player body class seed")
    ensure_player_start_near(chosen)
    gm = load_or_create_game_mode(chosen.get_class())
    set_world_game_mode(gm)
    unreal.EditorLevelLibrary.save_current_level()
    log(
        "DONE. Press Play (PIE). You should spawn as a grounded Character, "
        "not the flying ghost. Victoria remains AI-possessed separately. "
        "If you still float: World Settings → GameMode Override + Default Pawn Class."
    )


if __name__ == "__main__":
    main()
