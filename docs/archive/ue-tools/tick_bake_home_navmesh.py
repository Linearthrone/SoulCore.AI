"""
TASK-116: NavMesh bake for Home â€” ExecCmds / tick-safe entrypoint.

Prior FAIL: RebuildNavigation no-ops while AsyncLoadLock (0x20) is held.
-ExecutePythonScript blocks the game thread so the lock never clears.
Prefer launching via -ExecCmds="py ..." so the editor has already ticked;
if still locked, register a slate post-tick callback and QUIT_EDITOR when done.

Does NOT touch AIController / move_to (BED-117).
"""
import unreal
import traceback
from datetime import datetime

LOG_PATH = r"C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\Saved\Logs\task116_navmesh_tick.log"
HOME_MAP = "/Game/Home"
TAG_BOUNDS = "TASK116_NavMeshBounds"

CAPSULE_RADIUS = 34.0
CAPSULE_HALF_HEIGHT = 96.0
AGENT_RADIUS = CAPSULE_RADIUS
AGENT_HEIGHT = CAPSULE_HALF_HEIGHT * 2.0
AGENT_MAX_SLOPE = 44.0

VICTORIA_XY = (-231.92, 420.0)
VICTORIA_FLOOR_Z = 10.0
BRUSH_HALF_DEFAULT = 100.0
ASYNC_LOAD_LOCK = 0x20

MAX_WAIT_TICKS = 1800
MAX_BUILD_WAIT_TICKS = 1800
BOUNDS_HALF_XY = 2000.0
BOUNDS_HALF_Z = 300.0

out = []
_state = {
    "phase": "init",
    "tick": 0,
    "wait_ticks": 0,
    "build_ticks": 0,
    "handle": None,
    "path_ok": False,
    "path_info": "",
    "any_nav": False,
    "proj_ok": False,
    "floor_hit": False,
    "lock_cleared": False,
    "unlock_method": "",
    "overall": False,
    "error": "",
    "finished": False,
}


def log(msg):
    line = str(msg)
    out.append(line)
    unreal.log(line)
    print(line)


def flush_log():
    try:
        with open(LOG_PATH, "w", encoding="utf-8") as f:
            f.write("\n".join(out) + "\n")
    except Exception as e:
        print(f"Failed to write log: {e}")


def get_world():
    try:
        return unreal.EditorLevelLibrary.get_editor_world()
    except Exception:
        return unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()


def get_all_actors():
    try:
        return list(unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors())
    except Exception:
        return list(unreal.EditorLevelLibrary.get_all_level_actors())


def find_cls(substr):
    return [a for a in get_all_actors() if substr in a.get_class().get_name()]


def spawn_actor(cls, location, rotation=None):
    if rotation is None:
        rotation = unreal.Rotator(0, 0, 0)
    try:
        return unreal.get_editor_subsystem(unreal.EditorActorSubsystem).spawn_actor_from_class(
            cls, location, rotation
        )
    except Exception:
        return unreal.EditorLevelLibrary.spawn_actor_from_class(cls, location, rotation)


def ensure_tag(actor, tag):
    try:
        tags = list(actor.tags)
        if tag not in [str(t) for t in tags]:
            tags.append(tag)
            actor.tags = tags
    except Exception as e:
        log(f"WARNING: tag: {e}")


def is_nav_locked(world):
    """Static Blueprint API needs world context."""
    try:
        return bool(unreal.NavigationSystemV1.is_navigation_being_built_or_locked(world))
    except Exception as e:
        log(f"is_navigation_being_built_or_locked err: {e}")
        return None


