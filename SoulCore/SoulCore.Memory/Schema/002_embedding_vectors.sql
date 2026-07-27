-- Schema fragment 002: episodic_embedding_vectors (in-process cosine; sqlite-vec deferred)
-- Applied via Migrations/002_embedding_vectors.sql on upgrade / first open after 001.

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
