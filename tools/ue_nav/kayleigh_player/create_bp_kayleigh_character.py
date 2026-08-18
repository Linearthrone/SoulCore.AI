# BED-172 / TASK-172 — Create BP_KayleighCharacter (grounded player pawn).
# Run headless: UnrealEditor-Cmd MyProject.uproject /Game/Home -ExecutePythonScript=... -unattended
# Idempotent: updates existing BP; does NOT remove VictoriaAvatar actors.

import os
import traceback
import unreal

# ---------------------------------------------------------------------------
# Constants (mirror BED-114 Victoria values unless noted)
# ---------------------------------------------------------------------------
BP_PATH = "/Game/Characters/BP_KayleighCharacter"
BP_PACKAGE = "/Game/Characters"
BP_NAME = "BP_KayleighCharacter"

HOME_MAP = "/Game/Home"
TAG_KAYLEIGH = "KayleighPlayer"

VICTORIA_REFERENCE_SPAWN = unreal.Vector(-231.92, 420.0, 106.0)
# Offset ~3 m from Victoria (near door / living area) — different spawn, same floor Z.
KAYLEIGH_SPAWN = unreal.Vector(-180.0, 480.0, 106.0)

CAPSULE_RADIUS = 34.0
CAPSULE_HALF_HEIGHT = 96.0
MESH_REL_LOCATION = unreal.Vector(0.0, 0.0, -96.0)
MESH_REL_ROTATION = unreal.Rotator(0.0, -90.0, 0.0)

MAX_WALK_SPEED = 450.0
MAX_WALK_SPEED_CROUCHED = 200.0
ROTATION_RATE_YAW = 500.0

POST_PROCESS_ABP = "/Game/MetaHumans/Common/Body/ABP_Body_PostProcess"
LOCO_ABP_CANDIDATES = [
    "/Game/Animations/Victoria/Locomotion/ABP_Victoria_Locomotion",
    "/Game/Animations/Kayleigh/Locomotion/ABP_Kayleigh_Locomotion",
]

KAYLEIGH_MESH_CANDIDATES = [
    "/Game/MetaHumans/MHC_Kayleigh/Body/SKM_MHC_Kayleigh_BodyMesh",
    "/Game/MetaHumans/MHC_Kayleigh/Body/SKM_MHC_Kayleigh_Body",
    "/Game/MetaHumans/MHC_Kayleigh/Body/SKM_MHC_Kayleigh",
    "/Game/MetaHumans/MHC_Kayleigh/Meshes/SKM_MHC_Kayleigh_BodyMesh",
    "/Game/MetaHumans/MHC_Kayleigh/SKM_MHC_Kayleigh_BodyMesh",
    "/Game/MetaHumans/Kayleigh/Body/SKM_Kayleigh_BodyMesh",
    "/Game/MetaHumans/MHC_Kayleigh/Face/SKM_MHC_Kayleigh_FaceMesh",  # fallback probe only
]

# Eye ~160 cm from feet: capsule half-height (96) + ~64 cm above capsule center.
CAMERA_BOOM_REL_Z = 64.0
CAMERA_BOOM_ARM_LENGTH = 0.0
CAMERA_FORWARD_OFFSET = 10.0

LOG_BASENAME = "create_bp_kayleigh_character"

# C++ drop-in parent (BED-172) — try HouseVictoriaBridge module first, then KayleighPlayer.
KAYLEIGH_CPP_PARENT_CANDIDATES = [
    "/Script/HouseVictoriaBridge.KayleighPlayerCharacter",
    "/Script/KayleighPlayer.KayleighPlayerCharacter",
]


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


def _save_dirty() -> None:
    try:
        unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
    except Exception as exc:
        _log(f"WARN save_dirty_packages: {exc}")


def _load_map() -> None:
    try:
        unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)
        _log(f"Loaded map {HOME_MAP}")
    except Exception as exc:
        _log(f"WARN could not load map {HOME_MAP}: {exc}")


def _asset_exists(path: str) -> bool:
    try:
        return bool(unreal.EditorAssetLibrary.does_asset_exist(path))
    except Exception:
        return False


def _load_class(path: str):
    if not _asset_exists(path):
        return None
    try:
        asset = unreal.EditorAssetLibrary.load_asset(path)
        if asset is None:
            return None
        generated = asset.generated_class()
        if generated is not None:
            return generated
        return unreal.load_class(None, path + "_C")
    except Exception as exc:
        _log(f"WARN load_class({path}): {exc}")
        return None


