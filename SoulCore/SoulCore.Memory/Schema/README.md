# Schema (DBD-01)

Canonical SQL from **DBD-01 TASK-007**:

- `001_schema.sql` — episodic / emotion / charter / config + sqlite-vec stub
- `../Migrations/001_initial.sql` (+ rollback)
- `../Scripts/create-empty-db.ps1`

**BED-01 TASK-011:** `SqliteMemoryStore` embeds and applies `001_schema.sql` then `001_initial.sql` on first open to a configurable path under `%LOCALAPPDATA%/SoulCore/memory/` (not LLMOD `Data/`).
