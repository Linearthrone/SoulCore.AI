-- Migration 016: PROP-22.1 operator work items + week agenda (Presence glass shell).
-- Separate from victoria_tasks (Victoria's own ledger). Contract: docs/agents/unexecuted_proposals/presence-glass-shell.md §3.1 / §3.2.

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS operator_work_items (
    id                      INTEGER     PRIMARY KEY AUTOINCREMENT,
    kind                    TEXT        NOT NULL CHECK (kind IN ('task', 'project')),
    title                   TEXT        NOT NULL,
    description             TEXT,
    details                 TEXT,
    deadline_utc            TEXT        NULL,
    priority                INTEGER     NOT NULL DEFAULT 3 CHECK (priority BETWEEN 1 AND 5),
    no_checkin              INTEGER     NOT NULL DEFAULT 0 CHECK (no_checkin IN (0, 1)),
    phase_checkpoints_json  TEXT,
    deliverables_json       TEXT,
    needs_extra_permissions INTEGER     NOT NULL DEFAULT 0 CHECK (needs_extra_permissions IN (0, 1)),
    outside_collaboration   INTEGER     NOT NULL DEFAULT 0 CHECK (outside_collaboration IN (0, 1)),
    status                  TEXT        NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'done', 'cancelled')),
    created_at              TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at              TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT operator_work_items_title_nonempty CHECK (length(trim(title)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_operator_work_items_status_updated
    ON operator_work_items (status, updated_at DESC, id DESC);

CREATE TABLE IF NOT EXISTS operator_agenda_items (
    id          INTEGER     PRIMARY KEY AUTOINCREMENT,
    title       TEXT        NOT NULL,
    weekday     INTEGER     NOT NULL CHECK (weekday BETWEEN 0 AND 6),
    time_local  TEXT        NOT NULL DEFAULT '09:00',
    notes       TEXT,
    created_at  TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT operator_agenda_items_title_nonempty CHECK (length(trim(title)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_operator_agenda_items_weekday_time
    ON operator_agenda_items (weekday, time_local, id);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('016', 'operator_work_and_agenda');
