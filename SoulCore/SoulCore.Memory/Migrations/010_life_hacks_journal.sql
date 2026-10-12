-- Migration 010: PROP-17.2 fourth journal book — life_hacks.
-- SQLite cannot ALTER a CHECK constraint; recreate the books table.

PRAGMA foreign_keys = OFF;

CREATE TABLE IF NOT EXISTS victoria_journal_books_new (
    id           TEXT        NOT NULL PRIMARY KEY
                 CHECK (id IN ('feeling', 'animation', 'environment', 'life_hacks')),
    title        TEXT        NOT NULL,
    purpose      TEXT        NOT NULL,
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

INSERT INTO victoria_journal_books_new (id, title, purpose, created_at)
SELECT id, title, purpose, created_at FROM victoria_journal_books;

DROP TABLE victoria_journal_books;
ALTER TABLE victoria_journal_books_new RENAME TO victoria_journal_books;

INSERT OR IGNORE INTO victoria_journal_books (id, title, purpose) VALUES
    ('life_hacks',
     'Life-Hacks Journal',
     'Practical tricks and working methods I discover — separate from feeling, animation, or environment wants.');

PRAGMA foreign_keys = ON;

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('010', 'life_hacks_journal');
