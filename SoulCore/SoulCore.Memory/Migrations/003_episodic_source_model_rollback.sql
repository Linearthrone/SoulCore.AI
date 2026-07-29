-- Rollback 003: remove 'model' from episodic_memories.source CHECK
-- Owner: DBD-01 | TASK-157
--
-- Manual / OPS only — not applied by SqliteMemoryStore.
-- Coerces any source='model' rows to 'chat' (pre-003 store_memory workaround)
-- before rebuilding the CHECK without 'model'. Does not touch other sources.

PRAGMA foreign_keys = OFF;

UPDATE episodic_memories SET source = 'chat' WHERE source = 'model';

CREATE TABLE episodic_memories__003r (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    content         TEXT        NOT NULL,
    occurred_at     TEXT        NOT NULL,
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    source          TEXT        NOT NULL
                    CHECK (source IN (
                        'self', 'chat', 'imported', 'observation', 'correction', 'system'
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

INSERT INTO episodic_memories__003r (
    id, content, occurred_at, created_at, updated_at,
    source, source_ref, labels_json, importance, is_quarantined, embedding_id
)
SELECT
    id, content, occurred_at, created_at, updated_at,
    source, source_ref, labels_json, importance, is_quarantined, embedding_id
FROM episodic_memories;

DROP TABLE episodic_memories;

ALTER TABLE episodic_memories__003r RENAME TO episodic_memories;

CREATE INDEX IF NOT EXISTS idx_episodic_occurred_at
    ON episodic_memories (occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_episodic_source
    ON episodic_memories (source);

CREATE INDEX IF NOT EXISTS idx_episodic_quarantined
    ON episodic_memories (is_quarantined)
    WHERE is_quarantined = 1;

CREATE INDEX IF NOT EXISTS idx_episodic_created_at
    ON episodic_memories (created_at DESC);

DELETE FROM schema_migrations WHERE version = '003';

PRAGMA foreign_keys = ON;
