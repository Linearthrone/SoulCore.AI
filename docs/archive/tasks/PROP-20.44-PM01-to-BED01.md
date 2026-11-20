---
type: task
prop_id: PROP-20.44
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-18
title: Bump product version 0.1.54 → 1.0.0
decision: docs/agents/unexecuted_proposals/prop-20-v1-release-decision.md
---

# PROP-20.44 — Version 1.0.0

**Activate:** `@Agents/BED-01.md`

Set **Version** and **InformationalVersion** to **1.0.0** in:

1. `Directory.Build.props`
2. `SoulCore/SoulCore.Host/SoulCore.Host.csproj`
3. `House/House.ChatDesktop/House.ChatDesktop.csproj`

Keep all three aligned. No feature changes. `dotnet build` Host + ChatDesktop Release evidence; `/health` version comes from Host assembly after OPS rebuild.

Report: `docs/agents/reports/PROP-20.44-BED01-to-PM01.md`
