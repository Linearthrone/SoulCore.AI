-- Migration 005: expand episodic_memories.source CHECK to include 'model'
-- ISSUE-20260726-002 / TASK-157 (BED-01 hygiene).
--
-- Numbering note: ISSUE-002 recommended "003", but TASK-140 reserves
-- 003_victoria_tasks and TASK-141 reserves 004_victoria_workflows — this
-- CHECK expansion ships as 005 to avoid colliding with those tickets.
--
-- SQLite cannot ALTER a CHECK constraint in place; rebuild the table.
-- Child FKs (episodic_embedding_vectors, episodic_embeddings_meta) are
-- preserved by disabling foreign_keys for the swap (same row ids).

PRAGMA foreign_keys = OFF;

BEGIN TRANSACTION;

CREATE TABLE episodic_memories__mig005 (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    content         TEXT        NOT NULL,
    occurred_at     TEXT        NOT NULL,
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    source          TEXT        NOT NULL
                    CHECK (source IN (
                        'self', 'chat', 'imported', 'observation', 'correction', 'system', 'model'
                    )),
    source_ref      TEXT        NULL,
    labels_json     TEXT        NOT NULL DEFAULT '[]',
    importance      REAL        NOT NULL DEFAULT 0.5
                    CHECK (importance >= 0.0 AND importance <= 1.0),
    is_quarantined  INTEGER     NOT NULL DEFAULT 0
                    CHECK (is_quarantined IN (0, 1)),
    embedding_id    INTEGER     NULL,
    CONSTRAINT episodic_content_nonempty CHECK (length(trim(content)) > 0)
);

INSERT INTO episodic_memories__mig005 (
    id, content, occurred_at, created_at, updated_at,
    source, source_ref, labels_json, importance, is_quarantined, embedding_id
)
SELECT
    id, content, occurred_at, created_at, updated_at,
    source, source_ref, labels_json, importance, is_quarantined, embedding_id
FROM episodic_memories;

DROP TABLE episodic_memories;

ALTER TABLE episodic_memories__mig005 RENAME TO episodic_memories;

CREATE INDEX IF NOT EXISTS idx_episodic_occurred_at
    ON episodic_memories (occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_episodic_source
    ON episodic_memories (source);

CREATE INDEX IF NOT EXISTS idx_episodic_quarantined
    ON episodic_memories (is_quarantined)
    WHERE is_quarantined = 1;

CREATE INDEX IF NOT EXISTS idx_episodic_created_at
    ON episodic_memories (created_at DESC);

-- AUTOINCREMENT: DROP clears sqlite_sequence for this table; SQLite then uses
-- MAX(id)+1 on the next insert, so ids are not reused. No sqlite_sequence write
-- here (table may be absent on empty DBs).

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('005', 'episodic_source_model');

COMMIT;

PRAGMA foreign_keys = ON;
