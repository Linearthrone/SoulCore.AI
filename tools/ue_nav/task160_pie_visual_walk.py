"""
TASK-160 PIE verify — relative ~3 ft walk must produce continuous XY travel + velocity.
Evidence: tmpcode/qa160-evidence/
"""
import unreal
import traceback
import os
import json
from datetime import datetime

EVIDENCE_DIR = r"C:\Users\kurtw\Soul_Core\tmpcode\qa160-evidence"
LOG_PATH = os.path.join(EVIDENCE_DIR, "task160_pie_walk.log")
OUT_JSON = os.path.join(EVIDENCE_DIR, "task160_summary.json")

WALK_FORWARD_CM = 91.44
FLOOR_Z_MIN = 40.0
FLOOR_Z_MAX = 220.0
MIN_TRAVEL_CM = 50.0
MIN_INTERMEDIATE = 3
VELOCITY_WALK_THRESHOLD = 8.0
TELEPORT_CM = 70.0

os.makedirs(EVIDENCE_DIR, exist_ok=True)

out = []
_state = {
    "phase": "boot",
    "tick": 0,
    "handle": None,
    "finished": False,
    "overall": False,
    "error": "",
    "pie_started": False,
    "avatar": "",
    "anim_class": "",
    "controller": "",
    "start": None,
    "goal": None,
    "samples": [],
    "vel_samples": [],
    "z_samples": [],
    "move_issued": False,
    "bridge_walk_ok": None,
    "is_teleport": None,
    "continuous_motion": False,
    "walk_anim_signal": False,
    "feet_on_floor": False,
    "arrived_or_progress": False,
    "stop_ok": False,
    "stop_issued": False,
    "stop_loc": None,
    "max_velocity": 0.0,
    "max_travel": 0.0,
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
    payload = {
        "ts": datetime.utcnow().isoformat() + "Z",
        "overall": _state["overall"],
        "error": _state["error"],
        "pie_started": _state["pie_started"],
        "avatar": _state["avatar"],
        "anim_class": _state["anim_class"],
        "controller": _state["controller"],
        "start": _state["start"],
        "goal": _state["goal"],
        "move_issued": _state["move_issued"],
        "bridge_walk_ok": _state["bridge_walk_ok"],
        "is_teleport": _state["is_teleport"],
        "continuous_motion": _state["continuous_motion"],
        "walk_anim_signal": _state["walk_anim_signal"],
        "feet_on_floor": _state["feet_on_floor"],
        "arrived_or_progress": _state["arrived_or_progress"],
        "stop_ok": _state["stop_ok"],
        "max_velocity": _state["max_velocity"],
        "max_travel": _state["max_travel"],
        "samples": _state["samples"][:80],
        "vel_samples": _state["vel_samples"][:40],
        "z_samples": _state["z_samples"][:40],
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
    _state["phase"] = "done_wait"
    _state["done_wait"] = 0


def vec_tuple(v):
    return (round(float(v.x), 2), round(float(v.y), 2), round(float(v.z), 2))


def dist_xy(a, b):
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5


def get_pie_world():
    try:
        pie = getattr(unreal.EditorLevelLibrary, "get_play_in_editor_world", None)
        if callable(pie):
            w = pie()
            if w:
                return w
    except Exception:
        pass
    try:
        ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
        w = ues.get_game_world()
        if w:
            return w
    except Exception:
        pass
    return None


def find_victoria(world, verbose=False):
    if not world:
        return None
    try:
        chars = list(unreal.GameplayStatics.get_all_actors_of_class(world, unreal.Character))
        for c in chars:
            name = c.get_name()
            try:
                tags = [str(t) for t in list(c.tags)]
            except Exception:
                tags = []
            cls = c.get_class().get_name()
            if "VictoriaAvatar" in tags or "BP_VictoriaCharacter" in name:
                if verbose:
                    log(f"Found PIE Character: {name} class={cls} tags={tags}")
                return c
    except Exception as e:
        log(f"Character scan failed: {e}")
    return None


def read_anim(pawn):
    try:
        mesh = pawn.get_mesh() if hasattr(pawn, "get_mesh") else None
        if mesh is None and hasattr(pawn, "mesh"):
            mesh = pawn.mesh
        if not mesh:
            return ""
        ai = mesh.get_anim_instance()
        if ai:
            return ai.get_class().get_name()
    except Exception:
        pass
    return ""


def read_velocity(pawn):
    try:
        cm = pawn.get_character_movement()
        if cm:
            v = cm.velocity
            speed = (float(v.x) ** 2 + float(v.y) ** 2 + float(v.z) ** 2) ** 0.5
            return speed, vec_tuple(v)
    except Exception:
        pass
    return 0.0, (0.0, 0.0, 0.0)


def start_pie():
    try:
        les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
        les.editor_request_begin_play()
        log("editor_request_begin_play issued")
        return True
    except Exception as e:
        log(f"begin_play failed: {e}")
        return False


def evaluate():
    samples = _state["samples"]
    if len(samples) < 2 or not _state["start"]:
        return
    start = _state["start"]
    if _state["is_teleport"] is None and len(samples) >= 2:
        d = dist_xy(samples[1]["loc"], start)
        dt = samples[1]["t"] if isinstance(samples[1]["t"], int) else 0
        if d > TELEPORT_CM and dt <= 3:
            _state["is_teleport"] = True
        elif d > 5 and dt >= 6:
            _state["is_teleport"] = False

    travels = [dist_xy(s["loc"], start) for s in samples[1:]]
    if travels:
        _state["max_travel"] = max(travels)
    increasing = 0
    last = 0.0
    for t in travels:
        if t > last + 2.0:
            increasing += 1
            last = t
    if increasing >= MIN_INTERMEDIATE and _state["max_travel"] >= MIN_TRAVEL_CM:
        _state["continuous_motion"] = True
        if _state["is_teleport"] is None:
            _state["is_teleport"] = False

    zs = [s["z"] for s in _state["z_samples"]]
    _state["feet_on_floor"] = bool(zs) and all(FLOOR_Z_MIN <= z <= FLOOR_Z_MAX for z in zs)

    if _state["max_velocity"] >= VELOCITY_WALK_THRESHOLD:
        _state["walk_anim_signal"] = True
    elif _state["continuous_motion"] and "Locomotion" in (_state["anim_class"] or ""):
        _state["walk_anim_signal"] = True

    if _state["max_travel"] >= MIN_TRAVEL_CM:
        _state["arrived_or_progress"] = True


def on_tick(_dt):
    if _state.get("phase") == "done_wait":
        _state["done_wait"] = _state.get("done_wait", 0) + 1
        if _state["done_wait"] > 90:
            try:
                unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).editor_request_end_play()
            except Exception:
                pass
            try:
                unreal.SystemLibrary.execute_console_command(None, "QUIT_EDITOR", None)
            except Exception:
                pass
        return

    if _state["finished"]:
        return

    try:
        _state["tick"] += 1
        t = _state["tick"]
        phase = _state["phase"]

        if phase == "boot":
            if t < 60:
                return
            try:
                n = len(list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors()))
                log(f"editor actors={n}")
                if n < 10:
                    unreal.EditorLoadingAndSavingUtils.load_map("/Game/Home")
            except Exception:
                try:
                    unreal.EditorLoadingAndSavingUtils.load_map("/Game/Home")
                except Exception as e:
                    log(f"load_map: {e}")
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
                    finish(False, f"Home never populated actors={n}")
                return
            found = False
            try:
                for a in unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors():
                    if not a:
                        continue
                    name = a.get_name()
                    tags = []
                    try:
                        tags = [str(t) for t in list(a.tags)]
                    except Exception:
                        pass
                    if "VictoriaAvatar" in tags or "BP_VictoriaCharacter" in name:
                        log(f"Pre-PIE avatar: {name}")
                        found = True
                        break
            except Exception as e:
                log(f"pre-PIE: {e}")
            if not found and _state["map_wait"] < 600:
                return
            if not found:
                finish(False, "Victoria not in Home")
                return
            if not start_pie():
                finish(False, "failed to start PIE")
                return
            _state["pie_started"] = True
            _state["phase"] = "wait_pie"
            _state["pie_wait"] = 0
            return

        if phase == "wait_pie":
            _state["pie_wait"] = _state.get("pie_wait", 0) + 1
            world = get_pie_world()
            if _state["pie_wait"] % 60 == 0:
                log(f"wait_pie tick={_state['pie_wait']} world={bool(world)}")
            if not world:
                if _state["pie_wait"] > 1800:
                    finish(False, "PIE world never appeared")
                return
            pawn = find_victoria(world, verbose=True)
            if not pawn:
                if _state["pie_wait"] > 2400:
                    finish(False, "Victoria not in PIE")
                return

            _state["avatar"] = pawn.get_name()
            _state["anim_class"] = read_anim(pawn)
            ctrl = pawn.get_controller()
            if ctrl:
                _state["controller"] = ctrl.get_name()
            loc0 = vec_tuple(pawn.get_actor_location())
            log(
                f"PIE ready avatar={_state['avatar']} anim={_state['anim_class']} "
                f"loc={loc0} ctrl={_state['controller']}"
            )
            if not (FLOOR_Z_MIN <= loc0[2] <= FLOOR_Z_MAX):
                finish(False, f"feet Z={loc0[2]}")
                return

            try:
                unreal.HouseVictoriaBridgeBPLibrary.ensure_avatar_ai_controller(world)
                ctrl = pawn.get_controller()
                if ctrl:
                    _state["controller"] = ctrl.get_name()
            except Exception as e:
                log(f"ensure AI: {e}")

            start = pawn.get_actor_location()
            fwd = pawn.get_actor_forward_vector()
            goal = unreal.Vector(
                start.x + fwd.x * WALK_FORWARD_CM,
                start.y + fwd.y * WALK_FORWARD_CM,
                start.z,
            )
            _state["start"] = vec_tuple(start)
            _state["goal"] = vec_tuple(goal)
            _state["samples"].append({"t": 0, "loc": _state["start"], "speed": 0.0})
            _state["z_samples"].append({"t": 0, "z": _state["start"][2]})

            ok = False
            try:
                ok = unreal.HouseVictoriaBridgeBPLibrary.move_avatar_relative(
                    world, unreal.Vector(WALK_FORWARD_CM, 0.0, 0.0)
                )
                _state["bridge_walk_ok"] = bool(ok)
                log(f"move_avatar_relative ok={ok} start={_state['start']} goal≈{_state['goal']}")
            except Exception as e:
                log(f"move_avatar_relative err: {e}")
                _state["bridge_walk_ok"] = False

            if not ok:
                finish(False, "move_avatar_relative failed")
                return

            _state["move_issued"] = True
            _state["phase"] = "walk"
            _state["walk_start_tick"] = t
            _state["pie_pawn"] = pawn
            _state["pie_world"] = world
            return

        if phase == "walk":
            world = _state.get("pie_world") or get_pie_world()
            pawn = _state.get("pie_pawn") or (find_victoria(world) if world else None)
            if not pawn:
                finish(False, "avatar lost")
                return

            elapsed = t - _state.get("walk_start_tick", t)
            loc = vec_tuple(pawn.get_actor_location())
            speed, vel = read_velocity(pawn)
            if speed > _state["max_velocity"]:
                _state["max_velocity"] = speed

            # Sample every tick early (slate ticks are sparse vs game motion); later every 3.
            if elapsed <= 120 or elapsed % 3 == 0:
                _state["samples"].append({"t": elapsed, "loc": loc, "speed": round(speed, 2)})
                _state["z_samples"].append({"t": elapsed, "z": loc[2]})
                _state["vel_samples"].append({"t": elapsed, "speed": round(speed, 2), "vel": vel})
                if elapsed <= 30 or elapsed % 6 == 0:
                    log(f"sample t={elapsed} loc={loc} speed={speed:.1f}")
                evaluate()

            traveled = dist_xy(loc, _state["start"]) if _state["start"] else 0.0

            # Wait until we have continuous samples before stopping (need vmax + intermediates).
            if (
                not _state["stop_issued"]
                and elapsed >= 60
                and _state["continuous_motion"]
                and _state["max_velocity"] >= VELOCITY_WALK_THRESHOLD
            ):
                try:
                    stop_ok = unreal.HouseVictoriaBridgeBPLibrary.stop_avatar_movement(world)
                    _state["stop_ok"] = bool(stop_ok)
                    log(f"stop ok={stop_ok} at {loc} traveled={traveled:.1f}")
                except Exception as e:
                    log(f"stop err: {e}")
                _state["stop_issued"] = True
                _state["stop_loc"] = loc
                _state["phase"] = "after_stop"
                _state["after_stop_ticks"] = 0
                return

            if elapsed > 900:
                evaluate()
                finish(
                    False,
                    f"timeout travel={traveled:.1f} vmax={_state['max_velocity']:.1f}",
                )
                return

        if phase == "after_stop":
            _state["after_stop_ticks"] += 1
            world = _state.get("pie_world") or get_pie_world()
            pawn = _state.get("pie_pawn") or (find_victoria(world) if world else None)
            if not pawn:
                finish(False, "avatar lost after stop")
                return
            loc = vec_tuple(pawn.get_actor_location())
            speed, _ = read_velocity(pawn)
            if _state["after_stop_ticks"] % 6 == 0:
                _state["samples"].append(
                    {"t": f"s{_state['after_stop_ticks']}", "loc": loc, "speed": round(speed, 2)}
                )
                log(f"after_stop t={_state['after_stop_ticks']} loc={loc} speed={speed:.1f}")

            if _state["after_stop_ticks"] >= 48:
                evaluate()
                drift = dist_xy(loc, _state["stop_loc"] or loc)
                stopped = drift < 40.0 and speed < VELOCITY_WALK_THRESHOLD * 1.5
                _state["stop_ok"] = bool(_state["stop_ok"]) and stopped
                walk_ok = (
                    _state["pie_started"]
                    and _state["move_issued"]
                    and _state["continuous_motion"]
                    and (_state["is_teleport"] is not True)
                    and _state["feet_on_floor"]
                    and _state["walk_anim_signal"]
                    and _state["arrived_or_progress"]
                    and _state["stop_ok"]
                )
                log(
                    f"final travel={_state['max_travel']:.1f} vmax={_state['max_velocity']:.1f} "
                    f"continuous={_state['continuous_motion']} teleport={_state['is_teleport']} "
                    f"feet={_state['feet_on_floor']} anim={_state['walk_anim_signal']} "
                    f"stop={_state['stop_ok']} drift={drift:.1f}"
                )
                if walk_ok:
                    finish(True, "")
                else:
                    finish(
                        False,
                        f"AC fail continuous={_state['continuous_motion']} "
                        f"travel={_state['max_travel']:.1f} vmax={_state['max_velocity']:.1f} "
                        f"stop={_state['stop_ok']}",
                    )
                return

    except Exception:
        log(traceback.format_exc())
        finish(False, "exception")


log("TASK-160 PIE visual walk SCRIPT LOADED")
_state["handle"] = unreal.register_slate_post_tick_callback(on_tick)
log("tick callback registered")
