-- Migration 011: Research / hobby subject journals + APA-style papers.
-- Separate from personal journal books (feeling / animation / environment / life_hacks).

CREATE TABLE IF NOT EXISTS victoria_research_subjects (
    id           INTEGER     PRIMARY KEY AUTOINCREMENT,
    slug         TEXT        NOT NULL UNIQUE,
    title        TEXT        NOT NULL,
    summary      TEXT        NOT NULL DEFAULT '',
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT research_subject_title_nonempty CHECK (length(trim(title)) > 0),
    CONSTRAINT research_subject_slug_nonempty CHECK (length(trim(slug)) > 0)
);

CREATE TABLE IF NOT EXISTS victoria_research_papers (
    id               INTEGER     PRIMARY KEY AUTOINCREMENT,
    subject_id       INTEGER     NOT NULL
                     REFERENCES victoria_research_subjects (id) ON DELETE CASCADE,
    kind             TEXT        NOT NULL DEFAULT 'review'
                     CHECK (kind IN ('review', 'experiment', 'theory')),
    title            TEXT        NOT NULL,
    abstract         TEXT        NOT NULL DEFAULT '',
    body_apa         TEXT        NOT NULL DEFAULT '',
    bibliography_apa TEXT        NOT NULL DEFAULT '',
    status           TEXT        NOT NULL DEFAULT 'draft'
                     CHECK (status IN ('draft', 'published')),
    created_at       TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at       TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT research_paper_title_nonempty CHECK (length(trim(title)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_research_subjects_updated
    ON victoria_research_subjects (updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_research_papers_subject_updated
    ON victoria_research_papers (subject_id, updated_at DESC);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('011', 'victoria_research');
