---
type: proposal
status: accepted-pm-ticketed
tt_id: TT-01
prop_id: PROP-17-victoria-between-talks
created: 2026-10-12
updated: 2026-10-12
title: Victoria between talks — park dead work, self-started R&R, Presence shell
need: She keeps existing between conversations, stops hammering a dead task, researches on her own without a creativity clock, and Presence settings match how LinearThrone actually uses the house
sent_at: 2026-10-12
pm_intake: docs/agents/tasks/PROP-17-TT01-to-PM01.md
pm_accept: docs/agents/reports/PROP-17-PM01-accept.md
environment: TINA-main
pm_tickets:
  - docs/agents/tasks/PROP-17.1-PM01-to-BED01.md
  - docs/agents/tasks/PROP-17.2-PM01-to-BED01.md
  - docs/agents/tasks/PROP-17.3-PM01-to-BED01.md
  - docs/agents/tasks/PROP-17.4-PM01-to-FED01.md
  - docs/agents/tasks/PROP-17.5-PM01-to-FED01.md
pm_locks:
  leisure_model: qwen2.5:1.5b
  park_cooldown_min: 45
  repark_cooldown_min: 120
  identical_failure_threshold: 3
---

# Victoria between talks

## 1. Need / Want

Victoria is doing real work and getting lost in it. She opens many tabs to the same place, and she installed another browser that opened the same tabs again. When a site goes down mid-lesson she keeps trying to reconnect instead of setting the lesson aside. LinearThrone wants her to keep existing between conversations: do other released work if it can actually run, and otherwise follow something that interests her (research, notes, papers, pictures, YouTube) without a timer that cuts the thread off. He also wants to add and suspend tasks without deleting them, and a Presence settings shell that stops pretending several dead tabs and chrome buttons are useful.

Addressed to **TINA-main PM-01**. Execution stays on the House Victoria line (this repo). Not CreatorEdition.

## 2. Goal & Success Criteria

1. A dead surface (Maestro down, VM off, browser health fail, same URL error) parks the task after a small fixed number of identical failures. A failed tool call does not count as progress. She does not reconnect-loop. After a cooldown she probes once.
2. She continues other **released** tasks only when their surface is up. Suspended tasks are visible and are not started.
3. When nothing released can run, she self-starts creative research on a **small local model**. No wall-clock kill. No cloud fallback. Neural-interface design, papers, notes, and video are in scope.
4. She does not appoint herself to the work assignment. Buy, email, install, trade, or “drop the lesson and build it” is filed `suspended` for LinearThrone to release. The research keeps going.
5. One Thread stays the conversation. Work and R&R beats do not bury it. The activity band says what she is doing, including a parked reason and the research thread.
6. Browser invariants: one live tab per URL, no second browser, no package-install of a browser.
7. Presence settings collapse to Identity, Tools & Access, and System, as specified in §6.
8. Host Stop still kills the process when start is stuck on “Starting Host…”.

## 3. Context & Constraints

- [`AssignedWorkRunner`](SoulCore/SoulCore.Host/Loop/AssignedWorkRunner.cs) loops while `assigned-work.json` is `active`. After three no-tool passes the label becomes “Stuck on the assignment — still trying” and the runner continues (backoff caps near 120s). Any `desktop_*` / `browser_*` / `guest_*` name resets that streak, including failures.
- [`WorkContinuation.AssignmentPrompt`](SoulCore/SoulCore.Inference/Presence/WorkContinuation.cs) says the assignment stays in force and she must not stop because nobody has typed. “Done” matches a Maestro phrase (“both lessons”). [`ComputerUseGuidance`](SoulCore/SoulCore.Inference/Tools/Desktop/ComputerUseGuidance.cs) says retry, and the VM playbook says open Firefox or Chrome with `desktop_open_app`.
- Two lists: one JSON slot (`active|waiting|cleared`) and SQLite `victoria_tasks` (`todo|in_progress|done|blocked`). The runner does not read the table. `waiting` means keep going, not hold. Promoting overwrites the slot.
- Journals are check-constrained to `feeling|animation|environment`. No SOP store. No life-hacks book.
- Chat, SMS, and the work runner share session `presence-local`. Continuation nudges land in the human thread.
- Inference is one chat model (`Inference:Model`, local `:11434` or Ollama Cloud). `ToolModel` / `ToolModelUeLive` already exist. There is no leisure lane. Embeddings already split to local when chat is cloud. The spend meter does not bound a local or mis-routed cloud loop (rates default to zero).
- Email send/delete and MT4 treat model JSON `confirmed=true` as consent. `POST /settings/tools` does not use the companion token that email settings use. `guest_shell` blocks sudo, not `apt` / `snap` / `flatpak`.
- Only charter `kind=identity` enters the chat preamble ([`ChatContextBuilder`](SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs)).
- Settings tabs today: Identity (essay + Save), Tools & Access, Email (three roles), System (endpoint, repo folder, host buttons that disable while starting), Services stub, Notifications, Updates. Title chrome: Update, Restart stack, Correct mood, Refresh. Composer sits outside `ChatPanel`, fixed one line. `LampSandbox` is disabled. PROP-16 update/restart behavior is in progress; relocate it, do not drop it.
- Human in docs and UI labels: **LinearThrone**. Copy Victoria reads about him: **Kayleigh**.

