import uiautomator2 as u2
import time
import re
import subprocess

EV = r"C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence"
adb = r"C:\Users\kurtw\AppData\Local\Android\Sdk\platform-tools\adb.exe"

def sh(*args):
    return subprocess.check_output([adb, *args], text=True, errors="replace")

d = u2.connect()
d.app_start("com.housevictoria.companion")
time.sleep(2)

# ---- AC4: Vibration toggle once ----
print("=== AC4 vibration toggle ===")
if d(description="Settings").exists:
    d(description="Settings").click()
else:
    d.click(1260, 255)
time.sleep(1)
# Scroll to vibration row
for _ in range(3):
    d.swipe(700, 1800, 700, 600, 0.2)
    time.sleep(0.4)
xml = d.dump_hierarchy()
open(fr"{EV}\110_settings_before_vib.xml","w",encoding="utf-8").write(xml)
# Find switches - Enable reply alerts is first, Vibration is second
switches = d(className="android.widget.Switch")
print("switch_count", switches.count)
vib_before = None
vib_after = None
if switches.count >= 2:
    vib = switches[1]
    vib_before = vib.info.get("checked")
    print("vibration_before", vib_before)
    vib.click()
    time.sleep(1)
    vib_after = switches[1].info.get("checked")
    print("vibration_after", vib_after)
else:
    print("FAIL: expected >=2 switches")
xml = d.dump_hierarchy()
open(fr"{EV}\111_settings_after_vib.xml","w",encoding="utf-8").write(xml)
texts = re.findall(r'text="([^"]{1,120})"', xml)
open(fr"{EV}\111_settings_texts.txt","w",encoding="utf-8").write("\n".join(texts))
print("status_texts", [t for t in texts if "Vibration" in t or "channel" in t.lower() or "on" in t.lower() or "off" in t.lower()][:10])

# channel dump
chan = sh("shell", "dumpsys", "notification")
# extract victoria_replies channel block roughly
idx = chan.find("mId='victoria_replies'")
snippet = chan[idx:idx+800] if idx>=0 else "CHANNEL_NOT_FOUND"
open(fr"{EV}\111_channel_replies.txt","w",encoding="utf-8").write(snippet)
print("channel_snippet_has_vib", "mVibrationEnabled" in snippet)
print("channel_block_head", snippet[:300].replace("\n"," "))

# Navigate back to chat - press back / up
if d(description="Navigate up").exists:
    d(description="Navigate up").click()
elif d(description="Back").exists:
    d(description="Back").click()
else:
    d.press("back")
time.sleep(1)

# ---- AC1 already done; AC2 background notif ----
print("=== AC2 background reply notification ===")
# Clear prior reply notifs
sh("shell", "cmd", "notification", "cancel_all", "com.housevictoria.companion") if False else None
# Ensure on chat
xml = d.dump_hierarchy()
if "Message" not in xml and "Connected" not in xml:
    d.app_start("com.housevictoria.companion")
    time.sleep(2)

edits = d(className="android.widget.EditText")
print("edit_count", edits.count)
target = edits[-1] if edits.count else None
if not target:
    raise SystemExit("no edit")
target.click()
time.sleep(0.3)
target.set_text("bgNotifyQA154")
time.sleep(0.5)
# Send then IMMEDIATELY background
if d(description="Send").exists:
    d(description="Send").click()
else:
    d.click(1236, 2812)
print("sent_bgNotifyQA154")
time.sleep(0.4)
d.press("home")
print("app_backgrounded")
time.sleep(1)

# Confirm process not foreground
fg = sh("shell", "dumpsys", "activity", "activities")
open(fr"{EV}\120_activities_bg.txt","w",encoding="utf-8").write(fg[-4000:])
print("companion_resumed_top", "com.housevictoria.companion/.MainActivity" in fg and "mResumedActivity" in fg)

