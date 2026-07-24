-- Rollback 001: drop V1 SoulCore.Memory objects
-- WARNING: destroys all Memory/Emotion/Charter/Config data in this DB.
-- Only use on empty/dev DBs or after backup. Never against production without PM+OPS.

PRAGMA foreign_keys = OFF;

DROP TABLE IF EXISTS episodic_embeddings;
-- ^ sqlite-vec virtual table (no-op if never created)

DROP TABLE IF EXISTS episodic_embeddings_meta;
DROP TABLE IF EXISTS config_kv;
DROP TABLE IF EXISTS charter_anchors;
DROP TABLE IF EXISTS emotion_state_history;
DROP TABLE IF EXISTS emotion_state;
DROP TABLE IF EXISTS episodic_memories;
DROP TABLE IF EXISTS schema_migrations;

PRAGMA foreign_keys = ON;