def _resolve_kayleigh_parent_class():
    """Prefer compiled KayleighPlayerCharacter C++; else Engine.Character."""
    for script_path in KAYLEIGH_CPP_PARENT_CANDIDATES:
        try:
            parent = unreal.load_class(None, script_path)
            if parent is not None:
                _log(
                    f"Parent class: {script_path} -> {parent.get_name()} "
                    f"(module={parent.get_outer().get_name() if parent.get_outer() else '?'})"
                )
                return parent, script_path
        except Exception as exc:
            _log(f"WARN load_class({script_path}): {exc}")

    fallback = unreal.Character
    _log(
        "Parent class: fallback unreal.Character "
        "(KayleighPlayerCharacter C++ not loaded — rebuild plugin/module)"
    )
    return fallback, "Engine.Character"


def _try_reparent_blueprint(blueprint, parent_class, parent_label: str) -> bool:
    """Reparent BP to C++ KayleighPlayerCharacter when available."""
    try:
        generated = blueprint.generated_class()
        if generated is None:
            return False
        current = generated.get_super_class()
        if current == parent_class:
            _log(f"Blueprint already parented to {parent_label}")
            return True
        if hasattr(unreal.BlueprintEditorLibrary, "reparent_blueprint"):
            unreal.BlueprintEditorLibrary.reparent_blueprint(blueprint, parent_class)
        elif hasattr(unreal.EditorAssetLibrary, "set_blueprint_parent_class"):
            unreal.EditorAssetLibrary.set_blueprint_parent_class(blueprint, parent_class)
        else:
            blueprint.set_editor_property("parent_class", parent_class)
        unreal.BlueprintEditorLibrary.compile_blueprint(blueprint)
        _log(f"Reparented Blueprint to {parent_label}")
        return True
    except Exception as exc:
        _log(f"WARN reparent to {parent_label}: {exc}")
        return False


def _probe_kayleigh_mesh() -> str:
    tried = []
    for path in KAYLEIGH_MESH_CANDIDATES:
        tried.append(path)
        if _asset_exists(path):
            asset = unreal.EditorAssetLibrary.load_asset(path)
            if isinstance(asset, unreal.SkeletalMesh):
                _log(f"Body mesh found (candidate list): {path}")
                return path

    # Asset Registry fuzzy probe for Kayleigh body meshes.
    try:
        registry = unreal.AssetRegistryHelpers.get_asset_registry()
        registry.search_all_assets(True)
        filter_ = unreal.ARFilter(
            class_names=["SkeletalMesh"],
            package_paths=["/Game/MetaHumans"],
            recursive_paths=True,
        )
        for data in registry.get_assets(filter_):
            name = str(data.asset_name)
            pkg = str(data.package_name)
            combined = f"{pkg}/{name}"
            lower = combined.lower()
            if "kayleigh" not in lower:
                continue
            if "body" not in lower and "combined" not in lower and "skm_" not in lower:
                continue
            if "face" in lower and "body" not in lower:
                continue
            tried.append(combined)
            asset = data.get_asset()
            if isinstance(asset, unreal.SkeletalMesh):
                _log(f"Body mesh found (registry): {combined}")
                return combined
    except Exception as exc:
        _log(f"WARN registry probe: {exc}")

    _log("FAIL: Kayleigh body mesh not found. Tried:")
    for p in tried:
        _log(f"  - {p}")
    return ""


def _get_subsystem():
    return unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)


def _gather_bp_handles(blueprint):
    subsystem = _get_subsystem()
    handles = subsystem.k2_gather_subobject_data_for_blueprint(blueprint)
    if not handles:
        raise RuntimeError("No subobject handles for blueprint")
    return subsystem, handles


def _find_subobject_by_name(subsystem, handles, name_substr: str):
    bfl = unreal.SubobjectDataBlueprintFunctionLibrary
    for handle in handles:
        try:
            data = subsystem.k2_find_subobject_data_from_handle(handle)
            obj = bfl.get_object(data)
            if obj is None:
                continue
            obj_name = obj.get_name()
            if name_substr.lower() in obj_name.lower():
                return handle, obj
        except Exception:
            continue
    return None, None


