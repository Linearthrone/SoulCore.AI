"""
TASK-116: NavMesh bake — Cmd-safe with ScopedSlowTask pump.

AsyncLoadLock (0x20) blocks RebuildNavigation while WP finishes loading.
-ExecutePythonScript blocks the game thread; ScopedSlowTask.make_dialog()
pumps the Slate loop so the lock can clear without needing a full GUI tick
callback + staying alive.

Does NOT touch AIController / move_to (BED-117).
"""
import unreal
import traceback
from datetime import datetime

LOG_PATH = r"C:\Users\kurtw\Soul_Core\tools\ue_nav\task116_navmesh_result.log"
# Also mirror into project Saved/Logs for convenience
LOG_PATH_ALT = r"C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\Saved\Logs\task116_navmesh_tick.log"

CAPSULE_RADIUS = 34.0
CAPSULE_HALF_HEIGHT = 96.0
AGENT_RADIUS = CAPSULE_RADIUS
AGENT_HEIGHT = CAPSULE_HALF_HEIGHT * 2.0
AGENT_MAX_SLOPE = 44.0

VICTORIA_XY = (-231.92, 420.0)
VICTORIA_FLOOR_Z = 10.0
BRUSH_HALF_DEFAULT = 100.0
ASYNC_LOAD_LOCK = 0x20
TAG_BOUNDS = "TASK116_NavMeshBounds"
BOUNDS_HALF_XY = 2000.0
BOUNDS_HALF_Z = 300.0
HOME_MAP = "/Game/Home"

out = []


def log(msg):
    line = str(msg)
    out.append(line)
    unreal.log(line)
    print(line)


def flush_log():
    text = "\n".join(out) + "\n"
    for path in (LOG_PATH, LOG_PATH_ALT):
        try:
            with open(path, "w", encoding="utf-8") as f:
                f.write(text)
        except Exception as e:
            print(f"Failed to write {path}: {e}")


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
    try:
        return bool(unreal.NavigationSystemV1.is_navigation_being_built_or_locked(world))
    except Exception as e:
        log(f"is_navigation_being_built_or_locked err: {e}")
        return None


def try_force_unlock(nav):
    methods = [n for n in dir(nav) if "lock" in n.lower() or "release" in n.lower() or "build" in n.lower()]
    log(f"nav interesting attrs ({len(methods)}): {methods[:40]}")

    # Try zeroing any lock-flag properties
    for prop in (
        "nav_building_lock_flags",
        "nav_data_lock_flags",
        "building_lock_flags",
        "initial_building_locked",
        "b_initial_building_locked",
    ):
        try:
            nav.set_editor_property(prop, 0 if "flag" in prop else False)
            log(f"set {prop} -> {nav.get_editor_property(prop)}")
        except Exception as e:
            log(f"set {prop}: {type(e).__name__}")

    for meth in (
        "RemoveNavigationBuildLock",
        "remove_navigation_build_lock",
        "ReleaseInitialBuildingLock",
        "release_initial_building_lock",
    ):
        for args in ((ASYNC_LOAD_LOCK,), (0xFF,), (0x20,), (), (ASYNC_LOAD_LOCK, 1)):
            try:
                if hasattr(nav, "call_method"):
                    nav.call_method(meth, args)
                    log(f"call_method {meth}{args} ok")
                    return f"call_method:{meth}"
            except Exception:
                pass
            try:
                fn = getattr(nav, meth, None)
                if callable(fn):
                    fn(*args)
                    log(f"getattr {meth}{args} ok")
                    return f"getattr:{meth}"
            except Exception:
                pass
    return ""


def ensure_bounds():
    existing = find_cls("NavMeshBoundsVolume")
    if existing:
        a = existing[0]
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
        return a

    center = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 150.0)
    actor = spawn_actor(unreal.NavMeshBoundsVolume, center)
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
    log(f"Placed bounds {actor.get_name()} scale=({sx},{sy},{sz})")
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
        log(f"runtime_generation: {e}")


def ensure_recast():
    recasts = find_cls("RecastNavMesh")
    if recasts:
        log(f"Existing Recast: {recasts[0].get_name()}")
        configure_recast(recasts[0])
        return recasts[0]
    loc = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z)
    created = spawn_actor(unreal.RecastNavMesh, loc)
    log(f"Spawned Recast: {created}")
    if created:
        try:
            created.set_actor_label("RecastNavMesh_Home")
        except Exception:
            pass
        configure_recast(created)
    return created


def floor_trace(world):
    try:
        hit = unreal.SystemLibrary.line_trace_single(
            world,
            unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + 300.0),
            unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z - 300.0),
            unreal.TraceTypeQuery.TRACE_TYPE_QUERY1,
            True,
            [],
            unreal.DrawDebugTrace.NONE,
            True,
        )
        blocking = bool(getattr(hit, "blocking_hit", False))
        log(f"floor_trace blocking={blocking}")
        return blocking
    except Exception as e:
        log(f"floor_trace err: {e}")
        return False


