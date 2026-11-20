-- Rollback 015: drop PROP-18.6 activities log.
-- Safe on empty/dev DBs after backup. Never against production without PM+OPS.
-- Does not touch victoria_journal_* or victoria_research_* (papers stay separate).

PRAGMA foreign_keys = OFF;

DROP INDEX IF EXISTS idx_activities_subject_occurred;
DROP INDEX IF EXISTS idx_activities_occurred;
DROP TABLE IF EXISTS victoria_activities_log;

DELETE FROM schema_migrations WHERE version = '015';

PRAGMA foreign_keys = ON;