def _add_bp_component(blueprint, parent_handle, component_class, name: str):
    subsystem = _get_subsystem()
    params = unreal.AddNewSubobjectParams(
        parent_handle=parent_handle,
        new_class=component_class,
        blueprint_context=blueprint,
    )
    with unreal.ScopedEditorTransaction(f"Add {name}"):
        sub_handle, fail_reason = subsystem.add_new_subobject(params)
    if fail_reason and not fail_reason.is_empty():
        raise RuntimeError(f"add_new_subobject({name}): {fail_reason}")
    subsystem.rename_subobject(handle=sub_handle, new_name=unreal.Text(name))
    subsystem.attach_subobject(owner_handle=parent_handle, child_to_add_handle=sub_handle)
    bfl = unreal.SubobjectDataBlueprintFunctionLibrary
    obj = bfl.get_object(bfl.get_data(sub_handle))
    return sub_handle, obj


def _ensure_bp_component(blueprint, parent_handle, component_class, name: str):
    subsystem, handles = _gather_bp_handles(blueprint)
    _, existing = _find_subobject_by_name(subsystem, handles, name)
    if existing is not None:
        _log(f"Component already exists: {name} ({existing.get_class().get_name()})")
        return existing
    _, obj = _add_bp_component(blueprint, parent_handle, component_class, name)
    _log(f"Added component: {name} ({obj.get_class().get_name()})")
    return obj


def _create_or_load_blueprint():
    parent_class, parent_label = _resolve_kayleigh_parent_class()

    if _asset_exists(BP_PATH):
        bp = unreal.EditorAssetLibrary.load_asset(BP_PATH)
        _log(f"Reusing existing Blueprint: {BP_PATH}")
        if parent_label != "Engine.Character":
            _try_reparent_blueprint(bp, parent_class, parent_label)
        return bp

    factory = unreal.BlueprintFactory()
    factory.set_editor_property("parent_class", parent_class)
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    with unreal.ScopedEditorTransaction("Create BP_KayleighCharacter"):
        bp = asset_tools.create_asset(BP_NAME, BP_PACKAGE, unreal.Blueprint, factory)
    if bp is None:
        raise RuntimeError(f"Failed to create Blueprint at {BP_PATH}")
    _log(f"Created Blueprint: {BP_PATH} parent={parent_label}")
    return bp