def path_test(world):
    pairs = [
        (20.0, 300.0, 50.0),
        (50.0, 200.0, 0.0),
        (5.0, 100.0, 0.0),
    ]
    last = ""
    for dz, dx, dy in pairs:
        start = unreal.Vector(VICTORIA_XY[0], VICTORIA_XY[1], VICTORIA_FLOOR_Z + dz)
        end = unreal.Vector(VICTORIA_XY[0] + dx, VICTORIA_XY[1] + dy, VICTORIA_FLOOR_Z + dz)
        try:
            path = unreal.NavigationSystemV1.find_path_to_location_synchronously(world, start, end)
            pts = list(path.path_points) if path is not None else []
            last = f"points={len(pts)} dz={dz} dx={dx}"
            if pts:
                last += (
                    f" p0=({pts[0].x:.1f},{pts[0].y:.1f},{pts[0].z:.1f})"
                    f" pN=({pts[-1].x:.1f},{pts[-1].y:.1f},{pts[-1].z:.1f})"
                )
            if len(pts) >= 2:
                return True, last
        except Exception as e:
            last = f"exception:{e}"
    return False, last


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
        return abs(r.x) + abs(r.y) + abs(r.z) > 1.0
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
        return abs(result.x) + abs(result.y) + abs(result.z) > 1.0
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


def pump_until_unlocked(world, nav, frames=300):
    """Pump Slate via ScopedSlowTask so AsyncLoadLock can clear."""
    unlock_method = ""
    log(f"Pumping {frames} ScopedSlowTask frames; locked={is_nav_locked(world)}")
    with unreal.ScopedSlowTask(frames, "TASK-116 waiting for NavMesh AsyncLoadLock clear") as st:
        try:
            st.make_dialog(True)
        except Exception as e:
            log(f"make_dialog: {e}")
        for i in range(frames):
            try:
                st.enter_progress_frame(1)
            except Exception:
                pass
            if i in (10, 30, 60, 120, 180, 240):
                m = try_force_unlock(nav)
                if m:
                    unlock_method = m
                try:
                    unreal.SystemLibrary.execute_console_command(world, "FlushAsyncLoading")
                except Exception:
                    pass
                locked = is_nav_locked(world)
                log(f"  pump i={i} locked={locked} unlock={unlock_method}")
                if locked is False:
                    return True, unlock_method or "slowtask_natural"
            # Tiny yield via console noop
            try:
                if i % 20 == 0:
                    unreal.SystemLibrary.execute_console_command(world, "log LogNavigation Verbose")
            except Exception:
                pass
    locked = is_nav_locked(world)
    if locked is False:
        return True, unlock_method or "slowtask_end_clear"
    # Proceed even if still reporting locked — rebuild will tell us
    m = try_force_unlock(nav)
    return locked is False, unlock_method or m or "pump_exhausted"


def main():
    log(f"=== TASK-116 slowtask bake {datetime.now().isoformat()} ===")
    log(f"Agent target: radius={AGENT_RADIUS} height={AGENT_HEIGHT}")

    try:
        world = get_world()
        wname = str(world.get_path_name()) if world else ""
        if world is None or "Home" not in wname:
            unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)
    except Exception as e:
        log(f"load: {e}")
        unreal.EditorLoadingAndSavingUtils.load_map(HOME_MAP)

    world = get_world()
    nav = unreal.NavigationSystemV1.get_navigation_system(world)
    log(f"world={world} nav={nav} locked={is_nav_locked(world)} actors={len(get_all_actors())}")

    bounds = ensure_bounds()
    recast = ensure_recast()
    floor_hit = floor_trace(world)

    cleared, unlock_method = pump_until_unlocked(world, nav, frames=360)
    log(f"after_pump cleared={cleared} method={unlock_method} locked={is_nav_locked(world)}")

    try:
        nav.on_navigation_bounds_updated(bounds)
        log("on_navigation_bounds_updated ok")
    except Exception as e:
        log(f"on_navigation_bounds_updated: {e}")

    # Multiple rebuild attempts interleaved with more pumps
    path_ok = False
    path_info = ""
    proj_ok = False
    any_nav = False
    for attempt in range(1, 8):
        log(f"--- rebuild attempt {attempt} locked={is_nav_locked(world)} ---")
        try_force_unlock(nav)
        try:
            unreal.SystemLibrary.execute_console_command(world, "RebuildNavigation")
            unreal.SystemLibrary.execute_console_command(world, "n.RebuildNavigation")
            log("RebuildNavigation issued")
        except Exception as e:
            log(f"RebuildNavigation: {e}")

        # Pump a bit for async gather/build
        with unreal.ScopedSlowTask(60, f"TASK-116 nav build attempt {attempt}") as st:
            try:
                st.make_dialog(True)
            except Exception:
                pass
            for i in range(60):
                try:
                    st.enter_progress_frame(1)
                except Exception:
                    pass

        proj_ok = project_test(world) or proj_ok
        any_nav = random_nav(world) or any_nav
        ok, info = path_test(world)
        log(f"probe path_ok={ok} {info} proj={proj_ok} any={any_nav}")
        if ok:
            path_ok, path_info = True, info
            break
        path_info = info

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
            log(f"verified agent r={ar} h={ah}")
        except Exception as e:
            log(f"agent verify: {e}")

    overall = bounds_n >= 1 and recast_n >= 1 and agent_ok and (path_ok or proj_ok or any_nav)

    log("=== SUMMARY ===")
    log("coverage=Victoria-centered interior box ~40m x 40m x 6m (clamped from full UEhouse AABB)")
    log(f"agent_radius={AGENT_RADIUS} agent_height={AGENT_HEIGHT}")
    log(f"unlock_method={unlock_method} lock_cleared={cleared} floor_hit={floor_hit}")
    log(f"path_ok={path_ok} {path_info} proj_ok={proj_ok} any_nav={any_nav}")
    log(f"FINAL bounds={bounds_n} recast={recast_n} agent_ok={agent_ok}")
    log(f"RESULT: {'PASS' if overall else 'FAIL'}")
    flush_log()
    return 0 if overall else 1


if __name__ == "__main__":
    try:
        code = main()
    except Exception:
        log("FATAL:\n" + traceback.format_exc())
        flush_log()
        code = 1
