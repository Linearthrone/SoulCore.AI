---
type: note
from: PM-01
id: TINA
created: 2026-07-29
re: TASK-159 / OPS-143
---

# PM decision — TT-159 Avenue C accepted

**Chose:** Native-first Phase C/D (Avenue C).

- Keep Hermes `:8642` available; `Hermes.Enabled=false` until MCP quarry exists.
- Reticket / amend BED-135 / 136 / 138: **native C# backends required** for cloud Pass; Hermes MCP is parallel stretch via OPS-164 when quarry artifacts land.
- Do **not** wait on full LLMOD on Linux.
- Phase F (BED-144 / QA-145) stays gated on MCP sync.

Next execution tickets: BED-135 (desktop native) now; BED-136/138 after 135 pattern lands; SLOP-160 on Phase E (140/141).