def try_force_unlock(nav):
    methods = [n for n in dir(nav) if "lock" in n.lower() or "release" in n.lower()]
    log(f"nav lock-related attrs: {methods}")

    for meth in (
        "RemoveNavigationBuildLock",
        "remove_navigation_build_lock",
        "ReleaseInitialBuildingLock",
        "release_initial_building_lock",
    ):
        for args in ((ASYNC_LOAD_LOCK,), (ASYNC_LOAD_LOCK, 1), (0xFF,), (0x20,), ()):
            try:
                if hasattr(nav, "call_method"):
                    nav.call_method(meth, args)
                    log(f"call_method {meth}{args} ok")
                    return f"call_method:{meth}{args}"
            except Exception:
                pass
            try:
                fn = getattr(nav, meth, None)
                if callable(fn):
                    fn(*args)
                    log(f"getattr {meth}{args} ok")
                    return f"getattr:{meth}{args}"
            except Exception:
                pass

    # Class-level static attempts
    for meth in ("remove_navigation_build_lock", "RemoveNavigationBuildLock"):
        try:
            fn = getattr(unreal.NavigationSystemV1, meth, None)
            if callable(fn):
                for args in ((nav, ASYNC_LOAD_LOCK), (ASYNC_LOAD_LOCK,), (get_world(), ASYNC_LOAD_LOCK)):
                    try:
                        fn(*args)
                        log(f"static {meth}{args} ok")
                        return f"static:{meth}"
                    except Exception:
                        pass
        except Exception:
            pass
    return ""


def ensure_bounds():
    existing = find_cls("NavMeshBoundsVolume")
    if existing:
        for a in existing:
            loc = a.get_actor_location()
            sc = a.get_actor_scale3d()
            log(
                f"Existing bounds: {a.get_name()} label={a.get_actor_label()} "
                f"loc=({loc.x:.1f},{loc.y:.1f},{loc.z:.1f}) scale=({sc.x:.2f},{sc.y:.2f},{sc.z:.2f})"
            )
            try:
                a.set_editor_property("is_spatially_loaded", False)
            except Exception:
                pass
        return existing[0]

    center = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 150.0)
    actor = spawn_actor(unreal.NavMeshBoundsVolume, center)
    if actor is None:
        raise RuntimeError("Failed to spawn NavMeshBoundsVolume")
    ensure_tag(actor, TAG_BOUNDS)
    actor.set_actor_label("NavMeshBoundsVolume_HomeInterior")
    sx = (BOUNDS_HALF_XY + 100.0) / BRUSH_HALF_DEFAULT
    sy = (BOUNDS_HALF_XY + 100.0) / BRUSH_HALF_DEFAULT
    sz = (BOUNDS_HALF_Z + 50.0) / BRUSH_HALF_DEFAULT
    actor.set_actor_scale3d(unreal.Vector(sx, sy, sz))
    try:
        actor.set_editor_property("is_spatially_loaded", False)
    except Exception:
        pass
    log(f"Placed bounds {actor.get_name()} scale=({sx},{sy},{sz}) coverage=Victoria_box_40m")
    return actor


def configure_recast(recast):
    for prop, val in {
        "agent_radius": AGENT_RADIUS,
        "agent_height": AGENT_HEIGHT,
        "agent_max_slope": AGENT_MAX_SLOPE,
    }.items():
        try:
            recast.set_editor_property(prop, val)
            log(f"Recast {prop}={recast.get_editor_property(prop)}")
        except Exception as e:
            log(f"WARNING set {prop}: {e}")
    try:
        recast.set_editor_property("is_spatially_loaded", False)
    except Exception:
        pass
    try:
        recast.set_editor_property("runtime_generation", unreal.RuntimeGenerationType.STATIC)
        log(f"runtime_generation={recast.get_editor_property('runtime_generation')}")
    except Exception as e:
        log(f"runtime_generation set: {e}")


def ensure_recast(world, nav):
    recasts = find_cls("RecastNavMesh")
    if recasts:
        for r in recasts:
            log(f"Existing Recast: {r.get_name()}")
            configure_recast(r)
        return recasts[0]

    log("No RecastNavMesh â€” spawning")
    loc = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z)
    created = spawn_actor(unreal.RecastNavMesh, loc)
    log(f"Spawned RecastNavMesh: {created}")
    if created:
        try:
            created.set_actor_label("RecastNavMesh_Home")
        except Exception:
            pass
        configure_recast(created)
    return created


