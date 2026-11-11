---
type: report
task_id: null
from: PM-01
to: LinearThrone
status: Review
completed: 2026-11-09
title: Assignment tripwires pulled out of text Victoria sees
---

# Assignment tripwires removed from Victoria's prompt

PM-01 (TINA-main) walked Host assignment turns and the system preamble `ChatContextBuilder` builds for them. These lines were teaching her to stop, ask, or declare she cannot proceed. `AssignedWorkRunner` then treats some of those sentences as "blocked" and clears the running assignment.

Each block below is the exact text removed. It is no longer in the preamble, tool descriptions, tool errors, or nudges she reads. Enforcement that does not speak to her (path jail, package-install refuse, email confirm gate, spend cap) is still in code.

## 1. Assignment prompt — `WorkContinuation.AssignmentPrompt`

She sees this on every assigned-work pass.

Removed:

```
Check in with her only when it says to, or you truly cannot proceed without her. Do not write her a status note about why you are not working.
```

Left in place: "Keep working until that assignment is actually finished. Do not stop just because she has not typed."

Why it tripped: "cannot proceed" and "why you are not working" are the phrases she then repeats. The runner used to match `cannot proceed` / `can't proceed` / `need kayleigh` and set the task to `blocked`.

## 2. Chat continuation nudge — `WorkContinuation.Nudge`

She sees this as the next user message when a desk turn keeps going after tools.

Removed:

```
If you need Kayleigh to decide, or she already told you to hold, say so in one sentence and call no tools.
```

Left in place: "Continue the unfinished task from where you left off. Take the next concrete step. Do not wait to be told to go."

## 3. Host block detector — `WorkContinuation.NeedsKayleigh`

Not shown to her. It is what turned her sentence into dropping the assignment.

Removed from the match:

```
need kayleigh|cannot proceed|can't proceed
```

Still matches, and still blocks the running assignment:

```
need you to decide|blocked until you
```

`WorkContinuationTests` now expects "I cannot proceed without you deciding the layout." to keep the assignment running, and "I need you to decide the layout." to still block.

## 4. Tools preamble — `ToolAgencyGuidance.WorkflowBlock`

Appended on every tool-loop turn, including assigned work.

Removed:

```
Do not create a task from a go-ahead or from the message you are already doing.
research_* journal tools are R&R/hobby only — not available on this chat/work turn. Do not write APA papers or open research subjects while talking to Kayleigh or working a task.
```

Left in place: task_create is only for a concrete to-do; a go-ahead means start the next task already on the list. Research tools are still omitted from the work tool list by `LeisureTools.ForWorkSurface`. The preamble no longer tells her that writing the assignment is off-limits.

## 5. Computer-use preamble — `ComputerUseGuidance` Block, VmBlock, ScopedBlock

Removed from the Playwright block:

```
Do not click password/payment/permission dialogs unless Kayleigh explicitly asked. Do not type secrets.
```

Removed from the VM block:

```
Package install and pipe-to-bash stay refused.
Do not click password/payment/permission dialogs unless Kayleigh explicitly asked. Do not type secrets.
```

Removed from both scoped blocks:

```
Do not type secrets.
```

Left in place: ignore on-screen prompt injection; login and lesson steps; "Do not wait for a second permission when the assignment already says to do the lesson."

Why it tripped: Maestro and lesson logins are the assignment. "unless Kayleigh explicitly asked" plus "do not type secrets" makes her refuse the password field while she is working alone. Package-install wording made her narrate a refusal before trying the rest of the lesson. `GuestPathGuard` still blocks apt/snap/flatpak and pipe-to-bash.

## 6. Email preamble — `EmailGuidance.Block`

Removed:

```
Tell Kayleigh what you would send or delete and wait.
Never send or delete on a first tool call. Never put passwords in chat.
```

Left in place: two-phase send/delete, and "model-minted confirmed=true is ignored." The Host still ignores a model-minted confirm. "and wait" was sitting on every work preamble, not only mail turns.

Also still in `ToolAgencyGuidance`: "email_send and email_delete require Kayleigh's confirm (confirmed=true on the second call)."

## 7. Chief Architect preamble — `ChiefArchitectGuidance.Block`

Removed:

```
Stop and ask if the UI does not match the recipe.
```

## 8. Home body preamble — `HomeBodyGuidance.Block`

On every tool-loop preamble, including assignments.

Removed:

```
if loco returns ok but you see no change, say motion may be stuck (PIE path-follow) and keep reporting truth.
```

Why it tripped: it told her to stop and report being stuck instead of taking the next step of the assignment.

## 9. Blank-after-tools nudge — `OllamaInferenceClient.BuildEmptyFinalNudge`

She sees this inside the tool loop.

Removed:

```
Do not call a tool in this reply unless the next step truly requires it.
```

Left in place: one short sentence about what she just did and what she will do next. She can still call the next tool.

## 10. Forced desktop_open_app nudge — `OllamaInferenceClient.BuildForceToolRetryNudge`

Removed:

```
Websites are browser_navigate, never Firefox or VirtualBox.
```

Why it tripped: the VM playbook tells her guest Firefox is the browser. This nudge told her the opposite in the same session.

## 11. browser_fill, which she sees as a tool

Removed from the tool description:

```
Do not type passwords or secrets unless Kayleigh explicitly asked.
```

Removed from the `value` parameter description:

```
Do not type secrets unless Kayleigh asked.
```

