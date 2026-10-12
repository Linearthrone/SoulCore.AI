-- Schema companion for migration 009 (PROP-17.2 SOP store).
-- Documentation only — not embedded. Live DBs apply Migrations/009_victoria_sops.sql
-- (includes index, seed SOPs, schema_migrations row).
CREATE TABLE IF NOT EXISTS victoria_sops (
    id           INTEGER     PRIMARY KEY AUTOINCREMENT,
    title        TEXT        NOT NULL,
    when_text    TEXT        NOT NULL DEFAULT '',
    steps        TEXT        NOT NULL DEFAULT '',
    donts        TEXT        NOT NULL DEFAULT '',
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
