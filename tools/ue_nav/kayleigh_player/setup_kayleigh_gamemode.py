# BED-172 — Create GM_HouseVictoria with DefaultPawnClass = BP_KayleighCharacter.
# Attempts Home World Settings GameMode Override; documents manual fallback.

import os
import traceback
import unreal

GM_PATH = "/Game/Characters/GM_HouseVictoria"
GM_PACKAGE = "/Game/Characters"
GM_NAME = "GM_HouseVictoria"
BP_KAYLEIGH_PATH = "/Game/Characters/BP_KayleighCharacter"
HOME_MAP = "/Game/Home"
LOG_BASENAME = "setup_kayleigh_gamemode"


def _log(msg: str) -> None:
    line = f"[{LOG_BASENAME}] {msg}"
    unreal.log(line)
    print(line)


def _log_file(msg: str) -> None:
    _log(msg)
    try:
        project_dir = unreal.Paths.project_dir()
        log_path = os.path.join(project_dir, "Saved", "Logs", f"{LOG_BASENAME}.log")
        with open(log_path, "a", encoding="utf-8") as fh:
            fh.write(msg + "\n")
    except Exception:
        pass


def _asset_exists(path: str) -> bool:
    try:
        return bool(unreal.EditorAssetLibrary.does_asset_exist(path))
    except Exception:
        return False


def _load_generated_class(path: str):
    if not _asset_exists(path):
        return None
    asset = unreal.EditorAssetLibrary.load_asset(path)
    if asset is None:
        return None
    return asset.generated_class()


def _create_or_load_gamemode_blueprint():
    if _asset_exists(GM_PATH):
        gm_bp = unreal.EditorAssetLibrary.load_asset(GM_PATH)
        _log(f"Reusing existing GameMode Blueprint: {GM_PATH}")
        return gm_bp

    factory = unreal.BlueprintFactory()
    factory.set_editor_property("parent_class", unreal.GameModeBase)
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    with unreal.ScopedEditorTransaction("Create GM_HouseVictoria"):
        gm_bp = asset_tools.create_asset(GM_NAME, GM_PACKAGE, unreal.Blueprint, factory)
    if gm_bp is None:
        raise RuntimeError(f"Failed to create GameMode at {GM_PATH}")
    _log(f"Created GameMode Blueprint: {GM_PATH}")
    return gm_bp


def _pawn_name_forbidden(name: str) -> bool:
    n = (name or "").lower()
    return any(
        tok in n
        for tok in ("victoria", "victoriaavatar", "bp_victoriacharacter", "bp_mhc_victoria")
    )


def _set_default_pawn_on_cdo(gm_blueprint, pawn_class) -> bool:
    generated = gm_blueprint.generated_class()
    if generated is None:
        _log("FAIL: GameMode blueprint not compiled")
        return False

    proposed = str(pawn_class.get_name()) if pawn_class is not None else ""
    if _pawn_name_forbidden(proposed):
        _log(
            f"FAIL HARD: refused to set DefaultPawnClass to Victoria-related class '{proposed}'. "
            "Player must be BP_KayleighCharacter."
        )
        return False
    if "kayleigh" not in proposed.lower():
        _log(
            f"FAIL HARD: refused DefaultPawnClass '{proposed}' — name must contain Kayleigh."
        )
        return False

    cdo = unreal.get_default_object(generated)
    try:
        cdo.set_editor_property("default_pawn_class", pawn_class)
        actual = cdo.get_editor_property("default_pawn_class")
        if actual is None:
            _log("FAIL: default_pawn_class is None after set")
            return False
        actual_name = actual.get_name()
        if _pawn_name_forbidden(actual_name):
            _log(f"FAIL HARD: after set, DefaultPawnClass is still Victoria-related: {actual_name}")
            return False
        if "kayleigh" not in actual_name.lower():
            _log(f"FAIL HARD: after set, DefaultPawnClass lacks Kayleigh: {actual_name}")
            return False
        _log(f"DefaultPawnClass set on CDO: {actual_name}")
        return True
    except Exception as exc:
        _log(f"FAIL default_pawn_class: {exc}")
        return False


