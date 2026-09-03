# Coding conventions (House / SoulCore)

Apply across the whole repo. Prefer one way of doing each thing.

## General

- Delete code that is not serving the product. Temporary bridges are OK if **documented as temporary** and not expanded.
- No committed secrets, MDNs, tokens, Host `bin` trees, QA evidence logs, or `artifacts/` publish dumps.
- Prefer small, focused files; thin Host wiring; logic in testable services.

## .NET / C#

- `net8.0`, nullable enabled, implicit usings.
- No `any`-shaped public contracts — use typed options / records.
- Await all async DB/HTTP/scheduler calls.
- Public companion APIs: validate inputs; return structured JSON errors (no empty 500s).
- DI: required dependencies in constructors (do not hide with `= null` optionals unless truly optional).
- Indexes for lookups; no unbounded `.Collect()` on hot paths without pagination.

## Tools

- Implement `ITool` with a clear name/description/JSON schema.
- Gate dangerous desktop/browser tools behind access settings.
- SMS inbound must not invoke the tool loop.

## Config

- User-facing secrets via `SoulCore/.env` (`SOULCORE_*`); `.env` overwrites stale process/User env.
- Defaults in `appsettings.json` must match Example + runtime remaps (no `Mt4Backend=hermes` leftovers).

## Docs

- Architecture & workflows → `docs/handbook/` (this site).
- Step-by-step ops → `docs/runbooks/`.
- PROP tickets → `docs/agents/` (do not duplicate architecture there).
- Do not leave conflicting registries (one PROP table only: `PROP_NUMBERING.md`).

## Frontend (ChatDesktop / Android)

- Follow existing project patterns; do not introduce a second UI stack for the same job.
