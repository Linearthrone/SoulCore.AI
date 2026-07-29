-- Migration 003: allow episodic_memories.source = 'model'
-- Owner: DBD-01 | TASK-157 | ISSUE-20260726-002
--
-- SQLite cannot ALTER/DROP a CHECK constraint in place. Rebuild the table with
-- an expanded CHECK. Preserve all existing rows; do NOT rewrite 'chat' → 'model'.
-- Child FKs (episodic_embeddings_meta, episodic_embedding_vectors) keep their
-- REFERENCES by table name — foreign_keys must be OFF during DROP/RENAME.

PRAGMA foreign_keys = OFF;

CREATE TABLE episodic_memories__003 (
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

INSERT INTO episodic_memories__003 (
    id, content, occurred_at, created_at, updated_at,
    source, source_ref, labels_json, importance, is_quarantined, embedding_id
)
SELECT
    id, content, occurred_at, created_at, updated_at,
    source, source_ref, labels_json, importance, is_quarantined, embedding_id
FROM episodic_memories;

DROP TABLE episodic_memories;

ALTER TABLE episodic_memories__003 RENAME TO episodic_memories;

CREATE INDEX IF NOT EXISTS idx_episodic_occurred_at
    ON episodic_memories (occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_episodic_source
    ON episodic_memories (source);

CREATE INDEX IF NOT EXISTS idx_episodic_quarantined
    ON episodic_memories (is_quarantined)
    WHERE is_quarantined = 1;

CREATE INDEX IF NOT EXISTS idx_episodic_created_at
    ON episodic_memories (created_at DESC);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('003', 'episodic_source_model');

PRAGMA foreign_keys = ON;
