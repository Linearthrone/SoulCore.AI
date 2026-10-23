---
type: proposal
status: complete-client
tt_id: TT-01
prop_id: PROP-16-presence-glass-overlay
created: 2026-10-21
updated: 2026-10-22
title: Presence glass overlay — drawers, traits, live quarter-screen guest
need: A frameless white/blue glass overlay that shows every current Presence feature, with persona drawers, a trait catalog, and a live VirtualBox view
pm_route: B
pm_accepted_at: 2026-10-22
pm_note: Ticketed chain closed 2026-10-22. Avenue B drawers, catalog, archive, and chat-multi accepted on builds and tests. 16.2 stopped: the guest is an opaque still, not a scaled live window. No running Presence window was clicked. Commit pending.
pm_tickets:
  - docs/agents/log/PROP-16.0-PM01-routing.md
  - docs/agents/log/PROP-16.1-PM01-to-FED01.md
  - docs/agents/log/PROP-16.2-PM01-to-FED01.md
  - docs/agents/log/PROP-16.2b-PM01-to-FED01.md
  - docs/agents/log/PROP-16.3-PM01-to-BED01.md
  - docs/agents/log/PROP-16.3b-PM01-to-FED01.md
  - docs/agents/log/PROP-16.4-PM01-to-BED01.md
  - docs/agents/log/PROP-16.5-PM01-to-FED01.md
  - docs/agents/log/PROP-16.6-PM01-to-BED01.md
  - docs/agents/log/PROP-16.6b-PM01-to-FED01.md
---

# Presence glass overlay

Accepted by PM-01 on 2026-10-22 as **PROP-16-presence-glass-overlay**. Avenue **B**. The ticketed chain closed the same day. The live guest stopped at 16.2; the viewport is an opaque still.

Review frames (static art, not XAML):

| Frame | File |
| --- | --- |
| Idle desktop | [glass-01-idle-desktop.jpg](assets/presence-glass-overlay/glass-01-idle-desktop.jpg) |
| Radial menu | [glass-02-radial-menu.jpg](assets/presence-glass-overlay/glass-02-radial-menu.jpg) |
| Chat, one persona | [glass-03-chat-one.jpg](assets/presence-glass-overlay/glass-03-chat-one.jpg) |
| Chat, many | [glass-04-chat-many.jpg](assets/presence-glass-overlay/glass-04-chat-many.jpg) |
| Live viewport | [glass-05-viewport.jpg](assets/presence-glass-overlay/glass-05-viewport.jpg) |
| HOUSE lamps | [glass-06-house-lamps.jpg](assets/presence-glass-overlay/glass-06-house-lamps.jpg) |
| Settings | [glass-07-settings.jpg](assets/presence-glass-overlay/glass-07-settings.jpg) |
| Create agent | [glass-08-create.jpg](assets/presence-glass-overlay/glass-08-create.jpg) |
| Contacts | [glass-09-contacts.jpg](assets/presence-glass-overlay/glass-09-contacts.jpg) |

Sample names on the frames (Nova, Quill, Iris, Sol, Mira, Juno) are art only. They are not packs.

## 1. Need / Want

LinearThrone wants the CreatorEdition Presence shell rebuilt as a futuristic glass overlay: frameless drawers, a left-edge radial for personas, a system-tray app with no taskbar button, personality traits that change behavior, and a live VirtualBox view at one quarter of the screen. Mockups come before any UI code.

## 2. Goal & Success Criteria

1. The running product stays Avalonia 11.3 (`House/House.ChatDesktop`) talking to Host on `127.0.0.1:7700`. No new UI toolkit.
2. Every current surface stays findable: composer (send, quote, attach, hold-to-talk, video stub), mood correction, browser pane, desktop stills, HOUSE lamps (SoulCore, Ollama, Unreal, Comfy, CUA, Sandbox), and Settings sections Identity, Tools & Access, Email, System, Services, Notifications, Updates.
3. Tray right-click: Open main drawer, Open settings, Exit. The process is absent from the taskbar. A tested show/quit hotkey exists before `ShowInTaskbar=false` ships.
4. Left-edge half-circle grows about 4x into a radial: Create Agent, Agent Contacts, up to six favorite slots (large photo and name).
5. Chat lists open threads with each persona’s name and picture. One mode parks everyone but the focused persona. Many mode lets several personas chat, each against their own memory. Tools, the VM, and SoulLoop belong to the focused persona only.
6. Trait catalog: top 30 in creation, top 50 behind “More traits.” Skipped traits store the default. The prompt still emits a short banded block (cap eight lines), not one line per slider.
7. Physical description is its own field. An AI help button calls the local model and fills the box. LinearThrone accepts the text. The call cannot edit LIMITS or tool flags.
8. Viewport drawer: live VirtualBox guest, top-left, one quarter of the work area, thin glass border. Guest pixels stay sharp. A clickthrough toggle sits on that drawer’s top edge, default off. That drawer has its own opacity slider, separate from chrome opacity.
9. Archive hides the thread and writes a short summary into that persona’s episodic store only.
10. Exit, crash, and update-restart clear the CUA lamp.

