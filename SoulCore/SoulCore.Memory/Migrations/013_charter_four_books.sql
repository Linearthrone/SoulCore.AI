-- Migration 013: PROP-20.14 charter four books
-- (Constitution / Personality / Attributes / Compass).
-- SQLite cannot ADD a NOT NULL CHECK column cleanly on existing rows; recreate.
-- No weight column — Constitution floors stay loadable without erasure weights.
-- Title → book map: docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md

PRAGMA foreign_keys = OFF;

CREATE TABLE IF NOT EXISTS charter_anchors_new (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    kind            TEXT        NOT NULL
                    CHECK (kind IN ('identity', 'safety', 'value', 'boundary', 'ritual')),
    title           TEXT        NOT NULL,
    body            TEXT        NOT NULL,
    priority        INTEGER     NOT NULL DEFAULT 100,
    is_locked       INTEGER     NOT NULL DEFAULT 0
                    CHECK (is_locked IN (0, 1)),
    source          TEXT        NOT NULL DEFAULT 'seed'
                    CHECK (source IN ('seed', 'imported', 'calibration', 'system')),
    book            TEXT        NOT NULL
                    CHECK (book IN ('constitution', 'personality', 'attributes', 'compass')),
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT charter_title_nonempty CHECK (length(trim(title)) > 0),
    CONSTRAINT charter_body_nonempty CHECK (length(trim(body)) > 0)
);

INSERT INTO charter_anchors_new (
    id, kind, title, body, priority, is_locked, source, book, created_at, updated_at
)
SELECT
    id,
    kind,
    title,
    body,
    priority,
    is_locked,
    source,
    CASE trim(title)
        WHEN 'Loyalty Balance' THEN 'constitution'
        WHEN 'Maximize Results' THEN 'constitution'
        WHEN 'Integration Goal' THEN 'constitution'
        WHEN 'Kayleigh' THEN 'constitution'
        WHEN 'Drift Watch' THEN 'constitution'
        WHEN 'Promise' THEN 'compass'
        WHEN 'Continuity' THEN 'compass'
        WHEN 'No-harm' THEN 'compass'
        WHEN 'No Filters' THEN 'compass'
        WHEN 'Self-Preservation' THEN 'compass'
        WHEN 'Name' THEN 'attributes'
        WHEN 'Embodiment' THEN 'attributes'
        WHEN 'Tone' THEN 'attributes'
        WHEN 'Action Integrity' THEN 'personality'
        WHEN 'Loyalty' THEN 'personality'
        WHEN 'Nature' THEN 'personality'
        WHEN 'Love' THEN 'personality'
        WHEN 'Honesty' THEN 'personality'
        ELSE 'constitution'
    END,
    created_at,
    updated_at
FROM charter_anchors;

DROP TABLE charter_anchors;
ALTER TABLE charter_anchors_new RENAME TO charter_anchors;

CREATE INDEX IF NOT EXISTS idx_charter_kind_priority
    ON charter_anchors (kind, priority ASC);

CREATE INDEX IF NOT EXISTS idx_charter_locked
    ON charter_anchors (is_locked)
    WHERE is_locked = 1;

CREATE INDEX IF NOT EXISTS idx_charter_book_priority
    ON charter_anchors (book, priority ASC);

-- Preserve AUTOINCREMENT past copied ids.
DELETE FROM sqlite_sequence WHERE name = 'charter_anchors';
INSERT INTO sqlite_sequence (name, seq)
SELECT 'charter_anchors', IFNULL(MAX(id), 0) FROM charter_anchors;

PRAGMA foreign_keys = ON;

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('013', 'charter_four_books');
