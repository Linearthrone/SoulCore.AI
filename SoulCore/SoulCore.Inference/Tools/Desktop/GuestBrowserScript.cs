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

def match_nodes(query):
    Atspi = load_atspi()
    apps = firefox_apps(Atspi)
    acc = []
    q = (query or "").lower()
    for app in apps:
        walk(app, acc, "")
    hits = [e for e in acc if q and q in (e.get("name") or "").lower()]
    if not hits:
        hits = [e for e in acc if q and q in (e.get("role") or "").lower()]
    return hits

def click_text(query, nth):
    hits = match_nodes(query)
    if not hits:
        out(False, error=f"no control matching '{query}'", count=0)
        return
    idx = max(1, nth) - 1
    if idx >= len(hits):
        out(False, error=f"nth={nth} out of range (found {len(hits)})", count=len(hits), elements=hits[:20])
        return
    el = hits[idx]
    out(True, action="click_text", count=len(hits), picked=el, elements=hits[:20])

def fill(query, value):
    hits = match_nodes(query)
    entries = [e for e in hits if "entry" in (e.get("role") or "").lower()
               or "password" in (e.get("role") or "").lower()
               or "text" in (e.get("role") or "").lower()]
    pick = (entries or hits)
    if not pick:
        out(False, error=f"no field matching '{query}'")
        return
    el = pick[0]
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
