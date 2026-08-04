-- Schema fragment 006: Victoria journals (feeling / animation / environment).
-- Applied via Migrations/006_victoria_journals.sql on upgrade / first open after 005.

CREATE TABLE IF NOT EXISTS victoria_journal_books (
    id           TEXT        NOT NULL PRIMARY KEY
                 CHECK (id IN ('feeling', 'animation', 'environment')),
    title        TEXT        NOT NULL,
    purpose      TEXT        NOT NULL,
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE IF NOT EXISTS victoria_journal_entries (
    id           INTEGER     PRIMARY KEY AUTOINCREMENT,
    book_id      TEXT        NOT NULL
                 REFERENCES victoria_journal_books (id) ON DELETE CASCADE,
    body         TEXT        NOT NULL,
    mood_json    TEXT        NULL,
    tags_json    TEXT        NOT NULL DEFAULT '[]',
    occurred_at  TEXT        NOT NULL,
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    source       TEXT        NOT NULL DEFAULT 'self'
                 CHECK (source IN (
                     'self', 'chat', 'imported', 'observation', 'correction', 'system', 'model'
                 )),
    CONSTRAINT journal_body_nonempty CHECK (length(trim(body)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_journal_entries_book_occurred
    ON victoria_journal_entries (book_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_journal_entries_created
    ON victoria_journal_entries (created_at DESC);
