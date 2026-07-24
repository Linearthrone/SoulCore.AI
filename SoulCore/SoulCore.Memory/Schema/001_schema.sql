-- SoulCore.Memory — V1 canonical schema (SQLite + sqlite-vec)
-- Owner: DBD-01 | Phase 0 | TASK-20260722-007
-- Storage truth: SQLite structured + sqlite-vec. No PgVector day-one.
-- Secrets MUST NOT be stored in this database (env / user-secrets only).

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

-- ---------------------------------------------------------------------------
-- schema_migrations — applied migration ledger (raw SQL approach)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS schema_migrations (
    version     TEXT        NOT NULL PRIMARY KEY,  -- e.g. '001'
    name        TEXT        NOT NULL,              -- e.g. 'initial'
    applied_at  TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

-- ---------------------------------------------------------------------------
-- episodic_memories — first-person episodic writes (model-authored)
-- source: self | chat | imported | observation | correction | system
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS episodic_memories (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    content         TEXT        NOT NULL,           -- first-person text
    occurred_at     TEXT        NOT NULL,           -- ISO-8601 UTC when event happened
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    source          TEXT        NOT NULL
                    CHECK (source IN (
                        'self', 'chat', 'imported', 'observation', 'correction', 'system'
                    )),
    source_ref      TEXT        NULL,               -- optional quarry/session id
    labels_json     TEXT        NOT NULL DEFAULT '[]', -- JSON string array
    importance      REAL        NOT NULL DEFAULT 0.5
                    CHECK (importance >= 0.0 AND importance <= 1.0),
    is_quarantined  INTEGER     NOT NULL DEFAULT 0
                    CHECK (is_quarantined IN (0, 1)), -- imported quarantine flag
    embedding_id    INTEGER     NULL,               -- FK soft-link to vec row (when vec live)
    CONSTRAINT episodic_content_nonempty CHECK (length(trim(content)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_episodic_occurred_at
    ON episodic_memories (occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_episodic_source
    ON episodic_memories (source);

CREATE INDEX IF NOT EXISTS idx_episodic_quarantined
    ON episodic_memories (is_quarantined)
    WHERE is_quarantined = 1;

CREATE INDEX IF NOT EXISTS idx_episodic_created_at
    ON episodic_memories (created_at DESC);

-- ---------------------------------------------------------------------------
-- emotion_state — singleton (or versioned) emotion vector; survives restart
-- components_json: named floats e.g. {"valence":0.2,"arousal":0.4,...}
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS emotion_state (
    id              INTEGER     PRIMARY KEY
                    CHECK (id = 1),                 -- singleton row for live state
    valence         REAL        NOT NULL DEFAULT 0.0
                    CHECK (valence >= -1.0 AND valence <= 1.0),
    arousal         REAL        NOT NULL DEFAULT 0.0
                    CHECK (arousal >= 0.0 AND arousal <= 1.0),
    dominance       REAL        NOT NULL DEFAULT 0.5
                    CHECK (dominance >= 0.0 AND dominance <= 1.0),
    components_json TEXT        NOT NULL DEFAULT '{}', -- extensible named components
    note            TEXT        NULL,               -- optional correction note
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    revision        INTEGER     NOT NULL DEFAULT 1
);

-- Seed live emotion row (idempotent)
INSERT OR IGNORE INTO emotion_state (id) VALUES (1);

-- Optional history for tuning / correction UX (append-only)
CREATE TABLE IF NOT EXISTS emotion_state_history (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    valence         REAL        NOT NULL,
    arousal         REAL        NOT NULL,
    dominance       REAL        NOT NULL,
    components_json TEXT        NOT NULL,
    note            TEXT        NULL,
    reason          TEXT        NOT NULL DEFAULT 'update'
                    CHECK (reason IN ('update', 'correction', 'restart_snapshot', 'system')),
    recorded_at     TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_emotion_history_recorded_at
    ON emotion_state_history (recorded_at DESC);

-- ---------------------------------------------------------------------------
-- charter_anchors — identity / safety anchors OUTSIDE episodic memory
-- kind: identity | safety | value | boundary | ritual
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS charter_anchors (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    kind            TEXT        NOT NULL
                    CHECK (kind IN ('identity', 'safety', 'value', 'boundary', 'ritual')),
    title           TEXT        NOT NULL,
    body            TEXT        NOT NULL,
    priority        INTEGER     NOT NULL DEFAULT 100, -- lower = higher priority
    is_locked       INTEGER     NOT NULL DEFAULT 0
                    CHECK (is_locked IN (0, 1)),
    source          TEXT        NOT NULL DEFAULT 'seed'
                    CHECK (source IN ('seed', 'imported', 'calibration', 'system')),
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT charter_title_nonempty CHECK (length(trim(title)) > 0),
    CONSTRAINT charter_body_nonempty CHECK (length(trim(body)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_charter_kind_priority
    ON charter_anchors (kind, priority ASC);

CREATE INDEX IF NOT EXISTS idx_charter_locked
    ON charter_anchors (is_locked)
    WHERE is_locked = 1;

-- ---------------------------------------------------------------------------
-- config_kv — non-secret knobs only (endpoints, feature flags, model names)
-- NEVER store API keys, tokens, passwords here.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS config_kv (
    key             TEXT        NOT NULL PRIMARY KEY,
    value           TEXT        NOT NULL,
    value_type      TEXT        NOT NULL DEFAULT 'string'
                    CHECK (value_type IN ('string', 'int', 'float', 'bool', 'json')),
    category        TEXT        NOT NULL DEFAULT 'system'
                    CHECK (category IN (
                        'system', 'inference', 'memory', 'emotion',
                        'voice', 'unreal', 'safety', 'ui'
                    )),
    description     TEXT        NULL,
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT config_key_nonempty CHECK (length(trim(key)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_config_category
    ON config_kv (category);

-- ---------------------------------------------------------------------------
-- episodic_embeddings_meta — metadata for vector rows (sqlite-vec virtual tbl)
-- The actual float vectors live in vec0 virtual table when extension is loaded.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS episodic_embeddings_meta (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    episodic_id     INTEGER     NOT NULL
                    REFERENCES episodic_memories (id) ON DELETE CASCADE,
    model           TEXT        NOT NULL DEFAULT 'nomic-embed-text',
    dims            INTEGER     NOT NULL DEFAULT 768,
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    UNIQUE (episodic_id)
);

CREATE INDEX IF NOT EXISTS idx_embeddings_meta_episodic
    ON episodic_embeddings_meta (episodic_id);

-- ---------------------------------------------------------------------------
-- sqlite-vec PLAN (stub — gated on extension availability)
-- ---------------------------------------------------------------------------
-- When sqlite-vec is installed and loaded (e.g. `.load sqlite_vec` / native
-- extension), create the virtual table below. Until then, BED must not assume
-- vec_distance / KNN queries work; fall back to recency/importance ranking.
--
-- Expected V1 dims: 768 (nomic-embed-text). Adjust if model changes.
--
-- CREATE VIRTUAL TABLE IF NOT EXISTS episodic_embeddings USING vec0(
--     embedding float[768]
-- );
--
-- Insert pairing:
--   INSERT INTO episodic_embeddings_meta (episodic_id, model, dims) VALUES (...);
--   INSERT INTO episodic_embeddings (rowid, embedding) VALUES (last_meta_id, <blob>);
--
-- Search sketch:
--   SELECT m.episodic_id, distance
--   FROM episodic_embeddings e
--   JOIN episodic_embeddings_meta m ON m.id = e.rowid
--   WHERE e.embedding MATCH ?
--   ORDER BY distance
--   LIMIT 10;
--
-- PgVector: NOT day-one. Optional async dual-write later behind feature flag.
-- ---------------------------------------------------------------------------
