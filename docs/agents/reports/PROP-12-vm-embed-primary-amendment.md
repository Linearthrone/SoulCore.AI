---
type: pm-decision
from: operator + cloud agent
created: 2026-10-08
prop_id: PROP-12-amend
title: VM embed is primary browser/desktop (supersedes Playwright-primary for desk)
status: decided-and-shipping
---

# PROP-12 amendment — Her screen VM embed is primary

PROP-12 locked Host Playwright for web when the VM path was thrashing. With
Presence **SetParent** of `victoria-sandbox` into Her screen, the desk product
is now:

1. **Web / login / desktop** → Ubuntu guest in VirtualBox (`BrowserBackend=native`,
   `DesktopTargetWindowTitle=victoria-sandbox`, `VmEmbedPane=true`).
2. **Playwright** → opt-in only (`SOULCORE_Tools__BrowserBackend=playwright`).

Victoria must never claim she can only text in chat; tool failures should name
the real error (VM off, missing `SOULCORE_VBOX_GUEST_PASS`, etc.).

**Follow-up (Host 0.1.9):** Login / page NL ForceTool on `BrowserBackend=native` is
`desktop_screenshot` (not BED-194 `browser_click_text`). Playwright opt-in keeps
`browser_click_text`. Kayleigh `click (x, y)` pastes ForceTool screenshot first.