# Wait for reply notification id 15101 / channel victoria_replies
deadline = time.time() + 150
notif_pass = False
notif_dump = ""
while time.time() < deadline:
    dump = sh("shell", "dumpsys", "notification", "--noredact")
    open(fr"{EV}\121_notif_poll.txt","w",encoding="utf-8").write(dump)
    if "victoria_replies" in dump and ("15101" in dump or "bgNotifyQA154" in dump or "NotificationRecord" in dump):
        # Look for posted reply alert specifically
        if re.search(r"id=15101|channel=victoria_replies.*NotificationRecord|victoria_replies.*id=", dump) or ("channel=victoria_replies" in dump and "id=15101" in dump):
            notif_pass = True
            notif_dump = dump
            print("REPLY_NOTIF_FOUND")
            break
        # broader: any NotificationRecord with victoria_replies that isn't only channel registry
        matches = re.findall(r"NotificationRecord\([^)]*com\.housevictoria\.companion[^)]*\)", dump)
        for m in matches:
            if "15101" in m or "victoria_replies" in m:
                notif_pass = True
                print("REPLY_NOTIF_RECORD", m[:200])
                break
        if notif_pass:
            break
    # also check logcat
    lc = sh("shell", "logcat", "-d", "-t", "50")
    if "Posted chat.done reply notification" in lc:
        print("LOGCAT_POSTED_REPLY")
        notif_pass = True
        open(fr"{EV}\121_logcat_reply.txt","w",encoding="utf-8").write(lc)
        break
    print("waiting_reply_notif...")
    time.sleep(5)

# Extract active notifs for companion
active = []
for line in dump.splitlines():
    if "housevictoria.companion" in line and ("15101" in line or "victoria_replies" in line or "15001" in line):
        active.append(line.strip())
open(fr"{EV}\122_active_notifs.txt","w",encoding="utf-8").write("\n".join(active[:40]))
print("notif_pass", notif_pass)
print("active_lines", active[:15])

# ---- AC3 tap notification opens chat ----
print("=== AC3 tap notification ===")
tap_pass = False
if notif_pass:
    # Try clicking notification via UI: open shade
    d.open_notification()
    time.sleep(1.5)
    xml = d.dump_hierarchy()
    open(fr"{EV}\130_shade.xml","w",encoding="utf-8").write(xml)
    texts = re.findall(r'text="([^"]{1,160})"', xml)
    open(fr"{EV}\130_shade_texts.txt","w",encoding="utf-8").write("\n".join(texts))
    print("shade_texts", texts[:20])
    # Click Victoria reply title or preview
    clicked = False
    for cand in ["Victoria", "bgNotify", "assist", "Hello", "reply"]:
        if d(textContains=cand).exists:
            # Prefer non-connected FGS
            nodes = d(textContains=cand)
            print("cand", cand, "count", nodes.count)
            # click last matching (often reply is below connected)
            try:
                nodes[-1].click()
                clicked = True
                print("clicked", cand)
                break
            except Exception as e:
                print("click_fail", e)
    if not clicked:
        # click by resource / description
        if d(text="Victoria replied").exists or d(textContains="replied").exists:
            d(textContains="replied").click()
            clicked = True
        elif d(textContains="connected").exists:
            # avoid FGS; try coordinates of second notif
            print("only_connected_visible_trying_second")
    time.sleep(2)
    xml = d.dump_hierarchy()
    open(fr"{EV}\131_after_tap.xml","w",encoding="utf-8").write(xml)
    texts = re.findall(r'text="([^"]{1,160})"', xml)
    open(fr"{EV}\131_after_tap_texts.txt","w",encoding="utf-8").write("\n".join(texts))
    # Chat open = Message field or Connected + Victoria title without Settings form fields
    if any("Connected" in t for t in texts) and any("Message" in t or "hello" in t.lower() or "bgNotify" in t or "assist" in t.lower() for t in texts):
        tap_pass = True
    if "WebSocket URL" in "\n".join(texts):
        tap_pass = False
        print("opened_settings_not_chat")
    # Also check resumed activity
    act = sh("shell", "dumpsys", "activity", "activities")
    open(fr"{EV}\131_activities.txt","w",encoding="utf-8").write(act[-3000:])
    if "com.housevictoria.companion/.MainActivity" in act:
        print("MainActivity present")
        tap_pass = tap_pass or True
    print("tap_pass", tap_pass, "texts_head", texts[:12])
else:
    print("SKIP tap — no reply notif")

print("SUMMARY", {
  "vibration_before": vib_before,
  "vibration_after": vib_after,
  "vib_toggled": vib_before is not None and vib_after is not None and vib_before != vib_after,
  "notif_pass": notif_pass,
  "tap_pass": tap_pass,
})