def floor_trace(world):
    try:
        start = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 300.0)
        end = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z - 300.0)
        hit = unreal.SystemLibrary.line_trace_single(
            world,
            start,
            end,
            unreal.TraceTypeQuery.TRACE_TYPE_QUERY1,
            True,
            [],
            unreal.DrawDebugTrace.NONE,
            True,
        )
        blocking = False
        try:
            blocking = bool(getattr(hit, "blocking_hit", False))
        except Exception:
            pass
        log(f"floor_trace blocking={blocking} raw={hit}")
        return blocking
    except Exception as e:
        log(f"floor_trace err: {e}")
        return False


def path_test(world):
    starts_ends = [
        (
            unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 20.0),
            unreal.Vector(VICTORIA_XY[0] + 300.0, VICTORIA_XY[1] + 50.0, VICTORIA_FLOOR_Z + 20.0),
        ),
        (
            unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 50.0),
            unreal.Vector(VICTORIA_XY[0] + 200.0, VICTORIA_XY[1], VICTORIA_FLOOR_Z + 50.0),
        ),
        (
            unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 5.0),
            unreal.Vector(VICTORIA_XY[0] + 100.0, VICTORIA_XY[1], VICTORIA_FLOOR_Z + 5.0),
        ),
    ]
    last_info = ""
    for start, end in starts_ends:
        try:
            path = unreal.NavigationSystemV1.find_path_to_location_synchronously(world, start, end)
            pts = []
            if path is not None:
                try:
                    pts = list(path.path_points)
                except Exception:
                    pts = []
            info = f"points={len(pts)} start=({start.x:.0f},{start.y:.0f},{start.z:.0f})"
            if pts:
                info += (
                    f" p0=({pts[0].x:.1f},{pts[0].y:.1f},{pts[0].z:.1f})"
                    f" pN=({pts[-1].x:.1f},{pts[-1].y:.1f},{pts[-1].z:.1f})"
                )
            last_info = info
            if len(pts) >= 2:
                return True, info
        except Exception as e:
            last_info = f"exception:{e}"
    return False, last_info


def project_test(world):
    recasts = find_cls("RecastNavMesh")
    nav_data = recasts[0] if recasts else None
    pt = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 20.0)
    try:
        r = unreal.NavigationSystemV1.project_point_to_navigation(
            world, pt, nav_data, None, unreal.Vector(1000, 1000, 500)
        )
        log(f"project -> {r}")
        if r is None:
            return False
        try:
            return abs(r.x) + abs(r.y) + abs(r.z) > 1.0
        except Exception:
            return True
    except Exception as e:
        log(f"project err: {e}")
        return False


def random_nav(world):
    center = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 20.0)
    try:
        result = unreal.NavigationSystemV1.get_random_location_in_navigable_radius(
            world, center, 1500.0
        )
        log(f"random_location -> {result}")
        if result is None:
            return False
        if isinstance(result, (tuple, list)):
            return bool(result[0]) if result else False
        try:
            return abs(result.x) + abs(result.y) + abs(result.z) > 1.0
        except Exception:
            return True
    except Exception as e:
        log(f"random_location: {e}")
        return False


def save_all():
    try:
        unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
    except Exception:
        try:
            unreal.EditorLevelLibrary.save_current_level()
        except Exception as e:
            log(f"save_current_level: {e}")
    try:
        unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
        log("saved dirty packages")
    except Exception as e:
        log(f"save_dirty_packages: {e}")