## 3. Context & Constraints

| Fact | Note |
| --- | --- |
| Shell | Presence is already frameless (`SystemDecorations=None`). There is no tray and Close exits. Theme is dark-only in `App.axaml`. |
| Personas | PROP-15 shipped `PersonaPack`, six trait scales, band compiler in `SoulCore/SoulCore.Core/Persona/PersonaTraitCompiler.cs`, per-persona SQLite. `ActivePersonaSession` keeps one pack warm. A switch disposes the previous bundle. |
| Local transcripts | `%LocalAppData%\HouseVictoria\presence-chat.db` is one thread (`presence-local`), not keyed by persona. Live turns already write episodic rows on the active persona. |
| Viewport today | Stills plus optional `SetParent` of a window matched by `VmWindowTitle`. Not a scaled live guest of the whole VM. PROP-14 (HWND embed) is still in progress. |
| Tools | Pack `toolPolicy` is prompt text. The CUA lamp is process-wide. Dispatch does not read the pack flags. |
| Updates | Velopack path already exists (`MainWindow.Updates.cs`). Version numbers on the settings frame are placeholder art. |
| Naming | Docs call the operator LinearThrone. Copy a persona speaks uses that pack’s human-address field. |

## 4. Clarifying Q&A (answered)

| # | Lock (2026-10-21) |
| --- | --- |
| **Q1 Many-active** | **Chat-multi.** Several personas can chat at once, each with their own memory. Only the focused persona owns tools, the VM, and SoulLoop. Full parallel tools, and a VM per persona, stay parked. Default mode is still One. |
| **Q2 Viewport** | **Live VirtualBox guest**, scaled to one quarter of the display, thin glass border. Guest picture is not frosted. Top-edge **clickthrough** toggle. **View opacity** is a separate slider from chrome opacity. |

## 5. Avenues Explored

### Avenue A — Reskin the one window

Keep a single `MainWindow`. Add tray, hide the taskbar button, restyle to white/blue glass, turn the side column and HOUSE lamps into in-window panes.

Fastest reuse of the HWND slot. Poor fit for an edge radial and for a desktop that shows through. CONTRA’s preferred first cut if the live guest prototype fails hit-testing.

### Avenue B — Tray-owned drawers (recommended)

One process. Drawers are separate frameless windows where there is no HWND child: chat, settings, creator, contacts, HOUSE lamps, radial. The viewport window is the one that owns the live guest. Chrome opacity does not apply to guest pixels. Clickthrough is a toggle on that window only, default off.

### Avenue C — Shell, trait catalog, and runtime multi-active in one cut

Rejected. Same big-bang PROP-15 already parked. The compiler, the store hub, and a scaled guest do not belong in one milestone.

### Seats

- **STRAT:** Mockups, then shell on the current single-active runtime, then a capped trait catalog, then runtime multi. Top 30 does not enter the compiler as thirty lines. Top 50 is an advanced page under the same cap.
- **CONTRA:** Kill tray-only with no escape hatch. Kill a transparent overlay that parents the guest HWND. Kill injecting 30–50 traits into every prompt. Kill archive that crosses personas. Kill an AI rewrite that can touch LIMITS. Smallest fantasy if the live guest fails: reskin, and keep the VM an opaque island.
- **SYS:** Overlay rehost is a medium lift. Trait expansion stays banded and short. Multi-active is N chat sessions on one loopback Host, several SQLite bundles open, one foreground owner for tools, VM, and SoulLoop. Do not run N Host processes. Archive is a one-shot write into that persona’s file. Description assist is a local complete that returns text only.
- **RISK:** Trait text is style, not a safety control. Description assist is an untrusted local write the operator accepts. Tool authority has to be code, not prompt. CUA stays an operator lamp, off at process start, cleared when the UI dies. Update feed stays pinned. Consent dialogs sit above the glass and are not click-through.
- **USER:** Chat is home. Radial launches people. HOUSE lamps stay their own drawer. Creation shows one trait group at a time. Favorites, contacts, and active chats are three different lists.

**Conflict:** STRAT and the locked Q2 ask for a live quarter-screen guest. CONTRA says that HWND, plus clickthrough, is the historical failure (hit-testing, focus, DPI) and that whole-window opacity will not fade a Win32 child. The recommendation keeps the lock and treats CONTRA’s failure as a gate: if a prototype cannot scale the guest, keep clicks honest, and leave consent dialogs above the glass, stop and fall back to Avenue A for the viewport only. Do not frost the guest to fake it.

