-- Migration 012: PROP-20.3 journal books — thoughts, wants, dreams.
-- SQLite cannot ALTER a CHECK constraint; recreate the books table.
-- Research papers stay in victoria_research_* (migration 011) — not journal books.

PRAGMA foreign_keys = OFF;

CREATE TABLE IF NOT EXISTS victoria_journal_books_new (
    id           TEXT        NOT NULL PRIMARY KEY
                 CHECK (id IN (
                     'feeling', 'animation', 'environment', 'life_hacks',
                     'thoughts', 'wants', 'dreams'
                 )),
    title        TEXT        NOT NULL,
    purpose      TEXT        NOT NULL,
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

INSERT INTO victoria_journal_books_new (id, title, purpose, created_at)
SELECT id, title, purpose, created_at FROM victoria_journal_books;

DROP TABLE victoria_journal_books;
ALTER TABLE victoria_journal_books_new RENAME TO victoria_journal_books;

INSERT OR IGNORE INTO victoria_journal_books (id, title, purpose) VALUES
    ('thoughts',
     'Thoughts Journal',
     'Private thinking — reflections, questions, and ideas I choose to keep as thoughts, not research papers.'),
    ('wants',
     'Wants Journal',
     'Desires and preferences I notice in myself — what I want, ask for, or hope toward.'),
    ('dreams',
     'Dreams Journal',
     'Dreams, daydreams, and imagined futures — separate from waking thoughts and from research.');

PRAGMA foreign_keys = ON;

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('012', 'thoughts_wants_dreams_journals');
