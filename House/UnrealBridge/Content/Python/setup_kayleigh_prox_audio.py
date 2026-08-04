# BED-172 — Proximity voice components on BP_KayleighCharacter + Input stubs.
# AudioCaptureComponent + spatialized ProxVoice AudioComponent with attenuation.

import os
import traceback
import unreal

BP_PATH = "/Game/Characters/BP_KayleighCharacter"
IMC_PATH = "/Game/Input/IMC_Kayleigh"
IA_PROX_TALK_PATH = "/Game/Input/IA_Kayleigh_ProxTalk"
IA_MOVE_PATH = "/Game/Input/IA_Kayleigh_Move"
IA_LOOK_PATH = "/Game/Input/IA_Kayleigh_Look"
INPUT_PACKAGE = "/Game/Input"

PROX_INNER_RADIUS_CM = 150.0
PROX_OUTER_RADIUS_CM = 900.0

LOG_BASENAME = "setup_kayleigh_prox_audio"


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


def _get_subsystem():
    return unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)


def _gather_bp_handles(blueprint):
    subsystem = _get_subsystem()
    handles = subsystem.k2_gather_subobject_data_for_blueprint(blueprint)
    return subsystem, handles


def _find_subobject_by_name(subsystem, handles, name_substr: str):
    bfl = unreal.SubobjectDataBlueprintFunctionLibrary
    for handle in handles:
        try:
            data = subsystem.k2_find_subobject_data_from_handle(handle)
            obj = bfl.get_object(data)
            if obj is None:
                continue
            if name_substr.lower() in obj.get_name().lower():
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
    return bfl.get_object(bfl.get_data(sub_handle))


def _ensure_component(blueprint, parent_handle, component_class, name: str):
    subsystem, handles = _gather_bp_handles(blueprint)
    _, existing = _find_subobject_by_name(subsystem, handles, name)
    if existing is not None:
        _log(f"Component exists: {name}")
        return existing
    obj = _add_bp_component(blueprint, parent_handle, component_class, name)
    _log(f"Added component: {name}")
    return obj


def _create_attenuation_settings() -> unreal.SoundAttenuation:
    """Build or load attenuation asset for prox voice."""
    atten_path = "/Game/Input/ATT_Kayleigh_ProxVoice"
    if _asset_exists(atten_path):
        asset = unreal.EditorAssetLibrary.load_asset(atten_path)
        if isinstance(asset, unreal.SoundAttenuation):
            return asset

    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    factory = unreal.SoundAttenuationFactory()
    with unreal.ScopedEditorTransaction("Create ATT_Kayleigh_ProxVoice"):
        atten = asset_tools.create_asset(
            "ATT_Kayleigh_ProxVoice", INPUT_PACKAGE, unreal.SoundAttenuation, factory
        )
    if atten is None:
        raise RuntimeError("Failed to create SoundAttenuation asset")

    try:
        atten.set_editor_property("attenuation_shape", unreal.AttenuationShape.SPHERE)
        atten.set_editor_property("falloff_distance", PROX_OUTER_RADIUS_CM - PROX_INNER_RADIUS_CM)
        # UE5 uses attenuation struct on component; also set inner radius where supported.
        settings = atten.get_editor_property("attenuation")
        if settings is not None:
            settings.set_editor_property("attenuation_shape", unreal.AttenuationShape.SPHERE)
            settings.set_editor_property("inner_radius", PROX_INNER_RADIUS_CM)
            settings.set_editor_property("falloff_distance", PROX_OUTER_RADIUS_CM - PROX_INNER_RADIUS_CM)
            atten.set_editor_property("attenuation", settings)
    except Exception as exc:
        _log(f"WARN attenuation asset properties: {exc} — tune in editor")

    unreal.EditorAssetLibrary.save_loaded_asset(atten)
    _log(f"Created attenuation asset: {atten_path}")
    return atten


def _configure_prox_voice(audio_comp: unreal.AudioComponent, atten) -> None:
    audio_comp.set_editor_property("b_is_ui_sound", False)
    audio_comp.set_editor_property("b_auto_activate", False)
    try:
        audio_comp.set_editor_property("attenuation_settings", atten)
    except Exception:
        try:
            audio_comp.set_editor_property("attenuation_override", atten)
        except Exception as exc:
            _log(f"WARN set attenuation on ProxVoice: {exc}")
    _log(
        f"ProxVoice attenuation target inner≈{PROX_INNER_RADIUS_CM}cm outer≈{PROX_OUTER_RADIUS_CM}cm "
        "(verify in Details panel)"
    )


