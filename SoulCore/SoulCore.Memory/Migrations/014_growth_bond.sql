-- Migration 014: PROP-20.37 growth bond — soak clock, trait weights, ledger, pending.
-- Bond counters are computed from Host facts (chat / tasks / corrections / journals);
-- this schema persists soak start, applied weights, ledger rows, and pending band-cross.

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS growth_soak_state (
    id              INTEGER     PRIMARY KEY
                    CHECK (id = 1),
    started_at      TEXT        NOT NULL,
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE IF NOT EXISTS growth_trait_weights (
    trait_id        TEXT        NOT NULL PRIMARY KEY
                    CHECK (trait_id IN ('responsiveness', 'initiative', 'openness')),
    weight          REAL        NOT NULL
                    CHECK (weight >= 0.0 AND weight <= 1.0),
    band            TEXT        NOT NULL
                    CHECK (band IN ('Low', 'Mid', 'High')),
    prompt_line     TEXT        NOT NULL,
    ledger_reason   TEXT        NOT NULL,
    updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    CONSTRAINT growth_trait_prompt_nonempty CHECK (length(trim(prompt_line)) > 0)
);

CREATE TABLE IF NOT EXISTS growth_delta_ledger (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    trait_id        TEXT        NOT NULL
                    CHECK (trait_id IN ('responsiveness', 'initiative', 'openness')),
    from_weight     REAL        NOT NULL,
    to_weight       REAL        NOT NULL,
    from_band       TEXT        NOT NULL,
    to_band         TEXT        NOT NULL,
    reason          TEXT        NOT NULL,
    source          TEXT        NOT NULL DEFAULT 'auto'
                    CHECK (source IN ('auto', 'operator_confirm', 'seed')),
    recorded_at     TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_growth_ledger_recorded
    ON growth_delta_ledger (recorded_at DESC);

CREATE TABLE IF NOT EXISTS growth_pending_proposals (
    id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    trait_id        TEXT        NOT NULL
                    CHECK (trait_id IN ('responsiveness', 'initiative', 'openness')),
    from_weight     REAL        NOT NULL,
    to_weight       REAL        NOT NULL,
    from_band       TEXT        NOT NULL,
    to_band         TEXT        NOT NULL,
    reason          TEXT        NOT NULL,
    status          TEXT        NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'confirmed', 'dismissed')),
    created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    resolved_at     TEXT        NULL
);

CREATE INDEX IF NOT EXISTS idx_growth_pending_status
    ON growth_pending_proposals (status, created_at DESC);

-- Bootstrap snapshot only. SoT = GrowthTraits.DefaultSeedTraits (C#).
-- Parity gate: Prop2037GrowthBondTests.Migration014_SeedInsert_MatchesDefaultSeedTraits.
INSERT OR IGNORE INTO growth_trait_weights (trait_id, weight, band, prompt_line, ledger_reason)
VALUES
    ('responsiveness', 0.52, 'Mid',
     'Answers when reached; keeps the thread warm without smothering.',
     'operator-provisional-until-seed'),
    ('initiative', 0.52, 'Mid',
     'Reaches first when something matters; does not wait to be summoned for every step.',
     'operator-provisional-until-seed'),
    ('openness', 0.52, 'Mid',
     'Shares what she notices; invites Kayleigh into the honest middle of a thought.',
     'operator-provisional-until-seed');

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('014', 'growth_bond');
