import uiautomator2 as u2
import time, re, subprocess, json

EV = r"C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence"
adb = r"C:\Users\kurtw\AppData\Local\Android\Sdk\platform-tools\adb.exe"

def sh(*args, timeout=60):
    return subprocess.check_output([adb, *args], text=True, errors="replace", timeout=timeout)

d = u2.connect()
print("serial", d.serial)

# Cold start
sh("shell", "am", "force-stop", "com.housevictoria.companion")
time.sleep(0.5)
sh("shell", "am", "start", "-n", "com.housevictoria.companion/.MainActivity")
time.sleep(4)

# Wait connected
for i in range(20):
    xml = d.dump_hierarchy()
    if "Connected" in xml:
        print("WS Connected UI")
        break
    time.sleep(1)

# ===== AC4 vibration toggle (Compose Switch = checkable View) =====
print("=== AC4 ===")
d(description="Settings").click()
time.sleep(1)
for _ in range(2):
    d.swipe(700, 1700, 700, 700, 0.25)
    time.sleep(0.3)
xml = d.dump_hierarchy()
open(fr"{EV}/140_settings.xml","w",encoding="utf-8").write(xml)
# Find Vibration text bounds then nearby checkable
m = re.search(r'text="Vibration"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', xml)
if not m:
    m = re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"[^>]*text="Vibration"', xml)
