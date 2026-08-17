"""
BED-184 — legacy helper. Prefer REX-01 pipeline (TASK-191):

  tools/ue_nav/run_rex_pie_possess_kayleigh.ps1
  → kayleigh_player/rex_pie_possess_kayleigh.py
  → BP_KayleighCharacter + GM DefaultPawnClass (never Victoria)

This script still tries to point DefaultPawn at a Kayleigh Pawn/Character in-level.
If BP_MHC_Kayleigh is a bare MetaHuman Actor, it cannot possess it — use the REX
Character wrapper instead. It never selects Victoria as the player pawn.
"""
from __future__ import annotations

import unreal

VICTORIA_MARKERS = ("VictoriaAvatar", "Victoria", "BP_VictoriaCharacter", "BP_MHC_Victoria")
# Primary product name for Kurt's grounded body in Home.
KAYLEIGH_CLASS = "BP_MHC_Kayleigh"
PLAYER_MARKERS = (
    KAYLEIGH_CLASS,
    "MHC_Kayleigh",
    "Kayleigh",
    "PlayerAvatar",
    "Kurt",
    "Player",
)
GAME_MODE_PATH = "/Game/Blueprints/BP_HouseGameMode"
LOG_PREFIX = "[set_pie_player_pawn]"
EVIDENCE_DIR = r"C:\Users\kurtw\Soul_Core\tmpcode\bed184-pie-pawn"
LOG_FILE = EVIDENCE_DIR + r"\set_pie_player_pawn.log"

_out = []


def log(msg: str) -> None:
    line = f"{LOG_PREFIX} {msg}"
    _out.append(line)
    unreal.log(line)
    print(line)
    try:
        import os

        os.makedirs(EVIDENCE_DIR, exist_ok=True)
        with open(LOG_FILE, "w", encoding="utf-8") as f:
            f.write("\n".join(_out) + "\n")
    except Exception:
        pass


def actor_label(actor) -> str:
    try:
        return actor.get_actor_label()
    except Exception:
        return str(actor)


def class_name(actor) -> str:
    try:
        return str(actor.get_class().get_name())
    except Exception:
        return ""


def class_hierarchy(actor) -> str:
    """e.g. BP_MHC_Kayleigh_C -> MHC_… -> Actor — shows if possessable."""
    parts = []
    try:
        cls = actor.get_class()
        for _ in range(12):
            if cls is None:
                break
            parts.append(str(cls.get_name()))
            try:
                cls = cls.get_super_class()
            except Exception:
                break
    except Exception as ex:
        return f"(hierarchy error: {ex})"
    return " → ".join(parts)


def text_blob(actor) -> str:
    bits = [actor_label(actor), class_name(actor)]
    try:
        bits.extend(str(t) for t in actor.tags)
    except Exception:
        pass
    return " ".join(bits)


def matches_any(actor, markers) -> bool:
    blob = text_blob(actor).lower()
    return any(m.lower() in blob for m in markers)


def is_victoria(actor) -> bool:
    return matches_any(actor, VICTORIA_MARKERS)


def is_kayleigh(actor) -> bool:
    return matches_any(actor, (KAYLEIGH_CLASS, "MHC_Kayleigh", "Kayleigh"))


def is_pawn_like(actor) -> bool:
    try:
        return isinstance(actor, unreal.Pawn) or isinstance(actor, unreal.Character)
    except Exception:
        return False


