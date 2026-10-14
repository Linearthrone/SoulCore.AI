---
issue_id: ISSUE-20261014-001
discovered: 2026-10-14
severity: P1 (Critical)
status: Pending Fix
reticket: TASK-20261014-213-PM01-to-OPS01
last_retest: TASK-20261014-211
---

## Problem Description

With `victoria-sandbox` running and `HerScreenInput` attached on Release ChatDesktop pid 4564, one wheel-away detent (`WM_MOUSEMOVE` then `WM_MOUSEWHEEL` delta +120) did not scroll the guest page up about one notch. The chat transcript view also moved before a delayed after-shot.

Reported from TASK-20261014-195. Tester note uses LinearThrone.

## Reproduction Steps

1. Leave SoulCore.Host running. Do not stop ChatDesktop pid 4564 (`House.ChatDesktop.exe` from the Release output).
2. Confirm `VBoxManage showvminfo victoria-sandbox --machinereadable` shows `VMState="running"`.
3. Confirm a child window `HerScreenInput` of class `House.HerScreenHwndOverlay.v1`.
4. Capture the VirtualBox content rect and the chat column.
5. `SendMessageTimeout` one `WM_MOUSEMOVE` at the center of `HerScreenInput`, then one `WM_MOUSEWHEEL` with delta +120 and screen coordinates over that window. Do not send a second detent.

## Expected Result

Guest content moves up about one notch. The chat transcript does not scroll from that wheel.

## Actual Result

`SendMessageTimeout` returned 1 for both messages (`WHEEL_SENT_COUNT=1`, wheel `w=0x780000`).

Guest full frame: best vertical shift 0, mean absolute error 5.991, changed fraction 0.0232. Lesson column (560x600): best shift 0, error 16.997, changed fraction 0.0732. No shift in ±420px beat the unshifted lesson column. That is not one notch upward.

Chat crop: best shift +154px, aligned error 0.593 versus 85.837 unshifted. The after frame was taken at 09:01:40, about three minutes after the 08:58:40 send, because the checker exited with code 2 before the immediate after-shot. That later frame also shows new lines under the 08:36 message, so the 154px move is not isolated to the wheel.

At 09:00:09 `HerScreenInput` was briefly missing, then it returned at the same rect with a new hwnd.

## Screenshots/Logs

Numeric captures are in `tmpcode/qa-195/` (`guest-before.png`, `guest-after.png`, `chat-before.png`, `chat-after.png`). Command output is in `docs/agents/reports/TASK-20261014-195-QA01-to-PM01.md`.

`presence-startup.log` last write 08:39:13. No VM pointer fault was logged at the wheel time.

## Impact Scope

Operator wheel over her screen on the running Presence desk. Guest page does not take one notch. Transcript movement was observed on the TASK-195 run but was not isolated from new chat lines. The TASK-198 retest did not move the transcript.

## Retest TASK-20261014-198

Still failing on the guest page. Release ChatDesktop pid 31092, `VMState="running"`, `HerScreenInput` hwnd `0x310C58`. One detent only (`WM_MOUSEMOVE` then `WM_MOUSEWHEEL` delta +120, `w=0x780000`, `WHEEL_SENT_COUNT=1`). Guest after-shot saved 818 ms later, at `2026-10-14T09:37:17.722-04:00`.

Guest full frame: best shift 0, mean absolute error 2.927, changed fraction 0.0180. Body crop (x=143, y=92, 908x402): best shift 0, error 4.086, changed fraction 0.0194. A pixel compare of the same files found 6087 changed samples of 343252 (fraction 0.0177). Those samples are sparse. They do not line up as a vertical translation inside ±420 px.

Chat crop: best shift 0, error 0.000, changed fraction 0.0000. Top 70% the same. No new lines in that window.

Startup log, one new line:

```text
2026-10-14 09:37:17.153 her-screen wheel notches=1 point=754,371 sent
```

The wheel is logged as delivered. The guest page still does not move up about one notch. Frames are in `tmpcode/qa-198/`. Command output is in `docs/agents/reports/TASK-20261014-198-QA01-to-PM01.md`.

## Retest TASK-20261014-201

The scroll was not retested. Release ChatDesktop pid 15392 (started 09:49:37, `bin\Release\net8.0\House.ChatDesktop.exe`) was still running, and `showvminfo` showed `VMState="running"` (`VMStateChangeTime="2026-10-14T13:28:28.487000000"`). `HerScreenInput` was not present.

Pid 15392 window list at 10:00:54 had the Presence window (`hwnd=0x4FD0C68`, rect 5116,0 1899x1089) and no `House.HerScreenHwndOverlay.v1`. A desktop scan of 675 windows at 10:01:17 found no `HerScreenInput`. VirtualBoxVM pids 40864, 44188, and 44216 were still alive from 09:28, with no visible guest content window.

