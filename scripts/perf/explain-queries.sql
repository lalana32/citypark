-- CP-029: query plans for docs/performance.md. Run after seed-reservations.sql; changes nothing.
--
--   docker compose exec -T postgres psql -U citypark -d citypark -v ON_ERROR_STOP=1 < scripts/perf/explain-queries.sql

\set user_id '00000000-0000-0000-0000-00000000c029'
\set lot_id  '00000000-0000-0000-0000-00000000a029'

-- 2026-12-10 is day 100 of the data: the 10:00-12:00 interval overlaps the 09:00-11:00 slot of every space.
\set start '2026-12-10 10:00:00+00'
\set end   '2026-12-10 12:00:00+00'

SELECT id AS space_id FROM parking_spaces WHERE lot_id = :'lot_id' AND label = 'P-001' \gset

\echo '=== 1. Availability: the SQL AvailabilityService sends (parameters inlined) ==='
EXPLAIN (ANALYZE, BUFFERS)
SELECT p0.id AS "Id", p0.label AS "Label", NOT EXISTS (
    SELECT 1
    FROM reservations AS r
    WHERE r.status = 1 AND r.start_time < :'end' AND :'start' < r.end_time AND r.space_id = p0.id) AS "IsFree"
FROM parking_lots AS p
INNER JOIN parking_spaces AS p0 ON p.id = p0.lot_id
WHERE p.id = :'lot_id' AND p0.is_active
ORDER BY p0.label;

\echo '=== 2. Availability with the range operator, the form the constraint index is built on ==='
EXPLAIN (ANALYZE, BUFFERS)
SELECT p0.id AS "Id", p0.label AS "Label", NOT EXISTS (
    SELECT 1
    FROM reservations AS r
    WHERE r.status = 1
      AND tstzrange(r.start_time, r.end_time, '[)') && tstzrange(:'start', :'end', '[)')
      AND r.space_id = p0.id) AS "IsFree"
FROM parking_lots AS p
INNER JOIN parking_spaces AS p0 ON p.id = p0.lot_id
WHERE p.id = :'lot_id' AND p0.is_active
ORDER BY p0.label;

\echo '=== 3. Insert of a free slot (11:00-12:00 is between two slots); the constraint checks it ==='
BEGIN;
EXPLAIN (ANALYZE, BUFFERS)
INSERT INTO reservations (id, currency, end_time, plate, price, space_id, start_time, status, user_id)
VALUES (gen_random_uuid(), 'BAM', '2026-12-10 12:00:00+00', 'A12K345', 2.00, :'space_id',
        '2026-12-10 11:00:00+00', 1, :'user_id')
RETURNING xmin;
ROLLBACK;

\echo '=== 4. The probe the constraint runs for that insert, as a query ==='
EXPLAIN (ANALYZE, BUFFERS)
SELECT 1
FROM reservations
WHERE status = 1
  AND space_id = :'space_id'
  AND tstzrange(start_time, end_time, '[)') && tstzrange('2026-12-10 11:00:00+00', '2026-12-10 12:00:00+00', '[)');

\echo '=== 5. Indexes on reservations ==='
SELECT indexrelname AS index, pg_size_pretty(pg_relation_size(indexrelid)) AS size, idx_scan
FROM pg_stat_user_indexes
WHERE relname = 'reservations'
ORDER BY indexrelname;
