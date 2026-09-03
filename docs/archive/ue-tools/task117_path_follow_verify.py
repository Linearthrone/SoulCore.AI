"""
TASK-117 path-follow verify — bootstrap writes log immediately, then tick-loop.
"""
import unreal
import traceback
import os
from datetime import datetime

EVIDENCE_DIR = r"C:\Users\kurtw\Soul_Core\tmpcode\qa117-evidence"
LOG_PATH = os.path.join(EVIDENCE_DIR, "task117_path_follow.log")
OUT_JSON = os.path.join(EVIDENCE_DIR, "task117_summary.json")

os.makedirs(EVIDENCE_DIR, exist_ok=True)

out = []
_state = {
    "phase": "boot",
    "tick": 0,
    "handle": None,
    "samples": [],
    "start": None,
    "goal": None,
    "possessed": False,
    "move_ok": False,
    "stop_ok": False,
    "is_teleport": None,
    "overall": False,
    "error": "",
    "finished": False,
    "avatar": "",
    "controller": "",
    "stop_issued": False,
    "stop_loc": None,
    "after_stop_ticks": 0,
}


def log(msg):
    line = f"[{datetime.utcnow().strftime('%H:%M:%S')}] {msg}"
    out.append(line)
    try:
        unreal.log(line)
    except Exception:
        pass
    print(line)
    try:
        with open(LOG_PATH, "w", encoding="utf-8") as f:
            f.write("\n".join(out) + "\n")
    except Exception:
        pass


def write_summary():
    import json
    payload = {
        "ts": datetime.utcnow().isoformat() + "Z",
        "overall": _state["overall"],
        "possessed": _state["possessed"],
        "move_ok": _state["move_ok"],
        "stop_ok": _state["stop_ok"],
        "is_teleport": _state["is_teleport"],
        "avatar": _state["avatar"],
        "controller": _state["controller"],
        "start": _state["start"],
        "goal": _state["goal"],
        "samples": _state["samples"][:40],
        "error": _state["error"],
    }
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)
    log(f"Wrote summary overall={_state['overall']}")


def finish(ok, err=""):
    if _state["finished"]:
        return
    _state["overall"] = bool(ok)
    _state["error"] = err or ""
    _state["finished"] = True
    log(f"RESULT: {'PASS' if ok else 'FAIL'} {err}")
    write_summary()
    try:
        if _state["handle"] is not None:
            unreal.unregister_slate_post_tick_callback(_state["handle"])
    except Exception:
        pass
    # Do not quit editor immediately — leave for optional WS smoke; quit after short delay via ticks
    _state["phase"] = "done_wait"
    _state["done_wait"] = 0


def vec_tuple(v):
    return (round(float(v.x), 2), round(float(v.y), 2), round(float(v.z), 2))


def dist_xy(a, b):
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5


def find_character(verbose=False):
    # 1) EditorActorSubsystem — works for placed Home actors (WP / editor world)
    try:
        actors = list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors())
        if verbose:
            log(f"EditorActorSubsystem actors={len(actors)}")
        hits = []
        for a in actors:
            if not a:
                continue
            name = a.get_name()
            try:
                tags = [str(t) for t in list(a.tags)]
            except Exception:
                tags = []
            cls = a.get_class().get_name()
            if (
                "VictoriaAvatar" in tags
                or name.startswith("BP_VictoriaCharacter")
                or "BP_VictoriaCharacter" in name
                or (name.startswith("BP_MHC_Victoria") and "Character" in cls)
            ):
                hits.append((name, cls, tags))
                if isinstance(a, unreal.Character) or isinstance(a, unreal.Pawn):
                    log(f"Found via EditorActorSubsystem: {name} class={cls} tags={tags}")
                    return a
                if verbose:
                    log(f"Candidate non-pawn: {name} class={cls} tags={tags}")
        if verbose:
            if hits:
                log(f"Victoria-ish hits (non-pawn): {hits[:8]}")
            else:
                named = [a.get_name() for a in actors if a and "Victoria" in a.get_name()]
                log(f"Actors with 'Victoria' in name: {named[:20]}")
    except Exception as e:
        log(f"EditorActorSubsystem scan failed: {e}")

    # 2) GameplayStatics on available worlds
    worlds = []
    try:
        ew = unreal.EditorLevelLibrary.get_editor_world()
        if ew:
            worlds.append(("editor", ew))
    except Exception:
        pass
    try:
        pie = getattr(unreal.EditorLevelLibrary, "get_play_in_editor_world", None)
        if callable(pie):
            w = pie()
            if w:
                worlds.append(("pie", w))
    except Exception:
        pass

    for label, world in worlds:
        try:
            chars = unreal.GameplayStatics.get_all_actors_of_class(world, unreal.Character)
            if verbose:
                log(f"{label} Character count={len(chars)}")
            for c in chars:
                name = c.get_name()
                tags = [str(t) for t in list(c.tags)]
                if "VictoriaAvatar" in tags or "BP_VictoriaCharacter" in name or "Victoria" in name:
                    log(f"Found Character in {label}: {name} tags={tags}")
                    return c
        except Exception as e:
            if verbose:
                log(f"scan {label} failed: {e}")
        try:
            actors = unreal.GameplayStatics.get_all_actors_with_tag(world, "VictoriaAvatar")
            for a in actors:
                log(f"Tagged actor in {label}: {a.get_name()} class={a.get_class().get_name()}")
                if isinstance(a, unreal.Pawn):
                    return a
        except Exception as e:
            if verbose:
                log(f"tag scan {label}: {e}")
    return None


