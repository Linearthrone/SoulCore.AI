---
type: task
prop_id: PROP-15.22b
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: QA-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
qa_report: docs/agents/reports/PROP-15.22b-QA01-to-PM01.md
depends_on: PROP-15.22 (Pass), PROP-15.21 (Pass)
fed_report: docs/agents/reports/PROP-15.22-FED01-to-PM01.md
ops_report: docs/agents/reports/PROP-15.21-OPSHOME-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Smoke — persona switcher + Presence/Host version currency
---

# PROP-15.22b — QA smoke

## Prereqs

- Host CreatorEdition with `/api/personas` 200 (15.21)
- Run **current** ChatDesktop from this tree (Release) so 15.22 UI is present

## Checks

1. Switcher lists **DisplayName**s; activate another pack → quarantine confirm → success notice; ↻ refresh works.
2. Title bar / Settings → Updates: Presence version, Host version, Presence currency line, Host personas OK (not “outdated”).
3. Optional: stop Host or hit old Host — status shows persona-API missing (if easy without breaking feel-drive pin; restore Host after).

## Report

`docs/agents/reports/PROP-15.22b-QA01-to-PM01.md`
