-- Migration 001: initial SoulCore.Memory schema
-- Apply (preferred):
--   powershell -File Scripts/create-empty-db.ps1
-- Or from SoulCore.Memory/:
--   sqlite3 data/soulcore_memory.empty.db < Schema/001_schema.sql
--   sqlite3 data/soulcore_memory.empty.db "INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('001', 'initial');"
--
-- Note: do not use relative .read from this file — sqlite3 resolves .read against CWD, not this path.
-- Canonical DDL lives in Schema/001_schema.sql (source of truth for this version).

INSERT OR IGNORE INTO schema_migrations (version, name) VALUES ('001', 'initial');
