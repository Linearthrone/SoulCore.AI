# BED-172 / QA-173 prep — Headless verification checklist for Kayleigh player setup.

import os
import traceback
import unreal

BP_PATH = "/Game/Characters/BP_KayleighCharacter"
GM_PATH = "/Game/Characters/GM_HouseVictoria"
HOME_MAP = "/Game/Home"
TAG_KAYLEIGH = "KayleighPlayer"
TAG_VICTORIA = "VictoriaAvatar"

CAPSULE_RADIUS = 34.0
CAPSULE_HALF_HEIGHT = 96.0
TOLERANCE = 2.0

LOG_BASENAME = "verify_kayleigh_player"


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


def _check_bp_exists(checks: dict) -> None:
    checks["bp_exists"] = _asset_exists(BP_PATH)
    _log(f"CHECK bp_exists: {checks['bp_exists']} ({BP_PATH})")


def _check_parent_character(checks: dict) -> None:
    ok = False
    try:
        bp = unreal.EditorAssetLibrary.load_asset(BP_PATH)
        parent = unreal.BlueprintEditorLibrary.get_blueprint_parent_class(bp)
        if parent is not None:
            ok = parent == unreal.Character or parent.get_name() == "Character"
        if not ok:
            # Fallback: CDO has capsule + character movement (BED-114 reflection gotcha)
            gen = bp.generated_class()
            cdo = unreal.get_default_object(gen)
            has_cap = cdo.get_editor_property("capsule_component") is not None
            has_move = cdo.get_editor_property("character_movement") is not None
            ok = has_cap and has_move
    except Exception as exc:
        _log(f"WARN parent check: {exc}")
    checks["parent_character"] = ok
    _log(f"CHECK parent_character: {ok}")


def _check_tag_on_cdo(checks: dict) -> None:
    ok = False
    try:
        gen = _load_generated_class(BP_PATH)
        cdo = unreal.get_default_object(gen)
        tags = list(cdo.get_editor_property("tags") or [])
        ok = TAG_KAYLEIGH in tags and TAG_VICTORIA not in tags
        _log(f"  CDO tags: {tags}")
    except Exception as exc:
        _log(f"WARN tag check: {exc}")
    checks["tag_kayleigh_player"] = ok
    _log(f"CHECK tag_kayleigh_player (no VictoriaAvatar on CDO): {ok}")


def _check_capsule(checks: dict) -> None:
    ok = False
    try:
        gen = _load_generated_class(BP_PATH)
        cdo = unreal.get_default_object(gen)
        cap = cdo.get_editor_property("capsule_component")
        r = float(cap.get_editor_property("capsule_radius"))
        h = float(cap.get_editor_property("capsule_half_height"))
        ok = abs(r - CAPSULE_RADIUS) <= TOLERANCE and abs(h - CAPSULE_HALF_HEIGHT) <= TOLERANCE
        _log(f"  capsule radius={r} half_height={h}")
    except Exception as exc:
        _log(f"WARN capsule check: {exc}")
    checks["capsule_sized"] = ok
    _log(f"CHECK capsule_sized: {ok}")


def _check_camera_present(checks: dict) -> None:
    boom = False
    cam = False
    try:
        bp = unreal.EditorAssetLibrary.load_asset(BP_PATH)
        subsystem = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
        handles = subsystem.k2_gather_subobject_data_for_blueprint(bp)
        bfl = unreal.SubobjectDataBlueprintFunctionLibrary
        for handle in handles:
            data = subsystem.k2_find_subobject_data_from_handle(handle)
            obj = bfl.get_object(data)
            if obj is None:
                continue
            name = obj.get_name().lower()
            cls = obj.get_class().get_name()
            if "cameraboom" in name or "springarm" in cls.lower():
                boom = True
            if "followcamera" in name or cls == "CameraComponent":
                cam = True
    except Exception as exc:
        _log(f"WARN camera check: {exc}")
    checks["camera_boom"] = boom
    checks["follow_camera"] = cam
    _log(f"CHECK camera_boom: {boom}")
    _log(f"CHECK follow_camera: {cam}")