No `WM_MOUSEMOVE` and no `WM_MOUSEWHEEL` were sent. No guest shift, chat shift, or cursor samples. The newest startup-log wheel line is still the TASK-198 line:

```text
2026-10-14 09:37:17.153 her-screen wheel notches=1 point=754,371 sent
```

Command output is in `docs/agents/reports/TASK-20261014-201-QA01-to-PM01.md`. Pid 15392 was not stopped. The guest was not powered.

## Retest TASK-20261014-204

Still failing on the guest page. Release ChatDesktop pid 38476, started 10:10:55, `VMState="running"`, `HerScreenInput` hwnd `0x3E0C6E`. Guest window hwnd `0x310F3E`, pid 48264, title `victoria-sandbox [Running]`. One detent only (`WM_MOUSEMOVE` then `WM_MOUSEWHEEL` delta +120, `w=0x780000`, `WHEEL_SENT_COUNT=1`). Guest after-shot saved 436 ms later, at `2026-10-14T10:18:40.013-04:00`.

Guest full frame: best shift 0, mean absolute error 3.236, changed fraction 0.0247. Body crop (x=146, y=108, 922x472): best shift 0, error 6.052, changed fraction 0.0473. The Maestro lesson page stayed in place. The red pointer readout moved from x=601 to x=595 and y stayed 324. That is not one notch upward.

Chat crop: best shift 0, error 0.000, changed fraction 0.0000. Top 70% the same. No new lines in that window.

Three cursor samples in the 400 ms after the send, all inside `HerScreenInput`: (6353,520) at 13 ms, (6360,485) at 151 ms, (6353,520) at 308 ms. Being inside the embed did not scroll the page.

Startup log, one new line after 10:10:55:

```text
2026-10-14 10:18:39.736 her-screen wheel notches=1 point=602,295 sent
```

The 09:37 line is still in the file and does not count. The wheel is logged as delivered. The guest page still does not move up about one notch. Frames are in `tmpcode/qa-204/`. Command output is in `docs/agents/reports/TASK-20261014-204-QA01-to-PM01.md`. Pid 38476 was not stopped. The guest was not powered.

## Retest TASK-20261014-211

Still failing on a visible scrollable page. Release ChatDesktop pid 38920, started 10:45:41, was still running. `VMState="running"`. `ShowWindow(SW_RESTORE)` (9) was applied to `House Victoria - Presence 0.1.27` hwnd `0x31139A`. The window was already visible. Its rect was `2751,0 2374x1386`, which extends below the ticket box `0,0 7040x1152` (the live virtual screen is `0,0 7040x1440`, primary `5120x1440`). `SetWindowPos` moved it to the primary origin: `0,0 2374x1386`. It was not resized. The window is taller than 1152, so it still extends past that line, and it fits on the primary monitor. `HerScreenInput` hwnd `0xD01392` was on screen at `793,180 1528x881`.

The before frame is not black (mean luminance 52.98, max 255). Firefox on the Ubuntu desktop is open to the Maestro lesson "The box model," a scrollable page about CSS padding and margin. It is not a login screen and not an empty desktop. No browser was opened for this run.

One detent only (`WM_MOUSEMOVE` then `WM_MOUSEWHEEL` delta +120, `w=0x780000`, `WHEEL_SENT_COUNT=1`). Guest after-shot saved 46 ms later, at `2026-10-14T11:07:03.111-04:00`.

Guest full frame: best shift 0, mean absolute error 0.016, changed fraction 0.0000. Body crop (x=182, y=140, 1155x611): best shift 0, error 0.000, changed fraction 0.0000. The lesson stayed in place. That is not one notch upward.

Startup log, one new line after 10:51:24:

```text
2026-10-14 11:07:03.060 her-screen wheel notches=1 key=pageup sent
```

The 10:51:24 line is the previous run and does not count. The wheel is logged as delivered. The guest page still does not move up about one notch. Frames are in `tmpcode/qa-211/`. Command output is in `docs/agents/reports/TASK-20261014-211-QA01-to-PM01.md`. Pid 38920 was not stopped. The guest was not powered. Tester note uses LinearThrone.

## Operator note (TASK-20261014-212)

Same wheel, second reason the lesson stays put. Maestro only scrolls when the pointer is in the center vertical section of the window (the lesson column). A Page Up tap with the pointer elsewhere does not move that page. The fix seats the guest pointer at the framebuffer center and left-clicks there before the queued Page key. This is not a second issue.
