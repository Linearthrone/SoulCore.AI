.headers on
.mode column
SELECT '=== COUNT(*) ===' AS section;
SELECT COUNT(*) AS anchor_count FROM charter_anchors;
SELECT '' AS sep;
SELECT '=== kind, title, is_locked, source, priority ===' AS section;
SELECT kind, title, is_locked, source, priority FROM charter_anchors ORDER BY priority;
SELECT '' AS sep;
SELECT '=== DISTINCT is_locked ===' AS section;
SELECT DISTINCT is_locked FROM charter_anchors;
SELECT '' AS sep;
SELECT '=== DISTINCT source ===' AS section;
SELECT DISTINCT source FROM charter_anchors;
SELECT '' AS sep;
SELECT '=== COUNT by kind ===' AS section;
SELECT kind, COUNT(*) AS n FROM charter_anchors GROUP BY kind;