def _try_set_home_world_gamemode_override(gm_class) -> bool:
    """Set GameMode Override on Home via WorldSettings actor."""
    manual_note = (
        "MANUAL: Open /Game/Home → World Settings → GameMode Override → GM_HouseVictoria "
        "(or set in DefaultEngine.ini [/Script/EngineSettings.GameMapsSettings] only with OPS approval)"
    )

    try:
        unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)
    except Exception as exc:
        _log(f"WARN load_map: {exc}")

    world = None
    try:
        world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    except Exception:
        try:
            world = unreal.EditorLevelLibrary.get_editor_world()
        except Exception as exc:
            _log(f"WARN get_editor_world: {exc}")

    if world is None:
        _log(f"WARN: No editor world — {manual_note}")
        return False

    # Approach 1: iterate level actors for WorldSettings
    try:
        actors = list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors())
    except Exception:
        actors = list(unreal.EditorLevelLibrary.get_all_level_actors())

    for actor in actors:
        try:
            if not isinstance(actor, unreal.WorldSettings):
                continue
            actor.set_editor_property("default_game_mode", gm_class)
            actual = actor.get_editor_property("default_game_mode")
            _log(f"Home WorldSettings default_game_mode set: {actual.get_name() if actual else None}")
            try:
                unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
            except Exception:
                unreal.EditorLevelLibrary.save_current_level()
            return True
        except Exception as exc:
            _log(f"WARN WorldSettings property set: {exc}")

    # Approach 2: EditorLevelLibrary helpers (UE version dependent)
    for fn_name in ("set_level_game_mode", "set_game_mode"):
        fn = getattr(unreal.EditorLevelLibrary, fn_name, None)
        if fn is None:
            continue
        try:
            fn(gm_class)
            _log(f"Home GameMode set via EditorLevelLibrary.{fn_name}")
            return True
        except Exception as exc:
            _log(f"WARN EditorLevelLibrary.{fn_name}: {exc}")

    _log(f"WARN: Could not set Home World Settings GameMode Override automatically.")
    _log(manual_note)
    return False


def main() -> bool:
    _log("=== setup_kayleigh_gamemode START ===")
    ok = True
    world_override = False

    try:
        if not _asset_exists(BP_KAYLEIGH_PATH):
            _log(f"FAIL: {BP_KAYLEIGH_PATH} missing — run create_bp_kayleigh_character.py first")
            return False

        pawn_class = _load_generated_class(BP_KAYLEIGH_PATH)
        if pawn_class is None:
            _log("FAIL: Could not load BP_KayleighCharacter generated class")
            return False

        gm_bp = _create_or_load_gamemode_blueprint()
        if not _set_default_pawn_on_cdo(gm_bp, pawn_class):
            ok = False

        unreal.BlueprintEditorLibrary.compile_blueprint(gm_bp)
        unreal.EditorAssetLibrary.save_loaded_asset(gm_bp)

        gm_class = gm_bp.generated_class()
        if gm_class is None:
            _log("FAIL: GameMode generated_class is None")
            ok = False
        else:
            world_override = _try_set_home_world_gamemode_override(gm_class)

        try:
            unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
        except Exception as exc:
            _log(f"WARN save: {exc}")

        if not world_override:
            _log(
                "NOTE: PIE on Home still needs World Settings → GameMode Override = GM_HouseVictoria "
                "if automatic override failed."
            )

    except Exception as exc:
        _log(f"FATAL: {exc}")
        _log(traceback.format_exc())
        ok = False

    result = "PASS" if ok else "FAIL"
    _log_file(f"RESULT: {result} (world_override={'yes' if world_override else 'manual'})")
    _log(f"=== setup_kayleigh_gamemode END — {result} ===")
    return ok


if __name__ == "__main__":
    main()
else:
    main()
