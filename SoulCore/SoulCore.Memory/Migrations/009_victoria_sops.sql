-- Migration 009: PROP-17.2 SOP store (charter-shaped).
-- Authority order (code comment / handbook): tool code → charter → operator settings → her notes.
-- An SOP cannot flip a host gate, mint confirmed, or call guest_shell.

CREATE TABLE IF NOT EXISTS victoria_sops (
    id           INTEGER     PRIMARY KEY AUTOINCREMENT,
    title        TEXT        NOT NULL,
    when_text    TEXT        NOT NULL DEFAULT '',
    steps        TEXT        NOT NULL DEFAULT '',
    donts        TEXT        NOT NULL DEFAULT '',
    created_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_victoria_sops_updated
    ON victoria_sops (updated_at DESC);

INSERT OR IGNORE INTO victoria_sops (id, title, when_text, steps, donts) VALUES
(1,
 'Browser hygiene — one tab per URL',
 'Opening or navigating to a page in the guest or Playwright browser.',
 '1) Call browser_tabs (or list open pages) before opening a URL.
2) If that URL is already open, focus the existing tab — do not open another.
3) Keep one browser process. Never launch a second Firefox/Chrome/Chromium.
4) Prefer browser_navigate / browser_tabs over desktop_open_app for websites.',
 'Do not open a second browser. Do not install a browser package. Do not mint confirmed or change Settings gates. An SOP cannot override tool code.'),
(2,
 'Site down means park',
 'A lesson or research page fails health, returns connection errors, or the same URL error repeats.',
 '1) Count identical surface failures (same URL / same unreachable host). A tool name in the transcript is not progress.
2) After three identical failures, park the task with a one-line reason and wait the cooldown.
3) After cooldown, probe once. Still down → park again with the longer cooldown.
4) Do other released work only when its surface is up; otherwise wait or research.',
 'Do not refresh-loop. Do not reconnect forever. Do not auto-start the VM to keep working. Do not flip host gates or call guest_shell for installs.');

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('009', 'victoria_sops');