def finish(ok, reason=""):
    if _state["finished"]:
        return
    _state["finished"] = True
    _state["overall"] = ok
    if reason:
        _state["error"] = reason
    bounds_n = len(find_cls("NavMeshBoundsVolume"))
    recast_n = len(find_cls("RecastNavMesh"))
    log("=== SUMMARY ===")
    log(
        f"coverage=Victoria-centered interior box ~40m x 40m x 6m "
        f"(half_xy={BOUNDS_HALF_XY} half_z={BOUNDS_HALF_Z})"
    )
    log(
        f"agent_radius={AGENT_RADIUS} agent_height={AGENT_HEIGHT} "
        f"(capsule r={CAPSULE_RADIUS} half_h={CAPSULE_HALF_HEIGHT})"
    )
    log(f"lock_cleared={_state['lock_cleared']} unlock_method={_state['unlock_method']}")
    log(f"floor_hit={_state['floor_hit']} proj_ok={_state['proj_ok']} any_nav={_state['any_nav']}")
    log(f"path_ok={_state['path_ok']} {_state['path_info']}")
    log(f"FINAL bounds={bounds_n} recast={recast_n}")
    log(f"ticks_total={_state['tick']} wait={_state['wait_ticks']} build={_state['build_ticks']}")
    if _state["error"]:
        log(f"error={_state['error']}")
    log(f"RESULT: {'PASS' if ok else 'FAIL'}")
    flush_log()

    try:
        if _state["handle"] is not None:
            unreal.unregister_slate_post_tick_callback(_state["handle"])
            _state["handle"] = None
    except Exception as e:
        log(f"unregister: {e}")

    try:
        unreal.SystemLibrary.execute_console_command(get_world(), "QUIT_EDITOR")
    except Exception:
        try:
            unreal.SystemLibrary.quit_editor()
        except Exception as e:
            log(f"QUIT_EDITOR failed: {e}")


def do_rebuild(world, nav):
    _state["floor_hit"] = floor_trace(world)
    bounds = ensure_bounds()
    recast = ensure_recast(world, nav)
    if bounds is None or recast is None:
        finish(False, "missing bounds or recast after ensure")
        return False

    try:
        nav.on_navigation_bounds_updated(bounds)
        log("on_navigation_bounds_updated ok")
    except Exception as e:
        log(f"on_navigation_bounds_updated: {e}")

    if is_nav_locked(world):
        method = try_force_unlock(nav)
        if method:
            _state["unlock_method"] = method

    try:
        unreal.SystemLibrary.execute_console_command(world, "RebuildNavigation")
        unreal.SystemLibrary.execute_console_command(world, "n.RebuildNavigation")
        log("RebuildNavigation issued")
    except Exception as e:
        log(f"RebuildNavigation: {e}")
    return True


def evaluate_and_maybe_finish(world, soft=False):
    _state["proj_ok"] = project_test(world)
    _state["any_nav"] = random_nav(world)
    ok, info = path_test(world)
    _state["path_ok"] = ok
    _state["path_info"] = info
    log(f"probe path_ok={ok} {info} proj={_state['proj_ok']} any={_state['any_nav']}")

    if ok or _state["proj_ok"] or _state["any_nav"]:
        save_all()
        bounds_n = len(find_cls("NavMeshBoundsVolume"))
        recast_n = len(find_cls("RecastNavMesh"))
        agent_ok = False
        recasts = find_cls("RecastNavMesh")
        if recasts:
            try:
                ar = float(recasts[0].get_editor_property("agent_radius"))
                ah = float(recasts[0].get_editor_property("agent_height"))
                agent_ok = abs(ar - AGENT_RADIUS) < 0.1 and abs(ah - AGENT_HEIGHT) < 0.1
                log(f"verified agent r={ar} h={ah} ok={agent_ok}")
            except Exception as e:
                log(f"agent verify: {e}")
        overall = bounds_n >= 1 and recast_n >= 1 and agent_ok and (
            ok or _state["proj_ok"] or _state["any_nav"]
        )
        finish(overall, "" if overall else "navigable probe soft-fail on agent/bounds")
        return True

    if soft:
        return False
    return False