## 6. Recommended Route

**Avenue B**, with Q1 and Q2 locked.

```text
Tray
  ├─ main chat drawer
  └─ settings drawer
Left half-circle → radial
  ├─ Create Agent
  ├─ Contacts
  └─ up to 6 favorites → open that chat
Chat drawer
  ├─ HOUSE lamps
  └─ viewport (live guest, top-left, 1/4 screen)
```

### Phase 0 — Frames (this proposal)

Static frames above. Art caveats, not spec changes:

- Idle frame matches: empty desktop, edge tab, tray glyph, app absent from the taskbar. The tab reads as a circle clipped by the edge; the build is a half-circle.
- Radial frame is centered on the canvas. The build grows it from the left-edge tab. The frame duplicates the name Iris. Favorite names must be unique. The PIN slot and the MANY chip are right.
- Chat-one matches the composer (Attach, Hold to talk, Video, Send) and a Replying chip.
- Chat-many shows the foreground label and live rows. The list uses symbols instead of portraits. The build uses each persona’s picture.
- Viewport frame matches the lock: sharp guest, thin glass border, CLICKTHROUGH, VIEW OPACITY, tab still reachable. It does not show the browser pane or desktop stills. Those stay a stage surface so they are not dropped.
- HOUSE frame matches the six lamps, SoulCore “hold to stop,” CUA as a toggle, Unreal / Comfy / Sandbox marked status. Ollama stays a real lamp, not status-only.
- Settings frame shows theme, chrome opacity, check for updates, and the section tabs. `1.4.2` is not a real version.
- Create frame shows physical description, AI HELP, one slider group, DEFAULT badges, and MORE TRAITS. The build still walks template → identity and portrait → model → trait groups → blurb → quarantine confirm.
- Contacts frame shows a large photo, PIN, EDIT, OPEN CHAT, and the quarantine line. The close glyph on that frame is extra chrome. Drawers stay frameless; dismiss is the tray, the tab, or a drag-away, not a window caption.

### Phase 1 — Shell

Tray, hotkey, frameless drawers, light and dark brush sets, chrome opacity with a floor around 0.55 so text stays readable. Reuse composer, mood correction, update toast, and wizard steps. Radial is custom-drawn. Favorites cap at six. Local transcripts become per persona (the current `presence-chat.db` thread is not quarantined). Rewrite button may be visible and disabled in this phase.

### Phase 2 — Live viewport

Quarter-screen top-left drawer. Live VirtualBox guest, scaled, thin glass border, guest pixels opaque and sharp. Clickthrough toggle on the top edge, **default off**. View opacity is its own setting and does not drive chrome opacity. Clickthrough and view opacity do not apply to other drawers. While a tool confirm, quarantine confirm, or update apply dialog is open, clickthrough turns off and CUA input pauses. Browser pane and desktop stills stay reachable (stage drawer or a second region). This phase does not re-implement PROP-14; it hosts the guest. If `SetParent` cannot scale and hit-test, stop and report — do not ship a frosted fake.

### Phase 3 — Traits and description

Schema: each trait is stored at its default when creation skips it. Slider is one 0–1 influence, snapped with today’s edges (under 0.34 / under 0.67 / else). Compiler emits at most eight directive lines: boundary language when set, then the traits furthest from their default. Traits sitting on the default stay out of the prompt. Sliders never grant tools, never say obey, and never outrank hard policy.

Physical description is a separate, length-capped field. AI help is a loopback Host route through the existing local model (pack model, else Host model). No tool loop, no episodic write, no cloud rewrite. The operator saves. The rewrite cannot edit or delete LIMITS, tool flags, or memory.

### Phase 4 — Chat-multi

Settings or radial chip, default One. Many: several open chats, each turn pinned to that `personaId` for context, transcript, and episodic write, with more than one SQLite bundle open. HOUSE lamps and the viewport always name the foreground persona. Background personas chat only. They do not drive the guest, browser, email, or SoulLoop. In-flight tool calls die on foreground change. Process-per-brain stays parked.

### Trait catalog

Existing six stay the first voice and recall traits so current packs still read. Blank-pack defaults for those six match today’s starter where they already exist. One slider each. Default in parentheses.

**Top 30 (creation, grouped).**

Voice: Warmth (0.50), Directness (0.50), Formality (0.45), Playfulness (0.40), Verbosity (0.45), Expressiveness (0.45).

Judgment: Boundary strictness (0.55), Skepticism (0.50), Caution (0.55), Candor (0.50), Privacy instinct (0.60), Risk tolerance (0.40).

Thinking and drive: Curiosity (0.55), Analytical depth (0.50), Creativity (0.45), Structure (0.50), Patience (0.55), Focus (0.60), Initiative (0.40), Persistence (0.50), Assertiveness (0.45).

