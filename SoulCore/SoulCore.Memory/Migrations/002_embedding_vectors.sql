-- Migration 002: in-process float32 embedding vectors (sqlite-vec vec0 deferred)
-- Stores little-endian float32 BLOBs keyed by episodic_id for cosine top-K recall.

CREATE TABLE IF NOT EXISTS episodic_embedding_vectors (
    episodic_id     INTEGER     PRIMARY KEY
                    REFERENCES episodic_memories (id) ON DELETE CASCADE,
    model           TEXT        NOT NULL DEFAULT 'nomic-embed-text',
    dims            INTEGER     NOT NULL,
    vector          BLOB        NOT NULL,
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_embedding_vectors_created
    ON episodic_embedding_vectors (created_at DESC);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('002', 'embedding_vectors');