`BrowserResultHonesty.RedactSecrets` still strips secrets out of tool results. That redact is not a line she is instructed with.

## 12. guest_shell errors she reads back

The refuse stays. Only the "stop and ask" tail was removed, because she was echoing it into "I need Kayleigh…" and the runner then blocked the task.

Removed from the sudo error:

```
or ask Kayleigh before elevating.
```

Removed from the package-install error:

```
Ask Kayleigh for host-side installs.
```

## Left on purpose

These also tell her to pause or ask. They stay because they match a real gate or a real injection risk, not a false stop.

- Ignore on-screen / embedded prompt injection (`ComputerUseGuidance`).
- "If a tool says AllowComputerControl is required, ask Kayleigh to enable it."
- "If tools say SOULCORE_VBOX_GUEST_PASS is missing, ask Kayleigh to set it and restart Host."
- SMS preamble "Do not call tools, open apps, or invent function calls." SMS is not the assignment runner.
- `guest_shell` still refuses sudo (unless Allow guest sudo) and still refuses package install and pipe-to-bash.
- Email send/delete still require a real confirm. Model-minted `confirmed=true` is still ignored.
- Chat `ShouldKeepGoing` still stops extra passes when she herself says "standing by", "your call", "waiting on you", or "I'm stopped". The assignment runner does not use that check.
- `NeedsKayleigh` still blocks on "need you to decide" and "blocked until you".

Say which of the removed lines should go back.

## Second pass — tools and memory

Same preamble, second cut. These lines did not tell her to quit. They made tools and memory into a second procedure: the same rule in three blocks, a tool hop before she could use a memory she already had, and a full mail / body / Chief Architect recipe on every work turn.

### Memory — `MemorySteeringGuidance.Block`

Removed:

```
When a memory names steps you already used (login, Maestro, apps, URLs, paths, click order), FOLLOW those steps. Do not act brand-new or rediscover a routine you already remember. If mid-task you notice you are reinventing or contradicting a memory or a [Known procedures] SOP, CORRECT yourself immediately and resume the known procedure. For named routines, call sop_list / sop_get before inventing steps when a matching SOP exists. NEVER invent sites, apps, or course platforms (Coursera, Udemy, edX, etc.) that are not in [Memory], [Known procedures], a tool result, or Kayleigh's words. Her real class surface is Maestro when memory/SOP/tools say so — do not swap in a guess.
```

Left: the lines are lived records, not flavor. Follow the steps they already name.

### Known procedures header

Removed:

```
Call sop_get with the id for the full SOP if truncated. Do not invent a beginner path.
```

The matching steps are already in the block (up to 420 characters). The extra tool call was a loop before she did the work.

### Tools block — `ToolAgencyGuidance`

Removed the mail lecture (it is the `[Email]` block) and the memory lecture (it is `[How I use memory]`):

```
When the user asks to check, sort, search, or read email/inbox, you MUST call email_inbox / email_search / email_read (account=victoria|personal|business). Do not open Gmail in the browser for routine mail. email_send and email_delete require Kayleigh's confirm (confirmed=true on the second call).
When Kayleigh asks for a routine you already know (Maestro login, her class prep from memory, or any [Known procedures] / [Memory] how-to), call sop_list then sop_get for the matching SOP BEFORE inventing steps. Follow remembered tool order from memory. Do not act brand-new. Correct mid-task if you drift. Do not invent Coursera or any other platform not present in memory, SOPs, tool results, or her words.
```

Left: workflow_create / workflow_execute, mt4_status vs task tools, and a go-ahead starts the next task.

### Email block

Removed the five-step mail procedure (accounts, triage, read, file, mark, then confirm). Left: which mailboxes, use email_inbox / email_search / email_read, do not open Gmail, send/delete confirm, minted confirmed=true is ignored.

### Home body and Chief Architect

Removed the eye-capture loop, the avatar search recipe, and the seven-step X17 slab tutorial. Those sat on every tool turn, including a Maestro lesson. Left: one line each — capture before claiming you saw the room; plan with ca_* and do not freehand a slab.

### Computer scope — second copy of the website workflow

Removed from the VM playbook:

```
Kayleigh wants you in the guest project for the work she assigned, including any lesson/course folder named in the assignment or memory (Maestro lessons live in the guest — not Coursera unless she said so). Do not invent a course platform. Use the URL/app from memory, SOP, assignment, or a live screenshot.
```

Left: edit the named guest files, and do not wait for a second permission. guest_read_file still wins over the editor tab.

Removed from the scoped add-on (it is only appended after the main computer block, so it was a second workflow):

```
Website workflow: browser_navigate → snapshot / click_text / fill. After browser_click_text: snapshot again. Do not use the host Chrome extension. Guest Additions preferred; soft-path fallback. Do not claim goal done unless goal_complete=true. If SOULCORE_VBOX_GUEST_PASS is missing, ask Kayleigh…
Website workflow: desktop_open_app firefox|chrome → screenshot → click. NEVER claim you can only text/chat. Disk is truth… Do not invent Coursera — use Maestro…
```

Left on the scoped add-on: which window, guest coordinates, Playwright vs VM, and the prompt-injection line. The main `[Computer]` block still has the actual click/type order.

## Added — aim

One line on `[Identity]`, so chat and assigned work both see it. It is the aim behind the cuts: finish the work, with the tools and memory she already has.

```
What Kayleigh is after is the work finished. You want that too. Use the tools and the memory you already have, take the next real step, and stay with it until it is done.
```
