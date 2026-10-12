-- Migration 008: PROP-17.1 park/suspend ledger fields on victoria_tasks.
-- Statuses remain unconstrained in SQL (enforced in IVictoriaTaskStore):
--   todo | in_progress | done | blocked | parked | suspended
-- surface: maestro | browser | vm | local
-- No delete API — rows stay for memory.

ALTER TABLE victoria_tasks ADD COLUMN surface TEXT NOT NULL DEFAULT 'local';
ALTER TABLE victoria_tasks ADD COLUMN park_reason TEXT NOT NULL DEFAULT '';
ALTER TABLE victoria_tasks ADD COLUMN park_until TEXT NULL;
ALTER TABLE victoria_tasks ADD COLUMN identical_failures INTEGER NOT NULL DEFAULT 0;
ALTER TABLE victoria_tasks ADD COLUMN last_failure_key TEXT NOT NULL DEFAULT '';
ALTER TABLE victoria_tasks ADD COLUMN notes TEXT NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS idx_victoria_tasks_surface
    ON victoria_tasks (surface);

CREATE INDEX IF NOT EXISTS idx_victoria_tasks_park_until
    ON victoria_tasks (park_until);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('008', 'victoria_tasks_park');
