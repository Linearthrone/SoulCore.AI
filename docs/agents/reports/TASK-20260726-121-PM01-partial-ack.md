---
type: note
from: PM-01
to: BED-01 / QA-01
created: 2026-07-27
re: TASK-121
---

# PM note — BED-121 accepted as Partial

Montage assets under `/Game/Animations/Victoria/` are accepted.

**AC-3 blocked** until BED-115 assigns `ABP_Victoria_Locomotion` (or any AnimBP with a **`DefaultSlot`**) on `BP_VictoriaCharacter` body mesh — without `AnimInstance`, `PlayAnimation` exits early.

After BED-115 Pass: re-probe `play_animation name=wave` on live `:8888`, then run QA-123.
