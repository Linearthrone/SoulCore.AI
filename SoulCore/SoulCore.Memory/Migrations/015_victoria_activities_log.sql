-- Migration 015: PROP-18.6 activities log (Q1 inspectable residue).
-- Table-like session log: Entry No., Date/Time, Activity, Subject/Topic/Focus, Desc.
-- Optional soft links to private journal book/entry only — never research papers
-- (papers stay in victoria_research_* from migration 011).

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS victoria_activities_log (
    entry_no            INTEGER     PRIMARY KEY AUTOINCREMENT,
    occurred_at         TEXT        NOT NULL,
    activity            TEXT        NOT NULL,
    subject             TEXT        NOT NULL,
    session_desc        TEXT        NOT NULL,
    journal_book_id     TEXT        NULL
                        REFERENCES victoria_journal_books (id) ON DELETE SET NULL,
    journal_entry_id    INTEGER     NULL
                        REFERENCES victoria_journal_entries (id) ON DELETE SET NULL,
    created_at          TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT activities_activity_nonempty CHECK (length(trim(activity)) > 0),
    CONSTRAINT activities_subject_nonempty CHECK (length(trim(subject)) > 0),
    CONSTRAINT activities_desc_nonempty CHECK (length(trim(session_desc)) > 0)
);

-- Recent-first list (Presence / while-you-were-gone).
CREATE INDEX IF NOT EXISTS idx_activities_occurred
    ON victoria_activities_log (occurred_at DESC, entry_no DESC);

-- Filter by subject/topic/focus, still recent-first.
CREATE INDEX IF NOT EXISTS idx_activities_subject_occurred
    ON victoria_activities_log (subject, occurred_at DESC, entry_no DESC);

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('015', 'victoria_activities_log');