Prompt-only SOPs are a kill. The current loop was specified in prompt text.

## 4. Clarifying Q&A (answered)

- Creativity clock: no. The 20-minute stop was a spend/loop brake. It is overruled.
- R&R model: local leisure model, never the work model, never cloud fallback. If it cannot load (missing pull, or Unreal/voice need the card), she stays quiet on paper. She does not borrow the work model.
- Self-start: creative research is hers to open (example LinearThrone gave: design and research neural interfaces). Self-appointing the **work assignment**, or crossing the R&R denylist, is not.
- Tasks: add and suspend. No delete. Memory of a task stays. Suspended means not yet. Parked means she tried and the world failed.
- Email accounts: unlimited. Operator-only setup. Passwords write-only. Three-role cap is overruled.
- Mailboxes of three: overruled.
- Phone this round: mood word and doing-line only.

## 5. Avenues Explored

### Avenue A — Gate, then procedure, then R&R, then shell (recommended)

Park and the ledger first, then browser/SOP invariants, then R&R on the local model, then the Presence shell. Host stop-unstick ships with park because that is how LinearThrone pulls the plug today.

### Avenue B — Prompt-only

Rewrite the assignment prompt and hope. Rejected. The hosted service still loops while status is `active`, and a browser “interest” tool resets the stuck counter.

### Avenue C — Shell first

Reshape Presence before the runner can stop. Rejected. She keeps hammering for the whole UI project. The only piece pulled forward is Host Stop, and it belongs in the first slice.

## 6. Recommended Route

Behavior before chrome. R&R ships only after park, the browser invariants, the host denylist, and the untrusted-page rule exist.

### 6.1 Ledger, park, add / suspend

`victoria_tasks` is the ledger. It already has many rows and no SQL `CHECK` on status. `assigned-work.json`, if kept, stores only `{ runningId }`. Do not build a third queue.

Statuses: `todo`, `in_progress`, `done`, `blocked`, `parked`, `suspended`. The runner resumes only `active` work, meaning the row it was told to run (`in_progress` / the running id). It does not schedule `parked` or `suspended`.

- Identical failures increment a counter. A tool name is not success. Then `parked`, with a one-line why and a cooldown (default on the order of 30–60 minutes, PM may tune). One probe. Still down: park again, longer cooldown.
- Surface tag on each row: `maestro`, `browser`, `vm`, or `local`. A released task starts only when that surface is up.
- Done is a real status, not the “both lessons” regex.
- LinearThrone can add a task. Default `suspended` unless he marks it ready. Release returns it to `todo`. Suspending the running task stops it and leaves her notes. No delete control.
- A parked lesson is not deleted when another row runs.
- Presence activity band lists title, surface, status, why, add, suspend, release.
- Host start: timeout around `start-soulcore.ps1`, always clear “Starting Host…”, Stop stays enabled and force-kills the Host process. Tray/hotkey only if the whole Presence window is frozen. That case is a sibling.

### 6.2 House rules and notebooks

Code, not a document she can skip:

- One live tab per URL. Reuse `browser_tabs`.
- Refuse a second browser when one is open. Refuse install-a-browser.
- Site down means park, not refresh.
- `guest_shell` refuses package install and pipe-to-bash in **every** mode (`apt`, `snap`, `flatpak`, `wget|bash`). Compile and test under `/home/victoria` stay. This is how the second browser happened during assigned work.