def _check_gamemode_default_pawn(checks: dict) -> None:
    ok = False
    try:
        if not _asset_exists(GM_PATH):
            checks["gamemode_exists"] = False
            checks["gamemode_default_pawn"] = False
            _log(f"CHECK gamemode_exists: False ({GM_PATH})")
            _log("CHECK gamemode_default_pawn: False")
            return

        checks["gamemode_exists"] = True
        _log(f"CHECK gamemode_exists: True")

        gm_gen = _load_generated_class(GM_PATH)
        pawn_gen = _load_generated_class(BP_PATH)
        cdo = unreal.get_default_object(gm_gen)
        default_pawn = cdo.get_editor_property("default_pawn_class")
        ok = default_pawn is not None and default_pawn == pawn_gen
        if default_pawn:
            _log(f"  GM DefaultPawnClass: {default_pawn.get_name()}")
    except Exception as exc:
        _log(f"WARN gamemode check: {exc}")
    checks["gamemode_default_pawn"] = ok
    _log(f"CHECK gamemode_default_pawn: {ok}")


def _check_prox_audio_components(checks: dict) -> None:
    capture = False
    voice = False
    try:
        bp = unreal.EditorAssetLibrary.load_asset(BP_PATH)
        subsystem = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
        handles = subsystem.k2_gather_subobject_data_for_blueprint(bp)
        bfl = unreal.SubobjectDataBlueprintFunctionLibrary
        for handle in handles:
            data = subsystem.k2_find_subobject_data_from_handle(handle)
            obj = bfl.get_object(data)
            if obj is None:
                continue
            name = obj.get_name().lower()
            cls = obj.get_class().get_name()
            if "audiocapture" in name or cls == "AudioCaptureComponent":
                capture = True
            if "proxvoice" in name:
                voice = True
    except Exception as exc:
        _log(f"WARN prox audio check: {exc}")
    checks["audio_capture"] = capture
    checks["prox_voice"] = voice
    _log(f"CHECK audio_capture: {capture}")
    _log(f"CHECK prox_voice: {voice}")


def _check_map_actors(checks: dict) -> None:
    kayleigh_instances = 0
    victoria_instances = 0
    try:
        unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)
        actors = list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors())
        for actor in actors:
            tags = list(actor.get_editor_property("tags") or [])
            if TAG_VICTORIA in tags:
                victoria_instances += 1
            if TAG_KAYLEIGH in tags or "BP_KayleighCharacter" in actor.get_name():
                kayleigh_instances += 1
    except Exception as exc:
        _log(f"WARN map actor check: {exc}")
    checks["home_kayleigh_instance"] = kayleigh_instances >= 1
    checks["victoria_avatar_preserved"] = victoria_instances >= 1
    _log(f"CHECK home_kayleigh_instance: {checks['home_kayleigh_instance']} (count={kayleigh_instances})")
    _log(
        f"CHECK victoria_avatar_preserved: {checks['victoria_avatar_preserved']} "
        f"(VictoriaAvatar count={victoria_instances})"
    )


def main() -> bool:
    _log("=== verify_kayleigh_player START ===")
    checks = {}

    try:
        _check_bp_exists(checks)
        if checks.get("bp_exists"):
            _check_parent_character(checks)
            _check_tag_on_cdo(checks)
            _check_capsule(checks)
            _check_camera_present(checks)
            _check_prox_audio_components(checks)
        else:
            checks["parent_character"] = False
            checks["tag_kayleigh_player"] = False
            checks["capsule_sized"] = False
            checks["camera_boom"] = False
            checks["follow_camera"] = False
            checks["audio_capture"] = False
            checks["prox_voice"] = False

        _check_gamemode_default_pawn(checks)
        _check_map_actors(checks)

    except Exception as exc:
        _log(f"FATAL: {exc}")
        _log(traceback.format_exc())

    required = [
        "bp_exists",
        "parent_character",
        "tag_kayleigh_player",
        "capsule_sized",
        "camera_boom",
        "follow_camera",
        "gamemode_default_pawn",
        "home_kayleigh_instance",
        "victoria_avatar_preserved",
    ]
    optional = ["audio_capture", "prox_voice", "gamemode_exists"]

    _log("--- CHECKLIST ---")
    all_required_pass = True
    for key in required:
        val = checks.get(key, False)
        status = "PASS" if val else "FAIL"
        _log(f"  [{status}] {key}")
        if not val:
            all_required_pass = False

    for key in optional:
        val = checks.get(key, False)
        status = "PASS" if val else "WARN"
        _log(f"  [{status}] {key} (optional)")

    result = "PASS" if all_required_pass else "FAIL"
    _log_file(f"RESULT: {result}")
    _log(f"=== verify_kayleigh_player END — {result} ===")
    return all_required_pass


if __name__ == "__main__":
    main()
else:
    main()