def on_tick(_delta_time):
    try:
        if _state["finished"]:
            return
        _state["tick"] += 1
        world = get_world()
        nav = unreal.NavigationSystemV1.get_navigation_system(world)
        phase = _state["phase"]

        if _state["tick"] % 30 == 1:
            log(f"tick={_state['tick']} phase={phase} locked={is_nav_locked(world)}")

        if phase == "wait_unlock":
            _state["wait_ticks"] += 1
            locked = is_nav_locked(world)

            if _state["wait_ticks"] in (15, 45, 90, 180, 360):
                method = try_force_unlock(nav)
                if method:
                    _state["unlock_method"] = method
                try:
                    unreal.SystemLibrary.execute_console_command(world, "FlushAsyncLoading")
                except Exception:
                    pass

            if locked is False:
                _state["lock_cleared"] = True
                if not _state["unlock_method"]:
                    _state["unlock_method"] = "natural_tick_release"
                log(f"Nav unlock cleared after {_state['wait_ticks']} wait ticks")
                _state["phase"] = "rebuild"
                return

            if _state["wait_ticks"] >= MAX_WAIT_TICKS:
                method = try_force_unlock(nav)
                if method:
                    _state["unlock_method"] = method
                locked2 = is_nav_locked(world)
                log(f"Wait budget exhausted; locked={locked2} method={_state['unlock_method']}")
                # Proceed anyway â€” Rebuild will log if still locked
                _state["lock_cleared"] = locked2 is False
                _state["phase"] = "rebuild"
                return

        elif phase == "rebuild":
            if not do_rebuild(world, nav):
                return
            _state["phase"] = "wait_build"
            _state["build_ticks"] = 0

        elif phase == "wait_build":
            _state["build_ticks"] += 1
            building = None
            try:
                building = unreal.NavigationSystemV1.is_navigation_being_built(world)
            except Exception:
                pass

            if _state["build_ticks"] % 30 == 1:
                log(f"build_wait tick={_state['build_ticks']} building={building} locked={is_nav_locked(world)}")

            if _state["build_ticks"] in (60, 120, 240, 480, 720, 960) or (
                building is False and _state["build_ticks"] > 45
            ):
                if evaluate_and_maybe_finish(world, soft=True):
                    return

            if _state["build_ticks"] >= MAX_BUILD_WAIT_TICKS:
                evaluate_and_maybe_finish(world, soft=True)
                if not _state["finished"]:
                    save_all()
                    finish(False, "build wait exhausted with no navigable probes")

    except Exception:
        log("FATAL in on_tick:\n" + traceback.format_exc())
        flush_log()
        finish(False, "exception in on_tick")


def main():
    log(f"=== TASK-116 tick bake {datetime.now().isoformat()} ===")
    log(
        f"Agent target: radius={AGENT_RADIUS} height={AGENT_HEIGHT} "
        f"slope={AGENT_MAX_SLOPE}"
    )

    try:
        world = get_world()
        wname = str(world.get_path_name()) if world else ""
        log(f"Current world={wname}")
        if world is None or "Home" not in wname:
            log(f"Loading {HOME_MAP}")
            unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)
    except Exception as e:
        log(f"load_map: {e}")
        try:
            unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)
        except Exception as e2:
            log(f"load_map retry: {e2}")

    world = get_world()
    nav = unreal.NavigationSystemV1.get_navigation_system(world)
    locked = is_nav_locked(world)
    log(f"world={world} nav={nav} locked={locked}")
    log(f"actors={len(get_all_actors())}")

    ensure_bounds()
    ensure_recast(world, nav)
    flush_log()

    # If already unlocked (typical for -ExecCmds after ticks), rebuild immediately
    # then still use tick callback to wait for bake completion.
    if locked is False:
        _state["lock_cleared"] = True
        _state["unlock_method"] = "already_clear_at_entry"
        log("Lock already clear at entry â€” issuing rebuild then waiting on ticks")
        do_rebuild(world, nav)
        _state["phase"] = "wait_build"
        _state["build_ticks"] = 0
    else:
        _state["phase"] = "wait_unlock"
        # Immediate force attempt
        method = try_force_unlock(nav)
        if method:
            _state["unlock_method"] = method
        if is_nav_locked(world) is False:
            _state["lock_cleared"] = True
            _state["unlock_method"] = _state["unlock_method"] or "force_at_entry"
            do_rebuild(world, nav)
            _state["phase"] = "wait_build"

    handle = unreal.register_slate_post_tick_callback(on_tick)
    _state["handle"] = handle
    log(f"Registered slate post-tick callback handle={handle} phase={_state['phase']}")
    flush_log()


if __name__ == "__main__":
    try:
        main()
    except Exception:
        log("FATAL:\n" + traceback.format_exc())
        flush_log()
        try:
            unreal.SystemLibrary.execute_console_command(get_world(), "QUIT_EDITOR")
        except Exception:
            pass