SOP store (new table, charter-shaped: title, when, steps, don’ts). Seed browser hygiene and “site down means park.” She may add and revise. Authority: tool code, then charter, then operator settings, then her notes as recall. An SOP cannot flip a gate, mint `confirmed`, or call `guest_shell`.

Life-hacks is a fourth journal book. Migration on the journal `CHECK`. Do not pretend `feeling` is that book.

### 6.3 R&R on the local model

When nothing `active` can run and no released task has a live surface. Suspended rows are not a reason to skip R&R.

- `Inference:LeisureModel` on local `:11434`, same routing seam as `ToolModel` / `ToolModelUeLive`, and the embedding-style local base URL so a cloud `BaseUrl` is not reused. Empty does not mean “use the work model.”
- Exclusive inference lease and short keep-alive. The work runner does not tick during the lease. If the small model is not pulled, or Unreal/voice need the card, R&R idles. No cloud. No work-model substitute.
- No wall clock and no turn budget on the interest.
- **Research she opens is the point.** A notion (neural interfaces, a design, a paper, a video) starts without a task and without release. Activity line names it (“Reading about X · local”). Notes go to books or memory, not only the in-memory chat ring.
- **An assignment she must not appoint herself to.** Buy, email, install, trade, or replace the released lesson is inserted `suspended`. Chat text does not call the start-now path. She keeps researching.
- Light tools: look, read, write in her books, guest screenshots (Comfy only if that lamp is already up). Pages and YouTube stay in the guest browser, not host Playwright (host Chromium can reach `127.0.0.1:7700`).
- Host rejects, not prompt wishes: email send/delete, MT4 writes, `guest_shell`, settings changes, downloads, a new browser, form login. Strip `confirmed` so the model cannot mint consent. `POST /settings/tools` requires the companion token.
- Page text, comments, and screenshots stay inside that R&R turn. They are not copied into the next work or chat preamble. A kept summary is short and marked untrusted. It cannot name tools, mint `confirmed`, or change settings.
- A human or SMS turn aborts the in-flight R&R pass before the next preamble. She answers.
- Work and R&R continuation lines do not append to `presence-local`. One find worth reading is one message. The band carries the rest.
- Dead page during R&R uses the same park/probe rule. A living thread is not interrupted by a timer.

### 6.4 Presence shell

Three tabs. Delete Email, Services, Notifications, and Updates as tabs. House drawer lamps stay.

**Title strip.** Remove Update, Restart stack, Correct mood, Refresh. Keep PROP-16’s pipeline on System. An update-available mark beside presence/host opens that block. It is not a title-bar button.

**Identity, read-only.** Drop the essay and Save. Group charter anchors by kind. Rank from priority (stronger / mid / lighter). Mark identity-kind rows “in her prompt.” Mark safety, value, boundary, and ritual “stored, not in the prompt yet.” Do not pretend they already weigh. Wiring them in is a sibling.

Stats: memories (live, quarantined, newest), books (feeling, animation, environment, life-hacks), SOP count, tasks (released, suspended, in progress, done), workflows labeled as workflows, one prompt-mix line, parked row plus next probe, current interest. Name is Host contact text.

**Tools & Access.** Keep current gates. Add family switches: files on this PC, guest VM files and shell, browser driving (separate from capture), body / Unreal, Chief Architect. Leave list-tools, system info, and her tasks/workflows always on. Email setup is a button here. One dialog, one account, no role cap. After save, System lists mailbox name and owner. Passwords stay off that list. The cap to remove is `EnsureCanonicalSlots` plus UI and tool text hardcoded to `victoria|personal|business`. The store already accepts any id.

**System.** Drop endpoint (already in the title bar) and repo folder (scripts may still find it; do not show it). Left: Start / Stop / Restart Host and the companion token. Right, about half the width, two columns: bind, port, inference, memory path and open flag, SoulLoop, desktop / browser / MT4 backends, Presence and Host versions, STT/TTS health. Under the token: mailboxes. Then VM controls via existing `VBoxManage`: Start, Save state, Power off (power off asks). Operator buttons, not her tools. Do not auto-start the VM so she can keep working. One Refresh. Notifications and Updates sit in short blocks here.

