import uiautomator2 as u2, time, re, subprocess
EV=r"C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence"
adb=r"C:\Users\kurtw\AppData\Local\Android\Sdk\platform-tools\adb.exe"
def sh(*a):
  return subprocess.check_output([adb,*a], text=True, errors="replace", timeout=60)
d=u2.connect()
sh("shell","am","start","-n","com.housevictoria.companion/.MainActivity")
time.sleep(2)
d(description="Settings").click(); time.sleep(1)
for _ in range(2):
  d.swipe(700,1700,700,700,0.2); time.sleep(0.3)
xml=d.dump_hierarchy()
# find Vibration switch
m=re.search(r'text="Vibration"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"',xml)
checks=re.findall(r'checkable="true" checked="(true|false)"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"',xml)
vy=(int(m.group(2))+int(m.group(4)))//2
best=min(checks, key=lambda c: abs(((int(c[2])+int(c[4]))//2)-vy))
print("current_vib_switch", best[0])
# Ensure OFF then ON once for sound/vib evidence of recreate
if best[0]=="true":
  d.click((int(best[1])+int(best[3]))//2, (int(best[2])+int(best[4]))//2)
  time.sleep(1.5)
  print("toggled_to_off")
sh("shell","logcat","-c")
# toggle ON to force recreate with vibration
xml=d.dump_hierarchy()
m=re.search(r'text="Vibration"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"',xml)
checks=re.findall(r'checkable="true" checked="(true|false)"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"',xml)
vy=(int(m.group(2))+int(m.group(4)))//2
best=min(checks, key=lambda c: abs(((int(c[2])+int(c[4]))//2)-vy))
print("before_on", best[0])
if best[0]=="false":
  d.click((int(best[1])+int(best[3]))//2, (int(best[2])+int(best[4]))//2)
  time.sleep(1.5)
xml=d.dump_hierarchy()
open(fr"{EV}/142_vib_on.xml","w",encoding="utf-8").write(xml)
texts=re.findall(r'text="([^"]+)"',xml)
print("status", [t for t in texts if "Vibration" in t or "channel" in t.lower() or "on" in t or "off" in t])
time.sleep(1)
chan=sh("shell","dumpsys","notification")
vm=re.search(r"mId='victoria_replies'.*?mVibrationEnabled=(true|false).*?mVibrationPattern=(\[[^\]]+\]|null)", chan, re.S)
print("channel", vm.groups() if vm else None)
open(fr"{EV}/142_channel.txt","w",encoding="utf-8").write(chan[chan.find("mId='victoria_replies'"):chan.find("mId='victoria_replies'")+700])
lc=sh("shell","logcat","-d","-t","40")
open(fr"{EV}/142_logcat.txt","w",encoding="utf-8").write(lc)
print("log_channel", [l for l in lc.splitlines() if "ReplyNotification" in l or "Channel victoria" in l][-5:])

# Clean AC3: send, bg, wait notif, tap ONLY (no am start)
print("=== clean AC3 ===")
d.press("back"); time.sleep(1)
edits=d(className="android.widget.EditText")
if edits.count<1:
  sh("shell","am","start","-n","com.housevictoria.companion/.MainActivity"); time.sleep(3)
  edits=d(className="android.widget.EditText")
edits[-1].set_text("tapOpenQA154")
d(description="Send").click(); time.sleep(0.3); d.press("home")
print("bg")
deadline=time.time()+120
ok=False
while time.time()<deadline:
  dump=sh("shell","dumpsys","notification","--noredact")
  if "channel=victoria_replies" in dump and "id=15101" in dump:
    ok=True; print("notif_ready"); break
  time.sleep(4)
if not ok:
  print("no_notif"); raise SystemExit(0)
d.open_notification(); time.sleep(1.5)
# click reply body containing tapOpen
if d(textContains="tapOpen").exists:
  d(textContains="tapOpen").click(); print("clicked_tapOpen")
elif d(textContains="assist").exists:
  d(textContains="assist").click(); print("clicked_assist")
else:
  # last Victoria title that isn't connected subtitle
  d(text="Victoria")[-1].click(); print("clicked_Victoria")
time.sleep(2)
xml=d.dump_hierarchy()
open(fr"{EV}/162_tap_only.xml","w",encoding="utf-8").write(xml)
texts=re.findall(r'text="([^"]{1,200})"',xml)
open(fr"{EV}/162_tap_only_texts.txt","w",encoding="utf-8").write("\n".join(texts))
print("UI_AFTER_TAP", texts[:15])
act=sh("shell","dumpsys","activity","activities")
# find resumed
for line in act.splitlines():
  if "mResumedActivity" in line or "topResumedActivity" in line:
    if "companion" in line.lower():
      print("RESUMED", line.strip())
print("DONE")
