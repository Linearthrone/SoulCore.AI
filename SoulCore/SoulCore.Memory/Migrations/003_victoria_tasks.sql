-- Migration 003: Victoria's own task store (model-callable via task_* tools).
-- Separate from PM-authored tickets under docs/agents/tasks/ — those are
-- human/agent-orchestration artifacts; this table is Victoria's runtime work
-- items that she creates and updates through the agent-loop tools.

CREATE TABLE IF NOT EXISTS victoria_tasks (
    id           INTEGER     PRIMARY KEY AUTOINCREMENT,
    title        TEXT        NOT NULL,
    description  TEXT        NOT NULL DEFAULT '',
    status       TEXT        NOT NULL DEFAULT 'todo',
    priority     TEXT        NOT NULL DEFAULT 'medium',
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_victoria_tasks_status
    ON victoria_tasks (status);

CREATE INDEX IF NOT EXISTS idx_victoria_tasks_updated
    ON victoria_tasks (updated_at DESC);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('003', 'victoria_tasks');
