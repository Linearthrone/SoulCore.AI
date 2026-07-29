# Schema (DBD-01)

Canonical SQL from **DBD-01 TASK-007**:

- `001_schema.sql` — episodic / emotion / charter / config + sqlite-vec stub
- `../Migrations/001_initial.sql` (+ rollback)
- `../Scripts/create-empty-db.ps1`

**BED-01 TASK-011:** `SqliteMemoryStore` embeds and applies `001_schema.sql` then `001_initial.sql` on first open to a configurable path under `%LOCALAPPDATA%/SoulCore/memory/` (not LLMOD `Data/`).

**BED-01 TASK-157 / ISSUE-002:** `005_episodic_source_model` expands `episodic_memories.source` CHECK to include `'model'` (slots 003/004 reserved by BED-140/141).
