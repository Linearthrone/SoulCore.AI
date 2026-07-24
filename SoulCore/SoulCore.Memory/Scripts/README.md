# SoulCore.Memory — create empty DB from schema (evidence / local)

Creates `data/soulcore_memory.empty.db` from `Schema/001_schema.sql`.
Does NOT touch LLMOD Data/ databases.

Usage (from `SoulCore.Memory/` or with full path):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts\create-empty-db.ps1
# or with explicit sqlite3:
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts\create-empty-db.ps1 -Sqlite3Path "C:\path\to\sqlite3.exe"
```