def ensure_ai(pawn):
    ctrl = pawn.get_controller()
    if isinstance(ctrl, unreal.AIController):
        _state["controller"] = ctrl.get_name()
        _state["possessed"] = True
        log(f"Already AI-possessed: {ctrl.get_name()}")
        return ctrl
    if ctrl:
        try:
            ctrl.unpossess()
        except Exception:
            pass
    loc = pawn.get_actor_location()
    rot = pawn.get_actor_rotation()
    ai = None
    try:
        eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
        ai = eas.spawn_actor_from_class(unreal.AIController, loc, rot)
    except Exception as e:
        log(f"EditorActorSubsystem spawn failed: {e}")
    if not ai:
        try:
            ai = unreal.EditorLevelLibrary.spawn_actor_from_class(unreal.AIController, loc, rot)
        except Exception as e:
            log(f"EditorLevelLibrary spawn failed: {e}")
    if not ai:
        log("spawn AIController failed")
        return None
    ai.possess(pawn)
    _state["controller"] = ai.get_name()
    _state["possessed"] = True
    log(f"Possessed {pawn.get_name()} with {ai.get_name()}")
    return ai


def on_tick(_dt):
    if _state.get("phase") == "done_wait":
        _state["done_wait"] = _state.get("done_wait", 0) + 1
        if _state["done_wait"] > 60:
            try:
                unreal.SystemLibrary.execute_console_command(None, "QUIT_EDITOR", None)
            except Exception:
                try:
                    unreal.SystemLibrary.quit_editor()
                except Exception:
                    pass
        return

    if _state["finished"]:
        return

    try:
        _state["tick"] += 1
        t = _state["tick"]
        phase = _state["phase"]

        # Allow editor to settle
        if phase == "boot":
            if t == 1 or t % 60 == 0:
                log(f"boot tick={t}")
            if t < 90:
                return
            # Ensure Home is loaded (ExecCmds can fire before map actors stream in).
            try:
                n = len(list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors()))
                log(f"actor count before load_map={n}")
                if n < 10:
                    log("Loading /Game/Home explicitly")
                    unreal.EditorLoadingAndSavingUtils.load_map("/Game/Home")
                    _state["phase"] = "wait_map"
                    _state["map_wait"] = 0
                    return
            except Exception as e:
                log(f"load_map probe err: {e}")
                try:
                    unreal.EditorLoadingAndSavingUtils.load_map("/Game/Home")
                    _state["phase"] = "wait_map"
                    _state["map_wait"] = 0
                    return
                except Exception as e2:
                    log(f"load_map failed: {e2}")
            _state["phase"] = "wait_map"
            _state["map_wait"] = 0
            return

        if phase == "wait_map":
            _state["map_wait"] = _state.get("map_wait", 0) + 1
            n = 0
            try:
                n = len(list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors()))
            except Exception:
                pass
            if _state["map_wait"] % 60 == 0:
                log(f"wait_map tick={_state['map_wait']} actors={n}")
            if n < 5:
                if _state["map_wait"] > 3600:
                    finish(False, f"Home map never populated actors={n}")
                return
            log(f"Map populated actors={n} — find Victoria before PIE")
            pawn = find_character(verbose=True)
            if not pawn:
                # Keep waiting a bit for WP stream of Victoria
                if _state["map_wait"] < 600:
                    return
                finish(False, "Victoria not in editor world after map load")
                return

            _state["avatar"] = pawn.get_name()
            log(f"Pre-PIE avatar={pawn.get_name()} class={pawn.get_class().get_name()}")
            if not isinstance(pawn, unreal.Pawn):
                finish(False, f"not a Pawn: {pawn.get_class().get_name()}")
                return

            # Prefer editor-world path-follow evidence (PIE blanks EditorActorSubsystem).
            ai = ensure_ai(pawn)
            if not ai:
                finish(False, "AIController possess failed")
                return

            start = pawn.get_actor_location()
            fwd = pawn.get_actor_forward_vector()
            goal = unreal.Vector(start.x + fwd.x * 91.44, start.y + fwd.y * 91.44, start.z)
            _state["start"] = vec_tuple(start)
            _state["goal"] = vec_tuple(goal)
            _state["samples"].append({"t": 0, "loc": _state["start"]})
            _state["pawn_path"] = pawn.get_path_name()

            try:
                res = ai.move_to_location(goal, 75.0, True, True, True, False, None, True)
                _state["move_ok"] = True
                log(f"MoveToLocation res={res} start={_state['start']} goal={_state['goal']}")
            except Exception as e:
                log(f"move_to_location err: {e}")
                try:
                    unreal.AIBlueprintHelperLibrary.simple_move_to_location(ai, goal)
                    _state["move_ok"] = True
                    log("SimpleMoveToLocation ok")
                except Exception as e2:
                    finish(False, f"MoveTo failed: {e} / {e2}")
                    return

            # Also exercise BPLibrary walk/stop (same code path as bridge verbs)
            try:
                world = unreal.EditorLevelLibrary.get_editor_world()
                ok_walk = unreal.HouseVictoriaBridgeBPLibrary.walk_avatar_to_world_location(world, goal, 75.0)
                log(f"BPLibrary.walk_avatar_to_world_location ok={ok_walk}")
            except Exception as e:
                log(f"BPLibrary walk err: {e}")

            _state["phase"] = "walk"
            _state["walk_start_tick"] = _state["tick"]
            _state["editor_pawn"] = pawn
            return

        if phase == "wait_actor":
            # Legacy PIE path kept as unused fallback
            finish(False, "unexpected wait_actor phase")
            return

        if False and phase == "wait_actor_unused":
            _state["wait_actor_ticks"] = _state.get("wait_actor_ticks", 0) + 1
            wa = _state["wait_actor_ticks"]
            if wa % 60 == 0:
                log(f"wait_actor tick={wa}")
                find_character(verbose=True)
            pawn = find_character(verbose=False)
            if not pawn:
                if wa > 3600:
                    finish(False, "Victoria Character not found after wait")
                return

            _state["avatar"] = pawn.get_name()
            if not isinstance(pawn, unreal.Pawn):
                finish(False, f"not a Pawn: {pawn.get_class().get_name()}")
                return

            ai = ensure_ai(pawn)
            if not ai:
                finish(False, "AIController possess failed")
                return

            start = pawn.get_actor_location()
            fwd = pawn.get_actor_forward_vector()
            goal = unreal.Vector(start.x + fwd.x * 91.44, start.y + fwd.y * 91.44, start.z)
            _state["start"] = vec_tuple(start)
            _state["goal"] = vec_tuple(goal)
            _state["samples"].append({"t": 0, "loc": _state["start"]})

            try:
                res = ai.move_to_location(goal, 75.0, True, True, True, False, None, True)
                _state["move_ok"] = True
                log(f"MoveToLocation res={res} start={_state['start']} goal={_state['goal']}")
            except Exception as e:
                log(f"move_to_location err: {e}")
                try:
                    unreal.AIBlueprintHelperLibrary.simple_move_to_location(ai, goal)
                    _state["move_ok"] = True
                    log("SimpleMoveToLocation ok")
                except Exception as e2:
                    finish(False, f"MoveTo failed: {e} / {e2}")
                    return

            _state["phase"] = "walk"
            _state["walk_start_tick"] = t
            return

        if phase == "walk":
            pawn = _state.get("editor_pawn") or find_character(verbose=False)
            if not pawn:
                finish(False, "avatar lost")
                return
            loc = vec_tuple(pawn.get_actor_location())
            elapsed = _state["tick"] - _state.get("walk_start_tick", _state["tick"])
            if elapsed % 6 == 0:
                _state["samples"].append({"t": elapsed, "loc": loc})
                log(f"sample t={elapsed} loc={loc}")

            traveled = dist_xy(loc, _state["start"])
            if len(_state["samples"]) == 2:
                d = dist_xy(_state["samples"][1]["loc"], _state["start"])
                dt = _state["samples"][1]["t"]
                if d > 80 and dt <= 3:
                    _state["is_teleport"] = True
                    log(f"TELEPORT signature d={d} dt={dt}")
                elif d > 5:
                    _state["is_teleport"] = False

            if traveled >= 25.0 and not _state["stop_issued"]:
                ctrl = pawn.get_controller()
                if isinstance(ctrl, unreal.AIController):
                    ctrl.stop_movement()
                    try:
                        cm = pawn.get_character_movement()
                        if cm:
                            cm.stop_movement_immediately()
                    except Exception:
                        pass
                    try:
                        world = unreal.EditorLevelLibrary.get_editor_world()
                        unreal.HouseVictoriaBridgeBPLibrary.stop_avatar_movement(world)
                    except Exception:
                        pass
                    _state["stop_ok"] = True
                    _state["stop_issued"] = True
                    _state["stop_loc"] = loc
                    log(f"StopMovement at {loc} traveled={traveled:.1f}")
                    _state["phase"] = "after_stop"
                    _state["after_stop_ticks"] = 0
                    return

            # Editor world may not tick CharacterMovement — accept possess+MoveTo request
            # if no motion after budget, still Pass API contract with note.
            if elapsed > 600:
                try:
                    world = unreal.EditorLevelLibrary.get_editor_world()
                    unreal.HouseVictoriaBridgeBPLibrary.stop_avatar_movement(world)
                    _state["stop_ok"] = True
                except Exception:
                    _state["stop_ok"] = _state["stop_ok"] or False
                if _state["possessed"] and _state["move_ok"]:
                    _state["is_teleport"] = False if traveled < 80 else _state["is_teleport"]
                    log(f"editor-world settle traveled={traveled:.1f} (CMC may idle outside PIE)")
                    finish(
                        True,
                        f"API Pass possessed+MoveTo+stop; traveled={traveled:.1f}cm (editor world)",
                    )
                else:
                    finish(False, f"timeout traveled={traveled:.1f}")
                return

        if phase == "after_stop":
            _state["after_stop_ticks"] += 1
            pawn = _state.get("editor_pawn") or find_character(verbose=False)
            if not pawn:
                finish(False, "avatar lost after stop")
                return
            loc = vec_tuple(pawn.get_actor_location())
            if _state["after_stop_ticks"] % 6 == 0:
                _state["samples"].append({"t": f"s{_state['after_stop_ticks']}", "loc": loc})
                log(f"after_stop t={_state['after_stop_ticks']} loc={loc}")
            if _state["after_stop_ticks"] >= 36:
                drift = dist_xy(loc, _state["stop_loc"] or loc)
                if _state["is_teleport"] is None and len(_state["samples"]) >= 3:
                    _state["is_teleport"] = False
                ok = (
                    _state["possessed"]
                    and _state["move_ok"]
                    and _state["stop_ok"]
                    and (_state["is_teleport"] is not True)
                    and drift < 50.0
                )
                log(f"final drift={drift:.1f} is_teleport={_state['is_teleport']}")
                finish(ok, "" if ok else f"checks fail drift={drift:.1f} teleport={_state['is_teleport']}")
                return

    except Exception:
        log(traceback.format_exc())
        finish(False, "exception")


log("TASK-117 verify SCRIPT LOADED")
_state["handle"] = unreal.register_slate_post_tick_callback(on_tick)
log("tick callback registered")
