-- Migration 004: Victoria's workflow store (model-callable via workflow_* tools).
-- A workflow is a named ordered list of steps (description + optional tool name).
-- current_step is the 0-based index of the next step to execute.

CREATE TABLE IF NOT EXISTS victoria_workflows (
    id            INTEGER     PRIMARY KEY AUTOINCREMENT,
    name          TEXT        NOT NULL,
    steps_json    TEXT        NOT NULL,
    current_step  INTEGER     NOT NULL DEFAULT 0,
    created_at    TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at    TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_victoria_workflows_updated
    ON victoria_workflows (updated_at DESC);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('004', 'victoria_workflows');
