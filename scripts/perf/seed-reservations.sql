-- CP-029: loads 50,000 reservations for the query plans in docs/performance.md.
-- Local database only, after the migrations. Safe to run again: it removes its own data first.
--
--   docker compose exec -T postgres psql -U citypark -d citypark -v ON_ERROR_STOP=1 < scripts/perf/seed-reservations.sql

-- Fixed ids, so the EXPLAIN queries can refer to the lot and the cleanup finds only this data.
\set user_id '00000000-0000-0000-0000-00000000c029'
\set lot_id  '00000000-0000-0000-0000-00000000a029'

BEGIN;

DELETE FROM reservations WHERE user_id = :'user_id';
DELETE FROM parking_spaces WHERE lot_id = :'lot_id';
DELETE FROM parking_lots WHERE id = :'lot_id';
DELETE FROM asp_net_users WHERE id = :'user_id';

-- No password hash: the account exists only to satisfy the foreign key and cannot sign in.
INSERT INTO asp_net_users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed,
                           phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
VALUES (:'user_id', 'perf@citypark.local', 'PERF@CITYPARK.LOCAL', 'perf@citypark.local', 'PERF@CITYPARK.LOCAL',
        false, false, false, false, 0);

INSERT INTO parking_lots (id, name, address, opens_at, closes_at, currency, hourly_price)
VALUES (:'lot_id', 'Perf', 'Perf 1', '06:00', '23:00', 'BAM', 2.00);

INSERT INTO parking_spaces (id, label, is_active, lot_id)
SELECT gen_random_uuid(), 'P-' || lpad(n::text, 3, '0'), true, :'lot_id'
FROM generate_series(1, 100) AS n;

-- 100 spaces x 500 reservations. Four 2-hour slots a day (05, 09, 13, 17 UTC) for 125 days from
-- 2026-09-01, so the data covers past and future and Active rows never overlap. Every tenth is
-- Cancelled, which the partial constraint ignores.
INSERT INTO reservations (id, space_id, user_id, start_time, end_time, status, plate, price, currency)
SELECT gen_random_uuid(),
       s.id,
       :'user_id',
       slot.start_time,
       slot.start_time + interval '2 hours',
       CASE WHEN g.i % 10 = 0 THEN 2 ELSE 1 END,
       'A' || lpad((row_number() OVER () % 100)::text, 2, '0') || 'K' || lpad((g.i % 1000)::text, 3, '0'),
       4.00,
       'BAM'
FROM parking_spaces s
CROSS JOIN generate_series(0, 499) AS g(i)
CROSS JOIN LATERAL (
    SELECT timestamptz '2026-09-01 05:00+00' + (g.i / 4) * interval '1 day' + (g.i % 4) * interval '4 hours' AS start_time
) AS slot
WHERE s.lot_id = :'lot_id';

COMMIT;

-- Fresh statistics, otherwise the planner estimates row counts from an almost empty table.
ANALYZE asp_net_users, parking_lots, parking_spaces, reservations;

SELECT count(*) AS reservations,
       count(*) FILTER (WHERE status = 1) AS active,
       min(start_time) AS first_start,
       max(end_time) AS last_end
FROM reservations
WHERE user_id = :'user_id';
