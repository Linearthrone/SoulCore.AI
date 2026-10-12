-- Schema companion for migration 008 (PROP-17.1 park columns).
-- Documentation only — not embedded. Live DBs apply Migrations/008_victoria_tasks_park.sql
-- (includes indexes + schema_migrations row).
ALTER TABLE victoria_tasks ADD COLUMN surface TEXT NOT NULL DEFAULT 'local';
ALTER TABLE victoria_tasks ADD COLUMN park_reason TEXT NOT NULL DEFAULT '';
ALTER TABLE victoria_tasks ADD COLUMN park_until TEXT NULL;
ALTER TABLE victoria_tasks ADD COLUMN identical_failures INTEGER NOT NULL DEFAULT 0;
ALTER TABLE victoria_tasks ADD COLUMN last_failure_key TEXT NOT NULL DEFAULT '';
ALTER TABLE victoria_tasks ADD COLUMN notes TEXT NOT NULL DEFAULT '';