With people: Empathy (0.55), Collaboration (0.60), Teaching stance (0.45), Recall bias (0.50), Agreeableness (0.50), Optimism (0.50).

With other AIs: Deference to other AIs (0.40), Peer-challenge (0.45), Loyalty to the operator when other AIs disagree (0.70).

**Top 50 adds these 20** on the advanced page. Same default-if-skipped rule. They join the eight-line prompt only when they are among the furthest from default.

Humor dryness (0.40), Sarcasm restraint (0.60), Storytelling (0.40), Metaphor use (0.35), Conciliation (0.55), Competitiveness (0.35), Humility (0.55), Confidence (0.50), Spontaneity (0.40), Planning horizon (0.50), Detail orientation (0.55), Abstraction (0.45), Practicality (0.60), Nurturance (0.50), Stoicism (0.45), Play with ideas (0.45), Citation habit (0.50), Question-asking (0.50), Continuity callbacks (0.55), Adaptability (0.50).

How a persona treats other AIs is those three top-30 traits, not a second copy of the whole list.

## 7. Alternatives (parked)

- Avenue A as the whole product. Use it only as the viewport fallback if the live guest prototype fails.
- Avenue C, one-shot shell plus compiler plus multi-brain.
- N Host processes, or a VM per background persona.
- Injecting all 30 or all 50 traits into every prompt.
- A second weight slider on top of the 0–1 trait. The influence slider *is* the trait scale.
- Shared episodic memory. Later shared knowledge stays the PROP-15 idea of a separate MCP server, not a hole in quarantine.
- Clickthrough default on.

## 8. Risks & Kill Criteria

Do not build while any of these are still true:

- Tray-only with no show/quit hotkey. A missing tray icon must not brick open or exit.
- Guest HWND parented into a click-through or frosted window. Clickthrough default-on is a kill. Guest pixels painted as frosted glass are a kill.
- Clickthrough left on while a consent, quarantine, or update dialog is open.
- All 30 or 50 traits injected into every prompt.
- Archive or description assist that can write another persona’s database, or that can edit LIMITS or tool flags.
- CUA lamp left armed after UI exit, crash, or update restart.
- Update check that a settings field or the persona can retarget off the pinned feed.
- A build whose UI drops hold-to-talk, updates, HOUSE lamps, the browser pane, desktop stills, or the VM slot.
- Background personas in Many mode allowed to call tools or drive the guest.

Must-mitigate before the viewport and Many mode ship: pack tool policy enforced in dispatch, not only in the prompt; turns pin `personaId` before any await; local `presence-chat.db` keyed by persona; consent UI opaque, topmost, and not under the glass.

## 9. Open Questions for User / PM

1. Frame approval: are these nine frames the visual target, with the art caveats in Phase 0 corrected in the build?
2. Hotkey chord for show/quit when the tray icon is missing. TT does not pick it.
3. Where the browser pane and desktop stills live once the quarter-screen drawer is the live guest (stage drawer beside chat, or a tab on the viewport window). They stay. The frame does not yet show them.
4. Real version strings on the settings drawer come from the existing Presence and Host build lines. The `1.4.2` on the frame is art.

Resolved and not open: Many means chat-multi with one tool owner. The viewport is a live scaled guest with its own opacity and a clickthrough toggle defaulting off.

## 10. Suggested PM Handoff

Not sent. When LinearThrone says to send, assign `PROP-16-presence-glass-overlay`, set `status: sent-to-pm`, and file `docs/agents/tasks/PROP-16-TT01-to-PM01.md`. Do not start XAML from this file.

Suggested splits after that send (PM may renumber or drop):

- `PROP-16.1` FED — tray, hotkey, frameless drawers, light/dark, chrome opacity, radial, per-persona local transcripts. Reuse current panels. No new traits.
- `PROP-16.2` FED — live quarter-screen VirtualBox drawer, thin glass border, view-opacity slider, clickthrough default off, consent pauses clickthrough. Browser pane and stills stay reachable. Stop if the guest cannot scale and hit-test.
- `PROP-16.3` BED — archive summary write into that persona’s episodic store only (`source=archive`). No cross-persona write.
- `PROP-16.4` BED — trait schema for the top 30 / top 50, defaults, eight-line compiler cap, tests that a band change is visible and that LIMITS still win. Physical-description field plus local rewrite that cannot save the pack.
- `PROP-16.5` FED — creation and contacts drawers bound to that schema. AI help fills the box; the operator saves.
- `PROP-16.6` BED — chat-multi: N pinned sessions, N open store bundles, one foreground owner for tools, VM, and SoulLoop. Pack tool policy enforced at dispatch. CUA clears when the UI dies.

What PM should decide first: approve or mark up the frames, then only ticket `16.1` until the viewport prototype in `16.2` either works or falls back to an opaque island inside the current window.