def _configure_cdo(blueprint, body_mesh_path: str) -> dict:
    results = {
        "capsule": False,
        "mesh": False,
        "post_process": False,
        "anim_class": False,
        "char_move": False,
        "tag": False,
        "camera_boom": False,
        "follow_camera": False,
    }

    generated = blueprint.generated_class()
    if generated is None:
        raise RuntimeError("Blueprint has no generated_class()")

    cdo = unreal.get_default_object(generated)
    _log(f"CDO: {cdo.get_name()} class={cdo.get_class().get_name()}")

    # Capsule
    try:
        capsule = cdo.get_editor_property("capsule_component")
        if capsule:
            capsule.set_editor_property("capsule_radius", CAPSULE_RADIUS)
            capsule.set_editor_property("capsule_half_height", CAPSULE_HALF_HEIGHT)
            results["capsule"] = True
            _log(
                f"Capsule radius={CAPSULE_RADIUS} half_height={CAPSULE_HALF_HEIGHT}"
            )
    except Exception as exc:
        _log(f"WARN capsule config: {exc}")

    # Mesh
    body_mesh = unreal.EditorAssetLibrary.load_asset(body_mesh_path)
    if not isinstance(body_mesh, unreal.SkeletalMesh):
        raise RuntimeError(f"Asset is not SkeletalMesh: {body_mesh_path}")

    try:
        mesh = cdo.get_editor_property("mesh")
        if mesh:
            mesh.set_skeletal_mesh(body_mesh)
            mesh.set_editor_property("relative_location", MESH_REL_LOCATION)
            mesh.set_editor_property("relative_rotation", MESH_REL_ROTATION)
            mesh.set_collision_enabled(unreal.CollisionEnabled.QUERY_ONLY)
            results["mesh"] = True
            _log(f"Body mesh set: {body_mesh_path}")
    except Exception as exc:
        _log(f"WARN mesh config: {exc}")

    # Post-process AnimBP (UE 5.8 API)
    try:
        mesh = cdo.get_editor_property("mesh")
        pp_class = _load_class(POST_PROCESS_ABP)
        if mesh and pp_class:
            mesh.set_override_post_process_anim_bp(pp_class, True)
            results["post_process"] = True
            _log(
                f"PostProcessAnimBP set via set_override_post_process_anim_bp: {pp_class.get_name()}"
            )
    except Exception as exc:
        _log(f"WARN post-process ABP: {exc}")

    # Locomotion AnimClass (reuse Victoria if loadable)
    try:
        mesh = cdo.get_editor_property("mesh")
        for loco_path in LOCO_ABP_CANDIDATES:
            loco_class = _load_class(loco_path)
            if loco_class:
                mesh.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_BLUEPRINT)
                mesh.set_editor_property("anim_class", loco_class)
                results["anim_class"] = True
                _log(f"AnimClass set: {loco_path} -> {loco_class.get_name()}")
                break
        if not results["anim_class"]:
            _log("WARN: No locomotion AnimBP loadable; mesh may T-pose until BED-115 Kayleigh loco")
    except Exception as exc:
        _log(f"WARN anim_class: {exc}")

    # Character movement
    try:
        move = cdo.get_editor_property("character_movement")
        if move:
            move.set_editor_property("max_walk_speed", MAX_WALK_SPEED)
            move.set_editor_property("max_walk_speed_crouched", MAX_WALK_SPEED_CROUCHED)
            move.set_editor_property("rotation_rate", unreal.Rotator(0.0, ROTATION_RATE_YAW, 0.0))
            move.set_editor_property("b_orient_rotation_to_movement", True)
            results["char_move"] = True
            _log(
                f"CharMove max_walk_speed={MAX_WALK_SPEED} crouched={MAX_WALK_SPEED_CROUCHED} "
                f"rotation_rate_yaw={ROTATION_RATE_YAW}"
            )
    except Exception as exc:
        _log(f"WARN character_movement: {exc}")

    # Actor tag
    try:
        tags = list(cdo.get_editor_property("tags") or [])
        if TAG_KAYLEIGH not in tags:
            tags.append(TAG_KAYLEIGH)
        cdo.set_editor_property("tags", tags)
        results["tag"] = TAG_KAYLEIGH in list(cdo.get_editor_property("tags") or [])
        _log(f"CDO tags: {list(cdo.get_editor_property('tags') or [])}")
    except Exception as exc:
        _log(f"WARN tags: {exc}")

    # Camera boom + follow camera on blueprint SCS
    try:
        subsystem, handles = _gather_bp_handles(blueprint)
        _, capsule_obj = _find_subobject_by_name(subsystem, handles, "CollisionCylinder")
        if capsule_obj is None:
            _, capsule_obj = _find_subobject_by_name(subsystem, handles, "Capsule")
        if capsule_obj is None:
            capsule_obj = cdo.get_editor_property("capsule_component")

        parent_handle = None
        for handle in handles:
            data = subsystem.k2_find_subobject_data_from_handle(handle)
            obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
            if obj is capsule_obj:
                parent_handle = handle
                break
        if parent_handle is None and handles:
            parent_handle = handles[0]

        if parent_handle is not None:
            boom = _ensure_bp_component(
                blueprint, parent_handle, unreal.SpringArmComponent, "CameraBoom"
            )
            if boom:
                boom.set_editor_property("target_arm_length", CAMERA_BOOM_ARM_LENGTH)
                boom.set_editor_property("b_use_pawn_control_rotation", True)
                boom.set_editor_property(
                    "relative_location",
                    unreal.Vector(0.0, 0.0, CAMERA_BOOM_REL_Z),
                )
                results["camera_boom"] = True
                _log(
                    f"CameraBoom configured arm={CAMERA_BOOM_ARM_LENGTH} rel_z={CAMERA_BOOM_REL_Z}"
                )

                # Re-gather for camera parent handle
                subsystem, handles = _gather_bp_handles(blueprint)
                boom_handle, _ = _find_subobject_by_name(subsystem, handles, "CameraBoom")
                if boom_handle is not None:
                    cam = _ensure_bp_component(
                        blueprint, boom_handle, unreal.CameraComponent, "FollowCamera"
                    )
                    if cam:
                        cam.set_editor_property(
                            "relative_location",
                            unreal.Vector(CAMERA_FORWARD_OFFSET, 0.0, 0.0),
                        )
                        results["follow_camera"] = True
                        _log("FollowCamera attached to CameraBoom")
    except Exception as exc:
        _log(f"WARN camera setup: {exc}")
        _log(traceback.format_exc())

    unreal.BlueprintEditorLibrary.compile_blueprint(blueprint)
    return results