**Voice.** No new tab. Health next to Hold to talk and in the System readout. Compact block: enabled, voice name, health. URLs under Advanced.

**Chat.** Composer inside `ChatPanel` so pop-out includes draft, Hold to talk, and Send. Wrap, grow with the draft, one line after send. Enter sends. Shift+Enter inserts a line.

**Band the composer leaves.** Always under the main split.

1. Doing — not the parked lesson, not the last order.
2. On her mind — in flight, or between talks the interest or quiet. Never the last order restamped as work.
3. Mood — word plus warm/cool, calm/keyed-up, steady, focused.
4. Influences — at most three.

Correct mood, if kept, is a disclosure in this band. Not chrome.

### 6.5 Phone

Mood word and doing-line only, if those frames already exist. Not this round: charter, stats, tool gates, Host/VM, updates, email setup, voice endpoints, the four-line pane. Android’s three-role email editor must not keep writing the old schema after unlimited accounts land. That follow-up rides the email change. It is not a phone redesign.

## 7. Alternatives (parked)

- Tray or hotkey Host kill if the Presence UI thread itself is frozen.
- Charter kinds other than `identity` actually entering the prompt.
- Spend-meter hardening. Do not claim the meter limits R&R.
- Guest firewall, curl egress, domain allowlists, HTML sanitizer, second GPU, separate chat product.
- Phone chrome beyond mood and doing-line.
- Full `guest_shell` binary approval and snapshot rollback. Package install and pipe-to-bash are in this proposal. The rest is not.

## 8. Risks & Kill Criteria

- Kill prompt-only park, prompt-only tab hygiene, and prompt-only denylist.
- Kill R&R if it can fall back to cloud or to the work model, or if page text is pasted into the next work preamble as instructions.
- Kill “she wrote an idea” if that path calls start-now on the work slot.
- Kill a second task list. One ledger.
- Kill a creativity clock. LinearThrone overruled it.
- Kill unlimited mail if send can still be self-confirmed. Strip `confirmed` before unlimited accounts are useful. Operator setup stays token-gated. Passwords stay write-only.
- Do not auto-start `victoria-sandbox` to keep her working. A down VM is a reason to park.
- Do not drop PROP-16 update/restart behavior when the chrome buttons move.

## 9. Open Questions for User / PM

- Which small local model name ships as the `LeisureModel` default. Not blocking. Missing pull means R&R stays quiet.
- Cooldown numbers (30–60 minutes, then longer). PM may tune. The shape (park, one probe, no loop) is locked.
- No product question remains on self-start versus suspend. Research starts. Action proposals wait.

## 10. Suggested PM Handoff

- `prop_id`: PROP-17-victoria-between-talks
- Intake: `docs/agents/tasks/PROP-17-TT01-to-PM01.md`
- For: **TINA-main PM-01**

Suggested splits (PM may re-divide). Do not start 17.3 until 17.1’s park, ledger, and host rejects exist, and 17.2’s tab/browser invariants exist.

- `PROP-17.1` BED-01 — Ledger on `victoria_tasks`; JSON pointer; park / suspend / release / done; surface tag; failed tool is not progress; runner only resumes the running row; strip `confirmed`; companion token on `POST /settings/tools`; `guest_shell` package-install refuse; Host start timeout and Stop force-kill.
- `PROP-17.2` BED-01 — One tab per URL, no second browser; SOP store plus two seeds; `life_hacks` journal migration.
- `PROP-17.3` BED-01 — `LeisureModel` local-only, inference lease, R&R allow/deny, untrusted observations, self-started research versus suspended action proposals, human/SMS abort, no continuation spam on `presence-local`.
- `PROP-17.4` FED-01 — Three settings tabs, title chrome removal, update mark, Identity portrait, email dialog, System layout, VM buttons, composer in the chat panel, activity band and task list.
- `PROP-17.5` FED-01 — Companion mood word and doing-line only. Android email editor follows 17.4’s account model. Do not redesign the phone.

What PM should decide first: ticket 17.1 before 17.3. The shell (17.4) can proceed in parallel only for layout that does not depend on new statuses. The activity band’s parked/interest lines need 17.1 and 17.3 phrases to be true.

TT-01 does not write the execution tickets.
