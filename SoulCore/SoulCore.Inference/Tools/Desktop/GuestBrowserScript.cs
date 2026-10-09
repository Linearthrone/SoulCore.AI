namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Python helper copied into the Ubuntu guest. Uses AT-SPI to list/click/fill
/// Firefox controls; falls back to printing an install hint.
/// </summary>
internal static class GuestBrowserScript
{
    public const string GuestPath = "/tmp/hv-browser.py";

    public const string Source = """
#!/usr/bin/env python3
import json, os, sys, base64, subprocess, traceback

def out(ok, **kw):
    payload = {"ok": bool(ok)}
    payload.update(kw)
    sys.stdout.write(json.dumps(payload, ensure_ascii=False))
    sys.stdout.write("\n")
    sys.stdout.flush()

def enable_a11y():
    try:
        subprocess.run(
            ["gsettings", "set", "org.gnome.desktop.interface", "toolkit-accessibility", "true"],
            check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=5)
    except Exception:
        pass

def load_atspi():
    import gi
    gi.require_version("Atspi", "2.0")
    from gi.repository import Atspi
    Atspi.init()
    return Atspi

INTERESTING = {
    "push button", "button", "link", "entry", "password text", "text",
    "check box", "radio button", "combo box", "menu item", "page tab",
    "tab", "heading", "toggle button", "spin button", "document web",
    "label", "static", "paragraph",
}

def extents(node):
    try:
        ext = node.get_extents(0)
        return int(ext.x), int(ext.y), int(ext.width), int(ext.height)
    except Exception:
        return 0, 0, 0, 0

def walk(node, acc, query, depth=0):
    if node is None or depth > 28 or len(acc) >= 450:
        return
    try:
        role = (node.get_role_name() or "").strip()
        name = (node.get_name() or "").strip()
        role_l = role.lower()
        if role_l in INTERESTING and (name or role_l in ("entry", "password text", "document web", "text")):
            if not query or query in name.lower() or query in role_l:
                x, y, w, h = extents(node)
                if w >= 2 and h >= 2:
                    acc.append({
                        "role": role, "name": name,
                        "x": x, "y": y, "w": w, "h": h,
                        "cx": x + w // 2, "cy": y + h // 2,
                    })
        n = node.get_child_count()
        for i in range(max(0, n)):
            walk(node.get_child_at_index(i), acc, query, depth + 1)
    except Exception:
        return

def firefox_apps(Atspi):
    desktop = Atspi.get_desktop(0)
    found = []
    for i in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(i)
        if app is None:
            continue
        n = (app.get_name() or "").lower()
        if "firefox" in n or "mozilla" in n:
            found.append(app)
    return found

def snapshot(query):
    Atspi = load_atspi()
    apps = firefox_apps(Atspi)
    if not apps:
        out(False, error="no Firefox accessibility tree (is Firefox open? enable toolkit-accessibility)")
        return
    acc = []
    for app in apps:
        walk(app, acc, query)
    out(True, action="snapshot", count=len(acc), elements=acc[:400])

def walk_nodes(node, acc, query, depth=0):
    # Like walk but keeps the live AT-SPI node for do_action.
    if node is None or depth > 28 or len(acc) >= 450:
        return
    try:
        role = (node.get_role_name() or "").strip()
        name = (node.get_name() or "").strip()
        role_l = role.lower()
        if role_l in INTERESTING and (name or role_l in ("entry", "password text", "document web", "text")):
            if not query or query in name.lower() or query in role_l:
                x, y, w, h = extents(node)
                if w >= 2 and h >= 2:
                    acc.append((node, {
                        "role": role, "name": name,
                        "x": x, "y": y, "w": w, "h": h,
                        "cx": x + w // 2, "cy": y + h // 2,
                    }))
        n = node.get_child_count()
        for i in range(max(0, n)):
            walk_nodes(node.get_child_at_index(i), acc, query, depth + 1)
    except Exception:
        return

def match_node_pairs(query):
    Atspi = load_atspi()
    apps = firefox_apps(Atspi)
    acc = []
    q = (query or "").lower()
    for app in apps:
        walk_nodes(app, acc, "")
    hits = [p for p in acc if q and q in (p[1].get("name") or "").lower()]
    if not hits:
        hits = [p for p in acc if q and q in (p[1].get("role") or "").lower()]
    return hits

def match_nodes(query):
    return [el for _, el in match_node_pairs(query)]

def try_do_action(node):
    # Activate via AT-SPI (no mouse) — immune to VirtualBox Absolute pointing.
    try:
        n = node.get_n_actions()
    except Exception:
        return False
    preferred = ("click", "press", "activate", "jump", "open")
    for i in range(max(0, n)):
        try:
            an = (node.get_action_name(i) or "").strip().lower()
        except Exception:
            continue
        if an not in preferred:
            continue
        try:
            result = node.do_action(i)
            # gi Atspi may return True, or None on success — only False is failure.
            if result is False:
                continue
            return True
        except Exception:
            continue
    return False

def activate_node(node):
    # Walk parents: leaf text often has no action; the button/link above does.
    cur = node
    for _ in range(10):
        if cur is None:
            return None
        if try_do_action(cur):
            return cur
        try:
            cur = cur.get_parent()
        except Exception:
            return None
    return None

def desktop_apps(Atspi):
    desktop = Atspi.get_desktop(0)
    found = []
    for i in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(i)
        if app is not None:
            found.append(app)
    return found

def node_at_point(Atspi, x, y):
    # Resolve accessible under screen point; Firefox apps first, then all apps.
    xi, yi = int(x), int(y)
    ordered = []
    seen = set()
    for app in firefox_apps(Atspi) + desktop_apps(Atspi):
        try:
            key = id(app)
        except Exception:
            key = None
        if key in seen:
            continue
        if key is not None:
            seen.add(key)
        ordered.append(app)
    for app in ordered:
        try:
            comp = app.get_component_iface()
            if comp is None:
                continue
            node = comp.get_accessible_at_point(xi, yi, Atspi.CoordType.SCREEN)
            if node is not None:
                return node
        except Exception:
            continue
    # Extents fallback when get_accessible_at_point is empty (common on some themes).
    best = None
    best_area = None
    acc = []
    for app in ordered:
        walk_nodes(app, acc, "", 0)
    for node, el in acc:
        x0, y0, w, h = el["x"], el["y"], el["w"], el["h"]
        if w < 2 or h < 2:
            continue
        if x0 <= xi < x0 + w and y0 <= yi < y0 + h:
            area = w * h
            if best is None or area < best_area:
                best, best_area = node, area
    return best

def click_text(query, nth):
    hits = match_node_pairs(query)
    if not hits:
        out(False, error=f"no control matching '{query}'", count=0)
        return
    idx = max(1, nth) - 1
    if idx >= len(hits):
        out(False, error=f"nth={nth} out of range (found {len(hits)})",
            count=len(hits), elements=[e for _, e in hits[:20]])
        return
    node, el = hits[idx]
    activated = activate_node(node)
    if activated is not None:
        out(True, action="click_text_atspi", count=len(hits), picked=el,
            elements=[e for _, e in hits[:20]], method="atspi")
        return
    Atspi = load_atspi()
    if atspi_mouse_click(Atspi, el.get("cx", 0), el.get("cy", 0)):
        out(True, action="click_text_atspi_mouse", count=len(hits), picked=el,
            elements=[e for _, e in hits[:20]], method="atspi-mouse")
        return
    out(True, action="click_text", count=len(hits), picked=el,
        elements=[e for _, e in hits[:20]], method="coords")

def atspi_mouse_click(Atspi, x, y):
    # In-guest synthetic mouse via AT-SPI — does not use host Absolute pointing.
    try:
        Atspi.generate_mouse_event(int(x), int(y), "b1c")
        return True
    except Exception:
        pass
    try:
        Atspi.generate_mouse_event(int(x), int(y), "abs")
        Atspi.generate_mouse_event(int(x), int(y), "b1p")
        Atspi.generate_mouse_event(int(x), int(y), "b1r")
        return True
    except Exception:
        return False

def click_xy(x, y):
    # Prefer AT-SPI do_action; then AT-SPI synthetic mouse; else coords for xdotool.
    Atspi = load_atspi()
    target = node_at_point(Atspi, x, y)
    activated = activate_node(target) if target is not None else None
    if activated is not None:
        role = (activated.get_role_name() or "").strip()
        name = (activated.get_name() or "").strip()
        out(True, action="click_xy_atspi", x=int(x), y=int(y),
            role=role, name=name, method="atspi")
        return
    if atspi_mouse_click(Atspi, x, y):
        out(True, action="click_xy_atspi_mouse", x=int(x), y=int(y), method="atspi-mouse")
        return
    out(False, action="click_xy", x=int(x), y=int(y), method="coords",
        error="no AT-SPI action at point — use xdotool")

def fill(query, value):
    hits = match_node_pairs(query)
    entries = [p for p in hits if "entry" in (p[1].get("role") or "").lower()
               or "password" in (p[1].get("role") or "").lower()
               or "text" in (p[1].get("role") or "").lower()]
    pick = (entries or hits)
    if not pick:
        out(False, error=f"no field matching '{query}'")
        return
    node, el = pick[0]
    # Focus via AT-SPI when possible so Absolute mouse is not required.
    try_do_action(node)
    out(True, action="fill", picked=el, typed_len=len(value or ""), value_set=False)

def tabs():
    hits = match_nodes("tab")
    tabs = [e for e in hits if "tab" in (e.get("role") or "").lower()]
    out(True, action="tabs", count=len(tabs), elements=tabs[:40])

def main():
    enable_a11y()
    argv = sys.argv[1:]
    cmd = argv[0] if argv else "snapshot"
    try:
        if cmd == "snapshot":
            q = (argv[1] if len(argv) > 1 else "").lower()
            snapshot(q)
        elif cmd == "click_text":
            q = argv[1] if len(argv) > 1 else ""
            nth = int(argv[2]) if len(argv) > 2 else 1
            click_text(q, nth)
        elif cmd == "click_xy":
            cx = int(argv[1]) if len(argv) > 1 else -1
            cy = int(argv[2]) if len(argv) > 2 else -1
            click_xy(cx, cy)
        elif cmd == "fill":
            q = argv[1] if len(argv) > 1 else ""
            raw = argv[2] if len(argv) > 2 else ""
            value = base64.b64decode(raw).decode("utf-8") if raw else os.environ.get("HV_FILL", "")
            fill(q, value)
        elif cmd == "tabs":
            tabs()
        else:
            out(False, error=f"unknown cmd {cmd}")
    except Exception as e:
        out(False, error=str(e), trace=traceback.format_exc()[-800:])

if __name__ == "__main__":
    main()
""";
}
