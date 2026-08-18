"""
REX-01 / TASK-191 — End-to-end PIE possess Kayleigh (never Victoria).

Run inside UE 5.8 Editor with /Game/Home open (or via run_rex_pie_possess_kayleigh.ps1).

Pipeline:
  1) create_bp_kayleigh_character.py  → /Game/Characters/BP_KayleighCharacter
  2) setup_kayleigh_gamemode.py       → GM_HouseVictoria.DefaultPawnClass = BP_KayleighCharacter
  3) verify_kayleigh_player.py        → hard checks (Kayleigh pawn, no VictoriaAvatar on player CDO)
  4) assert DefaultPawnClass name contains Kayleigh and does NOT contain Victoria

Hard rules (fail loud):
  - Never set player DefaultPawnClass to Victoria / BP_VictoriaCharacter / VictoriaAvatar
  - Never reparent BP_MHC_Kayleigh (MHC regen) — use Character wrapper only
  - Victoria stays AI-possessed for the bridge; Kurt possesses Kayleigh in PIE
"""
from __future__ import annotations

import importlib.util
import os
import sys
import traceback

import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
EVIDENCE_DIR = r"C:\Users\kurtw\Soul_Core\tmpcode\rex191-kayleigh-pie"
LOG_FILE = os.path.join(EVIDENCE_DIR, "rex_pie_possess_kayleigh.log")
BP_KAYLEIGH = "/Game/Characters/BP_KayleighCharacter"
GM_PATH = "/Game/Characters/GM_HouseVictoria"
VICTORIA_FORBIDDEN = ("victoria", "victoriaavatar", "bp_victoriacharacter", "bp_mhc_victoria")

_out: list[str] = []


def log(msg: str) -> None:
    line = f"[rex_pie_possess_kayleigh] {msg}"
    _out.append(line)
    unreal.log(line)
    print(line)
    try:
        os.makedirs(EVIDENCE_DIR, exist_ok=True)
        with open(LOG_FILE, "w", encoding="utf-8") as f:
            f.write("\n".join(_out) + "\n")
    except Exception:
        pass


def _load_module(filename: str):
    path = os.path.join(HERE, filename)
    if not os.path.isfile(path):
        raise FileNotFoundError(path)
    spec = importlib.util.spec_from_file_location(filename.replace(".py", ""), path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {path}")
    mod = importlib.util.module_from_spec(spec)
    # Ensure sibling imports / re-exec work under Editor Python
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)
    return mod


def _assert_default_pawn_is_kayleigh() -> bool:
    if not unreal.EditorAssetLibrary.does_asset_exist(GM_PATH):
        log(f"FAIL: GameMode missing at {GM_PATH}")
        return False
    gm = unreal.EditorAssetLibrary.load_asset(GM_PATH)
    gen = gm.generated_class()
    cdo = unreal.get_default_object(gen)
    pawn = cdo.get_editor_property("default_pawn_class")
    if pawn is None:
        log("FAIL: DefaultPawnClass is None")
        return False
    name = str(pawn.get_name()).lower()
    log(f"DefaultPawnClass = {pawn.get_name()}")
    if any(v in name for v in VICTORIA_FORBIDDEN):
        log(
            "FAIL HARD: DefaultPawnClass is Victoria-related. "
            "BOB-class mistake — unset this immediately. Player must be Kayleigh."
        )
        return False
    if "kayleigh" not in name:
        log(
            f"FAIL HARD: DefaultPawnClass '{pawn.get_name()}' does not contain Kayleigh. "
            "Refuse ghost / wrong pawn."
        )
        return False
    if not unreal.EditorAssetLibrary.does_asset_exist(BP_KAYLEIGH):
        log(f"FAIL: {BP_KAYLEIGH} missing after create step")
        return False
    kayleigh_gen = unreal.EditorAssetLibrary.load_asset(BP_KAYLEIGH).generated_class()
    if pawn != kayleigh_gen:
        log(
            f"FAIL: DefaultPawnClass ({pawn.get_name()}) != "
            f"BP_KayleighCharacter ({kayleigh_gen.get_name() if kayleigh_gen else None})"
        )
        return False
    log("PASS: DefaultPawnClass is exactly BP_KayleighCharacter (not Victoria).")
    return True


def main() -> bool:
    log("=== REX-01 PIPELINE START — PIE possess Kayleigh only ===")
    ok = True
    try:
        create = _load_module("create_bp_kayleigh_character.py")
        setup = _load_module("setup_kayleigh_gamemode.py")
        verify = _load_module("verify_kayleigh_player.py")

        log("--- step 1/3 create_bp_kayleigh_character ---")
        create_ok = True
        if hasattr(create, "main"):
            create_ok = bool(create.main())
        if not create_ok:
            log("FAIL: create_bp_kayleigh_character returned False")
            ok = False

        log("--- step 2/3 setup_kayleigh_gamemode ---")
        setup_ok = True
        if hasattr(setup, "main"):
            setup_ok = bool(setup.main())
        if not setup_ok:
            log("FAIL: setup_kayleigh_gamemode returned False")
            ok = False

        log("--- step 3/3 verify_kayleigh_player ---")
        verify_ok = True
        if hasattr(verify, "main"):
            verify_ok = bool(verify.main())
        if not verify_ok:
            log("FAIL: verify_kayleigh_player returned False")
            ok = False

        log("--- hard assert DefaultPawnClass ---")
        if not _assert_default_pawn_is_kayleigh():
            ok = False

    except Exception as ex:
        log(f"FATAL: {ex}")
        log(traceback.format_exc())
        ok = False

    result = "PASS" if ok else "FAIL"
    log(f"=== REX-01 PIPELINE END — {result} ===")
    if ok:
        log(
            "NEXT: Press Play (PIE). You must be BP_KayleighCharacter (grounded Kayleigh). "
            "If you are Victoria or a flying ghost → FAIL and stop — do not 'fix' by possessing Victoria."
        )
    return ok


if __name__ == "__main__":
    main()
else:
    main()
