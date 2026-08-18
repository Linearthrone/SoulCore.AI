"""
TASK-192 / REX-01 — Scaffold Victoria waist-up call camera (phone-call framing).

Run in UE 5.8 Editor with /Game/Home open after Victoria Character exists.

This script:
1) Finds BP_VictoriaCharacter / VictoriaAvatar (AI body — NOT Kayleigh player).
2) Logs hierarchy + suggests CallCapture placement.
3) Does NOT set player DefaultPawn (that remains Kayleigh / TASK-191).

Full SceneCapture + RT wiring may need Blueprint / Live Coding in
HouseVictoriaBridge (call_capture → call_frame). Use this as the placement guide.
"""
from __future__ import annotations

import unreal

VICTORIA_MARKERS = ("VictoriaAvatar", "BP_VictoriaCharacter", "BP_MHC_Victoria")
KAYLEIGH_MARKERS = ("KayleighPlayer", "BP_KayleighCharacter", "BP_MHC_Kayleigh", "Kayleigh")
LOG_PREFIX = "[setup_victoria_call_camera]"


def log(msg: str) -> None:
    line = f"{LOG_PREFIX} {msg}"
    unreal.log(line)
    print(line)


def blob(actor) -> str:
    bits = [actor.get_actor_label(), actor.get_class().get_name()]
    try:
        bits.extend(str(t) for t in actor.tags)
    except Exception:
        pass
    return " ".join(bits)


def is_victoria(actor) -> bool:
    b = blob(actor).lower()
    return any(m.lower() in b for m in VICTORIA_MARKERS)


def is_kayleigh(actor) -> bool:
    b = blob(actor).lower()
    return any(m.lower() in b for m in KAYLEIGH_MARKERS)


def main() -> None:
    log("Looking for Victoria AI avatar (call camera target)…")
    subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    actors = list(subsystem.get_all_level_actors())
    victorias = [a for a in actors if is_victoria(a)]
    kayleighs = [a for a in actors if is_kayleigh(a)]

    for a in victorias:
        loc = a.get_actor_location()
        log(f"Victoria candidate: {blob(a)} loc={loc}")
    for a in kayleighs:
        log(f"Kayleigh (PLAYER — do not attach call camera): {blob(a)}")

    if not victorias:
        log("FAIL: no Victoria avatar in level — place BP_VictoriaCharacter / tag VictoriaAvatar")
        return

    log(
        "NEXT (manual / Live Coding):\n"
        "  1) On Victoria Character add SceneCaptureComponent2D 'CallCapture'\n"
        "  2) Portrait RT ~720x1280, FOV ~60, ~0.5m in front of chest/face\n"
        "  3) Bridge: command call_capture → WS type call_frame {bytes_b64,format,width,height}\n"
        "  4) Verify: GET http://127.0.0.1:7700/api/companion/v1/call/frame\n"
        "HARD: never set player DefaultPawn to Victoria."
    )


if __name__ == "__main__":
    main()
