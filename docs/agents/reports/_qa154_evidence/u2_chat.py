import uiautomator2 as u2
import time
import json

d = u2.connect()
print('device', d.info.get('productName'), d.serial)
d.app_start('com.housevictoria.companion')
time.sleep(2)
# dump hierarchy snippet
xml = d.dump_hierarchy()
open(r'C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence\100_u2_hier.xml','w',encoding='utf-8').write(xml)
print('connected_ui', 'Connected' in xml or 'connected' in xml.lower())

# Find message EditText - last EditText
edits = d(className='android.widget.EditText')
print('edit_count', edits.count)
if edits.count == 0:
    raise SystemExit('no EditText')
# Prefer the one with Message hint / bottom
target = edits[-1] if edits.count > 1 else edits[0]
print('target_info', target.info)
target.click()
time.sleep(0.5)
ok = target.set_text('helloQA154')
print('set_text_result', ok)
time.sleep(0.8)
xml2 = d.dump_hierarchy()
open(r'C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence\101_u2_typed.xml','w',encoding='utf-8').write(xml2)
print('has_hello', 'helloQA154' in xml2)

send = d(description='Send')
print('send_exists', send.exists)
if send.exists:
    # click parent if needed
    send.click()
else:
    # fallback tap
    d.click(1236, 2812)
print('send_clicked')
time.sleep(2)

# Wait for reply up to 120s
deadline = time.time() + 120
pass_rt = False
while time.time() < deadline:
    xml = d.dump_hierarchy()
    open(r'C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence\102_u2_poll.xml','w',encoding='utf-8').write(xml)
    texts = []
    # crude extract
    import re
    texts = re.findall(r'text=\"([^\"]{1,200})\"', xml)
    open(r'C:\Users\kurtw\Soul_Core\docs\agents\reports\_qa154_evidence\102_u2_texts.txt','w',encoding='utf-8').write('\n'.join(texts))
    print('POLL', ' || '.join(texts[:12]))
    has_user = any('helloQA154' in t for t in texts)
    # assistant-ish: longer text not chrome
    noise = ('Victoria','Connected','WS connected','Message','helloQA154','frame loop')
    others = [t for t in texts if len(t) > 12 and not any(n in t for n in noise)]
    if has_user and others:
        print('CHAT_RT_PASS', others[0][:120])
        pass_rt = True
        break
    if has_user and not others:
        print('user_visible_waiting_reply')
    time.sleep(5)
print('RESULT', pass_rt)