def _try_create_input_stubs() -> bool:
    """Best-effort Enhanced Input asset stubs; full key binding usually needs editor UI."""
    created_any = False
    try:
        if not unreal.EditorAssetLibrary.does_directory_exist(INPUT_PACKAGE):
            unreal.EditorAssetLibrary.make_directory(INPUT_PACKAGE)
    except Exception as exc:
        _log(f"WARN make_directory {INPUT_PACKAGE}: {exc}")

    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    stubs = [
        ("IA_Kayleigh_ProxTalk", IA_PROX_TALK_PATH, unreal.InputAction),
        ("IA_Kayleigh_Move", IA_MOVE_PATH, unreal.InputAction),
        ("IA_Kayleigh_Look", IA_LOOK_PATH, unreal.InputAction),
    ]

    for asset_name, path, cls in stubs:
        if _asset_exists(path):
            _log(f"Input stub exists: {path}")
            continue
        try:
            factory = unreal.DataAssetFactory()
            factory.set_editor_property("data_asset_class", cls)
            with unreal.ScopedEditorTransaction(f"Create {asset_name}"):
                asset = asset_tools.create_asset(asset_name, INPUT_PACKAGE, cls, factory)
            if asset is not None:
                _log(f"Created InputAction stub: {path}")
                created_any = True
        except Exception as exc:
            _log(f"WARN could not create {asset_name}: {exc}")

    if not _asset_exists(IMC_PATH):
        try:
            factory = unreal.DataAssetFactory()
            factory.set_editor_property("data_asset_class", unreal.InputMappingContext)
            with unreal.ScopedEditorTransaction("Create IMC_Kayleigh"):
                imc = asset_tools.create_asset(
                    "IMC_Kayleigh", INPUT_PACKAGE, unreal.InputMappingContext, factory
                )
            if imc is not None:
                _log(f"Created InputMappingContext stub: {IMC_PATH}")
                created_any = True
        except Exception as exc:
            _log(f"WARN could not create IMC_Kayleigh: {exc}")
    else:
        _log(f"Input stub exists: {IMC_PATH}")

    return created_any


def _print_manual_input_steps() -> None:
    _log("MANUAL_INPUT — Enhanced Input (cannot fully wire V-hold capture in headless Python):")
    steps = [
        "1. Content Browser → /Game/Input — open IMC_Kayleigh (create if stubs missing).",
        "2. Create Input Actions: IA_Kayleigh_Move (Axis2D), IA_Kayleigh_Look (Axis2D), IA_Kayleigh_ProxTalk (Bool).",
        "3. Map keys: WASD → Move, Mouse XY → Look, V (hold) → ProxTalk.",
        "4. On BP_KayleighCharacter: add EnhancedInputComponent; set Default Mapping Contexts = IMC_Kayleigh.",
        "5. Event Graph: ProxTalk Started → AudioCapture Start; ProxTalk Completed → Stop + feed ProxVoice.",
        "6. Set ProxVoice volume for others; reduce/local mute for owner (no full self-earbleed).",
        "7. PlayerController or pawn: confirm audio listener follows camera (default when possessed).",
    ]
    for line in steps:
        _log(line)


def main() -> bool:
    _log("=== setup_kayleigh_prox_audio START ===")
    ok = True
    capture_ok = False
    voice_ok = False

    try:
        if not _asset_exists(BP_PATH):
            _log(f"FAIL: {BP_PATH} missing — run create_bp_kayleigh_character.py first")
            return False

        blueprint = unreal.EditorAssetLibrary.load_asset(BP_PATH)
        generated = blueprint.generated_class()
        cdo = unreal.get_default_object(generated)

        subsystem, handles = _gather_bp_handles(blueprint)
        _, mesh_obj = _find_subobject_by_name(subsystem, handles, "CharacterMesh")
        if mesh_obj is None:
            mesh_obj = cdo.get_editor_property("mesh")

        parent_handle = None
        for handle in handles:
            data = subsystem.k2_find_subobject_data_from_handle(handle)
            obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
            if obj is mesh_obj:
                parent_handle = handle
                break
        if parent_handle is None:
            _, cap_obj = _find_subobject_by_name(subsystem, handles, "CollisionCylinder")
            for handle in handles:
                data = subsystem.k2_find_subobject_data_from_handle(handle)
                obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
                if obj is cap_obj:
                    parent_handle = handle
                    break
        if parent_handle is None and handles:
            parent_handle = handles[0]

        # AudioCaptureComponent
        try:
            capture = _ensure_component(
                blueprint, parent_handle, unreal.AudioCaptureComponent, "AudioCapture"
            )
            if capture:
                capture_ok = True
                _log("AudioCaptureComponent present on BP_KayleighCharacter CDO graph")
        except Exception as exc:
            _log(f"WARN AudioCaptureComponent: {exc}")

        # ProxVoice spatialized audio
        try:
            atten = _create_attenuation_settings()
            voice = _ensure_component(
                blueprint, parent_handle, unreal.AudioComponent, "ProxVoice"
            )
            if voice:
                _configure_prox_voice(voice, atten)
                voice_ok = True
        except Exception as exc:
            _log(f"WARN ProxVoice AudioComponent: {exc}")

        input_stubs = _try_create_input_stubs()
        if input_stubs:
            _log("Input asset stubs created under /Game/Input — still need key mappings in editor.")
        _print_manual_input_steps()

        unreal.BlueprintEditorLibrary.compile_blueprint(blueprint)
        unreal.EditorAssetLibrary.save_loaded_asset(blueprint)
        try:
            unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
        except Exception:
            pass

        if not capture_ok or not voice_ok:
            ok = False
            _log("FAIL: ProxVoice and/or AudioCapture not configured on blueprint")

    except Exception as exc:
        _log(f"FATAL: {exc}")
        _log(traceback.format_exc())
        ok = False

    result = "PASS" if ok else "FAIL"
    _log_file(f"RESULT: {result}")
    _log(f"=== setup_kayleigh_prox_audio END — {result} ===")
    return ok


if __name__ == "__main__":
    main()
else:
    main()