def find_kayleigh_candidates():
    """Prefer BP_MHC_Kayleigh; never return Victoria."""
    subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    actors = list(subsystem.get_all_level_actors())

    kayleigh = [a for a in actors if is_kayleigh(a) and not is_victoria(a)]
    if kayleigh:
        # Prefer pawn-like instances first
        pawnish = [a for a in kayleigh if is_pawn_like(a)]
        return pawnish or kayleigh

    # Fallback: other player-marked non-Victoria pawns
    others = []
    for a in actors:
        if is_victoria(a):
            continue
        if not is_pawn_like(a):
            continue
        if matches_any(a, PLAYER_MARKERS):
            others.append(a)
    return others


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
        log("WARN: could not spawn PlayerStart — place one manually on BP_MHC_Kayleigh")


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

    try:
        generated = gm.generated_class()
        cdo = unreal.get_default_object(generated)
        cdo.set_editor_property("default_pawn_class", pawn_class)
        log(f"Set DefaultPawnClass → {pawn_class.get_name()}")
    except Exception as ex:
        log(
            f"WARN: could not set DefaultPawnClass via Python ({ex}). "
            f"Open {asset_path} and set Default Pawn Class = {KAYLEIGH_CLASS}."
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


def try_load_kayleigh_class_asset():
    """If the placed actor is awkward, try loading the BP asset class directly."""
    candidates = (
        f"/Game/MetaHumans/MHC_Kayleigh/{KAYLEIGH_CLASS}",
        f"/Game/MetaHumans/{KAYLEIGH_CLASS}",
        f"/Game/Characters/{KAYLEIGH_CLASS}",
        f"/Game/{KAYLEIGH_CLASS}",
    )
    for path in candidates:
        asset = unreal.EditorAssetLibrary.load_asset(path)
        if asset is None:
            continue
        try:
            if hasattr(asset, "generated_class"):
                return asset.generated_class()
        except Exception:
            pass
        try:
            return asset.get_class()
        except Exception:
            continue
    return None


def main():
    log(f"Looking for {KAYLEIGH_CLASS} in /Game/Home (not Victoria)…")
    candidates = find_kayleigh_candidates()
    if not candidates:
        log(
            f"FAIL: no {KAYLEIGH_CLASS} (or Kayleigh-named pawn) found in the level. "
            "Open /Game/Home, confirm BP_MHC_Kayleigh is placed, then re-run."
        )
        return

    for i, c in enumerate(candidates):
        log(
            f"  candidate[{i}] label={actor_label(c)} class={class_name(c)} "
            f"pawn={is_pawn_like(c)} loc={c.get_actor_location()}"
        )
        log(f"  candidate[{i}] hierarchy: {class_hierarchy(c)}")

    chosen = candidates[0]
    log(f"Using {actor_label(chosen)} ({class_name(chosen)}) as PIE player body")
    log(f"Hierarchy: {class_hierarchy(chosen)}")
    ensure_player_start_near(chosen)

    pawn_class = None
    if is_pawn_like(chosen):
        pawn_class = chosen.get_class()
    else:
        log(
            f"NOTE: placed {class_name(chosen)} is not a Pawn/Character — "
            "PIE cannot possess a bare MetaHuman Actor. Trying asset class load…"
        )
        pawn_class = try_load_kayleigh_class_asset()
        if pawn_class is None:
            log(
                "FAIL: BP_MHC_Kayleigh is Actor-only. Do NOT reparent MHC and do NOT use Victoria. "
                "Run REX-01 pipeline instead: tools/ue_nav/run_rex_pie_possess_kayleigh.ps1 "
                "(creates /Game/Characters/BP_KayleighCharacter)."
            )
            return

    # Hard refuse Victoria as player DefaultPawn (BOB failure mode).
    pawn_name = str(pawn_class.get_name()) if pawn_class is not None else ""
    if is_victoria(chosen) or any(
        m.lower() in pawn_name.lower()
        for m in ("Victoria", "VictoriaAvatar", "BP_VictoriaCharacter")
    ):
        log(
            f"FAIL HARD: refused player pawn class '{pawn_name}'. "
            "Victoria is AI-only. Use BP_KayleighCharacter via REX pipeline."
        )
        return
    if "kayleigh" not in pawn_name.lower():
        log(
            f"FAIL HARD: player pawn class '{pawn_name}' does not contain Kayleigh. Aborting."
        )
        return

    gm = load_or_create_game_mode(pawn_class)
    set_world_game_mode(gm)
    unreal.EditorLevelLibrary.save_current_level()
    log(
        f"DONE. Press Play (PIE). You should spawn as {KAYLEIGH_CLASS}, not the flying ghost. "
        "Victoria remains AI-possessed separately."
    )


if __name__ == "__main__":
    main()
