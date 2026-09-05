---
type: proposal
prop_id: PROP-7-hermes-dead-surface-cleanup
status: accepted-gated
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Hermes dead-surface cleanup — contracts, config, DI, docs honesty
need: Remove retired Hermes from live Host contracts/config so Ollama is the unambiguous inference boundary (NullHermesClient era ends)
parallel_with: PROP-11 (Memory-only), PROP-1/2/4
blocked_by: PROP-5 (sole Host lane until Pass)
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
sent_at: 2026-09-05
pm_intake: docs/agents/tasks/PROP-7-TT01-to-PM01.md
---

# Hermes dead-surface cleanup

## 1. Need / Want

PRODUCT_ROOT already claims Hermes is retired via `NullHermesClient`, but `IHermesClient`, `HermesOptions`, `BackendHermes`, PreferHermes remaps, and handler ctor params still shape startup and obscure the real Ollama-only boundary. Kurt wants dead surface gone — not a second inference stack.

## 2. Goal & Success Criteria

- No live Host DI requirement for `IHermesClient` / `HermesOptions` on the chat path.
- `ToolsOptions.BackendHermes` and remap branches removed or confined to an archived migration stub **outside** Host startup.
- `HermesToolRouting` and PreferHermes arms deleted or moved to `docs/archive` / migration-only package.
- Handbook / PRODUCT_ROOT / site blurbs that mention Hermes match reality after cleanup.
- Host boots; tool-loop tests green without Hermes test doubles.
- **Not** a ChatWebSocketHandler structural split (that is PROP-8) — delete-only on Hermes arms here.

## 3. Context & Constraints

- Soft Host lane: must not run parallel with another `Program.cs` / handler rewrite PR.
- Sequence: **after PROP-5 Pass** (PROP-5 may touch Program for charter ownership).
- Keep null-object pattern for other optional services untouched.

## 4. Clarifying Q&A

| Q | Default |
| --- | --- |
| Keep NullHermesClient type in repo for history? | Archive or delete; no Host registration |
| Full docs-site merge? | No — only fix Hermes truth claims |

## 5. Avenues Explored

- **A (recommended):** Delete/stop registering live contracts; fix tests; sync docs claims.
- **B:** Move to `SoulCore.Migration.Hermes` package — only if external consumer needs it (unlikely).
- **C:** Leave Null client forever — rejected (eval pain continues).

## 6. Recommended Route

Avenue A after PROP-5. Single BED seat. Docs honesty in same PR (small).

## 7. Alternatives (parked)

Full handbook↔docs-site tree merge (cluster map).

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Hidden PreferHermes config in deployed appsettings | Fail startup or remap log-once then remove |
| Test suite still constructs Hermes doubles | Update Protocol.Tests in same PROP |
| Expands into WS god-object split | Kill — defer to PROP-8 |

## 9. Open Questions

Any offline machine still setting `Backend=hermes` in secrets? (Assume no; confirm on send.)

## 10. Suggested PM Handoff

- `prop_id`: `PROP-7-hermes-dead-surface-cleanup`
- `PROP-7.1` BED — remove DI/config/handler Hermes surface + tests
- `PROP-7.2` DOC/BED — PRODUCT_ROOT + handbook Hermes claims
- PM decides first: confirm no machine still depends on Hermes backend strings