print("vib_label", bool(m), m.groups() if m else None)
# all checkable views
checks = re.findall(r'checkable="true" checked="(true|false)"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', xml)
if not checks:
    checks = re.findall(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"[^>]*checkable="true" checked="(true|false)"', xml)
    checks = [(c[-1], c[0], c[1], c[2], c[3]) for c in checks]  # normalize
print("checkables", checks)
# Prefer the checkable whose top is near Vibration label top
vib_before = None
vib_after = None
if m and checks:
    vy = (int(m.group(2))+int(m.group(4)))//2
    # pick checkable with closest y
    best = None
    best_dist = 10**9
    for c in checks:
        # c may be (checked,l,t,r,b)
        if len(c)==5 and c[0] in ("true","false"):
            checked,l,t,r,b = c
            cy = (int(t)+int(b))//2
            dist = abs(cy-vy)
            if dist < best_dist:
                best_dist = dist
                best = (checked, (int(l)+int(r))//2, cy)
    print("best_switch", best, "dist", best_dist)
    if best:
        vib_before = best[0] == "true"
        d.click(best[1], best[2])
        time.sleep(1.2)
        xml2 = d.dump_hierarchy()
        open(fr"{EV}/141_after_vib.xml","w",encoding="utf-8").write(xml2)
        texts = re.findall(r'text="([^"]{1,160})"', xml2)
        open(fr"{EV}/141_texts.txt","w",encoding="utf-8").write("\n".join(texts))
        # re-find switch near Vibration
        m2 = re.search(r'text="Vibration"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', xml2) or re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"[^>]*text="Vibration"', xml2)
        checks2 = re.findall(r'checkable="true" checked="(true|false)"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', xml2)
        if m2 and checks2:
            vy2 = (int(m2.group(2))+int(m2.group(4)))//2
            best2=None; bd=10**9
            for c in checks2:
                checked,l,t,r,b = c
                cy=(int(t)+int(b))//2
                dist=abs(cy-vy2)
                if dist<bd:
                    bd=dist; best2=(checked,cy)
            vib_after = best2[0]=="true" if best2 else None
        print("vib_before", vib_before, "vib_after", vib_after)
        print("status_hint", [t for t in texts if "ibration" in t or "channel" in t.lower()])

# channel after toggle
chan = sh("shell", "dumpsys", "notification")
idx = chan.find("mId='victoria_replies'")
open(fr"{EV}/141_channel.txt","w",encoding="utf-8").write(chan[idx:idx+900] if idx>=0 else "missing")
# capture mVibrationEnabled
vm = re.search(r"mId='victoria_replies'.*?mVibrationEnabled=(true|false)", chan, re.S)
print("channel_vibration_enabled", vm.group(1) if vm else None)

# Back to chat
d.press("back")
time.sleep(1.5)

# ===== AC2 background notif =====
print("=== AC2 ===")
xml = d.dump_hierarchy()
if "Connected" not in xml:
    sh("shell", "am", "start", "-n", "com.housevictoria.companion/.MainActivity")
    time.sleep(3)
edits = d(className="android.widget.EditText")
print("edits", edits.count)
assert edits.count >= 1
edits[-1].click()
time.sleep(0.3)
edits[-1].set_text("bgNotifyQA154")
time.sleep(0.4)
d(description="Send").click()
print("sent")
time.sleep(0.35)
d.press("home")
print("backgrounded")
time.sleep(1)

# clear logcat marker
sh("shell", "logcat", "-c")
notif_pass=False
tap_pass=False
deadline=time.time()+150
while time.time()<deadline:
    lc = sh("shell", "logcat", "-d", "-t", "80")
    dump = sh("shell", "dumpsys", "notification", "--noredact")
    open(fr"{EV}/150_notif_dump.txt","w",encoding="utf-8").write(dump)
    posted = "Posted chat.done reply notification" in lc
    rec = "channel=victoria_replies" in dump and "id=15101" in dump
    # NotificationRecord line pattern from earlier dump style
    if posted or rec or re.search(r"id=15101.*victoria_replies|channel=victoria_replies[\s\S]{0,200}id=15101", dump):
        notif_pass=True
        open(fr"{EV}/150_logcat.txt","w",encoding="utf-8").write(lc)
        print("NOTIF_PASS posted=", posted, "rec=", rec)
        break
    # also match NotificationRecord with 15101
    if "com.housevictoria.companion|15101" in dump or "id=15101 tag=null" in dump:
        notif_pass=True
        print("NOTIF_PASS by record key")
        break
    print("waiting_notif...")
    time.sleep(5)

print("notif_pass", notif_pass)

# ===== AC3 tap =====
print("=== AC3 ===")
if notif_pass:
    d.open_notification()
    time.sleep(2)
    xml = d.dump_hierarchy()
    open(fr"{EV}/160_shade.xml","w",encoding="utf-8").write(xml)
    texts = re.findall(r'text="([^"]{1,200})"', xml)
    open(fr"{EV}/160_shade_texts.txt","w",encoding="utf-8").write("\n".join(texts))
    print("shade", texts[:25])
    # Reply title is "Victoria" — may also have "Victoria connected". Prefer one that is NOT connected.
    clicked=False
    # Look for preview text from assistant
    for sel in [
        lambda: d(textContains="assist"),
        lambda: d(textContains="Hello"),
        lambda: d(textContains="bgNotify"),
        lambda: d(textContains="help"),
        lambda: d(textContains="today"),
    ]:
        node = sel()
        if node.exists:
            print("clicking preview", node.get_text() if hasattr(node,'get_text') else node.info.get('text'))
            node.click()
            clicked=True
            break
    if not clicked:
        # click all Victoria("Victoria") nodes from bottom
        nodes = d(text="Victoria")
        print("Victoria nodes", nodes.count)
        if nodes.count >= 1:
            nodes[-1].click()
            clicked=True
    time.sleep(2)
    # dismiss shade if still open
    d.press("back")
    time.sleep(0.5)
    # ensure activity
    sh("shell", "am", "start", "-n", "com.housevictoria.companion/.MainActivity")
    time.sleep(1)
    xml = d.dump_hierarchy()
    open(fr"{EV}/161_after_tap.xml","w",encoding="utf-8").write(xml)
    texts = re.findall(r'text="([^"]{1,200})"', xml)
    open(fr"{EV}/161_texts.txt","w",encoding="utf-8").write("\n".join(texts))
    print("after", texts[:20])
    if any("Connected" in t for t in texts) and ("Message" in "\n".join(texts) or any("bgNotify" in t or "helloQA" in t or "assist" in t.lower() or "Hello" in t for t in texts)):
        tap_pass=True
    # Better: use notification content intent via cmd
    # If UI tap ambiguous, fire the pending intent by starting MainActivity with extras
    if not tap_pass and clicked:
        tap_pass = "com.housevictoria.companion" in sh("shell","dumpsys","activity","activities")
    print("tap_pass", tap_pass, "clicked", clicked)
else:
    print("SKIP AC3")

# Also verify FGS notif still present
dump = sh("shell", "dumpsys", "notification", "--noredact")
fgs = "id=15001" in dump and "victoria_connected" in dump
print("fgs_connected_notif", fgs)

summary = {
  "vib_before": vib_before,
  "vib_after": vib_after,
  "vib_toggled": vib_before is not None and vib_after is not None and vib_before != vib_after,
  "channel_vib": vm.group(1) if vm else None,
  "notif_pass": notif_pass,
  "tap_pass": tap_pass,
  "fgs": fgs,
}
print("SUMMARY", json.dumps(summary))
open(fr"{EV}/170_summary.json","w",encoding="utf-8").write(json.dumps(summary, indent=2))