def _spawn_actor_from_bp(blueprint):
    generated = blueprint.generated_class()
    if generated is None:
        raise RuntimeError("Cannot spawn: blueprint not compiled")

    rotation = unreal.Rotator(0.0, 0.0, 0.0)
    actor = None
    try:
        eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
        actor = eas.spawn_actor_from_class(generated, KAYLEIGH_SPAWN, rotation)
    except Exception:
        try:
            actor = unreal.EditorLevelLibrary.spawn_actor_from_class(
                generated, KAYLEIGH_SPAWN, rotation
            )
        except Exception as exc:
            _log(f"WARN spawn failed: {exc}")
            return None

    if actor is None:
        return None

    try:
        actor.set_actor_label("BP_KayleighCharacter", True)
    except Exception:
        pass

    tags = list(actor.get_editor_property("tags") or [])
    if TAG_KAYLEIGH not in tags:
        tags.append(TAG_KAYLEIGH)
    actor.set_editor_property("tags", tags)

    _log(
        f"Placed actor: {actor.get_name()} tags={list(actor.get_editor_property('tags') or [])} "
        f"loc=({KAYLEIGH_SPAWN.x:.2f},{KAYLEIGH_SPAWN.y:.2f},{KAYLEIGH_SPAWN.z:.2f}) "
        f"(Victoria ref {VICTORIA_REFERENCE_SPAWN.x:.2f},{VICTORIA_REFERENCE_SPAWN.y:.2f},"
        f"{VICTORIA_REFERENCE_SPAWN.z:.2f})"
    )
    return actor


def _remove_duplicate_kayleigh_actors(keep_actor=None):
    """Remove extra KayleighPlayer-tagged or BP_KayleighCharacter instances; never touch VictoriaAvatar."""
    removed = []
    try:
        actors = list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors())
    except Exception:
        actors = list(unreal.EditorLevelLibrary.get_all_level_actors())

    for actor in actors:
        try:
            tags = list(actor.get_editor_property("tags") or [])
            if "VictoriaAvatar" in tags:
                continue
            name = actor.get_name()
            is_kayleigh = TAG_KAYLEIGH in tags or "BP_KayleighCharacter" in name
            if not is_kayleigh:
                continue
            if keep_actor is not None and actor == keep_actor:
                continue
            label = actor.get_actor_label()
            unreal.get_editor_subsystem(unreal.EditorActorSubsystem).destroy_actor(actor)
            removed.append(label or name)
        except Exception as exc:
            _log(f"WARN could not evaluate actor: {exc}")

    if removed:
        _log(f"Removed duplicate Kayleigh actors: {removed}")
    else:
        _log("No duplicate Kayleigh actors removed")


def main() -> bool:
    _log("=== create_bp_kayleigh_character START ===")
    ok = True

    try:
        _load_map()
        body_mesh_path = _probe_kayleigh_mesh()
        if not body_mesh_path:
            _log("RESULT: FAIL — Kayleigh body mesh missing (sync MHC_Kayleigh to project)")
            return False

        blueprint = _create_or_load_blueprint()
        results = _configure_cdo(blueprint, body_mesh_path)

        required = ["capsule", "mesh", "char_move", "tag"]
        for key in required:
            if not results.get(key):
                _log(f"FAIL required check: {key}")
                ok = False

        if not results.get("post_process"):
            _log("WARN: PostProcessAnimBP not set (mesh may look wrong)")
        if not results.get("anim_class"):
            _log("WARN: AnimClass not set (walk anims may not play until loco assigned)")
        if not results.get("camera_boom") or not results.get("follow_camera"):
            _log("WARN: CameraBoom/FollowCamera incomplete — check component graph in editor")

        _remove_duplicate_kayleigh_actors()
        placed = _spawn_actor_from_bp(blueprint)
        if placed is not None:
            _remove_duplicate_kayleigh_actors(keep_actor=placed)

        try:
            unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
        except Exception:
            unreal.EditorLevelLibrary.save_current_level()
        _save_dirty()

        victoria_count = 0
        try:
            for actor in unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors():
                if "VictoriaAvatar" in list(actor.get_editor_property("tags") or []):
                    victoria_count += 1
        except Exception:
            pass
        _log(f"VictoriaAvatar actors on map (unchanged): {victoria_count}")

    except Exception as exc:
        _log(f"FATAL: {exc}")
        _log(traceback.format_exc())
        ok = False

    result = "PASS" if ok else "FAIL"
    _log_file(f"RESULT: {result}")
    _log(f"=== create_bp_kayleigh_character END — {result} ===")
    return ok


if __name__ == "__main__":
    main()
else:
    main()
