# Performance: availability and overlap query plans

Measured in CP-029 to check whether the availability query and the insert conflict check use the
GiST index behind the exclusion constraint `ex_reservations_space_no_overlap`
([ADR-002](adr/0002-overlap-prevention-with-exclusion-constraint.md)), and whether another index is
needed.

## Conclusion

**No additional index is needed.**

- **Insert conflict check:** uses the constraint's GiST index. The probe reads 4 buffers and takes
  0.26 ms with 50,000 rows, and grows with the depth of the index, not the size of the table.
- **Availability query:** does **not** use any index on `reservations`. It reads the whole table
  once (715 buffers, 20 ms) because EF Core translates the overlap as
  `start_time < @end AND @start < end_time`, and the GiST index is built on the expression
  `tstzrange(start_time, end_time, '[)')`. The same query written with `&&` on that expression uses
  the existing index (438 buffers, 9.8 ms). The index the query needs already exists; another one
  would only add write cost to every insert.
- **Not urgent:** 20 ms at 50,000 rows is acceptable. Reservations are kept as history, so the
  sequential scan grows linearly with every reservation ever made (about 14 buffers per 1,000 rows),
  while the index form grows with the number of spaces in the lot. If availability becomes slow,
  rewrite the predicate to the range form (for example through a mapped database function); do not
  add an index.
- `ix_reservations_space_id` (btree, from `InitialCreate`) was not used by any measured query
  (`idx_scan = 0`). It is kept: foreign key checks when a space is deleted and other queries were
  not measured here.

## Setup

- PostgreSQL 17 (`docker-compose.yml`), local machine (Windows, Docker Engine in WSL).
- Data: [scripts/perf/seed-reservations.sql](../scripts/perf/seed-reservations.sql). One lot
  "Perf" with 100 spaces and 500 reservations per space: 50,000 rows, 45,000 Active and 5,000
  Cancelled. Four 2-hour slots a day (05, 09, 13 and 17 UTC) from 2026-09-01 to 2027-01-03, so the
  data covers past and future. The script ends with `ANALYZE`, so the planner has fresh statistics.
- Plans: [scripts/perf/explain-queries.sql](../scripts/perf/explain-queries.sql), which changes
  nothing (the insert is rolled back).
- Interval queried: `[2026-12-10 10:00, 2026-12-10 12:00)` UTC, which overlaps the 09:00–11:00
  slot of every space, so every space is reported as taken.
- The plans below are from the **second** run. In the first run the buffers were the same, but the
  timings were several times higher (cold catalog and pages, for example 38 ms for one foreign key
  check). Buffers are the stable measure; single timings are indicative only.

Reproduce:

```bash
docker compose exec -T postgres psql -U citypark -d citypark -v ON_ERROR_STOP=1 < scripts/perf/seed-reservations.sql
docker compose exec -T postgres psql -U citypark -d citypark -v ON_ERROR_STOP=1 < scripts/perf/explain-queries.sql
```

## Results

| # | Query | Access to `reservations` | Buffers | Execution |
| --- | --- | --- | --- | --- |
| 1 | Availability, as sent by EF Core | Seq Scan, once (hashed subplan) | 718 | 20.0 ms |
| 2 | Availability, `tstzrange &&` form | Index Scan on the GiST index, once per space | 444 | 9.8 ms |
| 3 | Insert of a free slot | constraint check (not shown in the plan) | 20 | 17.0 ms |
| 4 | The constraint's probe as a query | Bitmap Index Scan on the GiST index | 4 | 0.26 ms |

### 1. Availability query, as sent by EF Core

The SQL from `AvailabilityService`, copied from the EF Core command log; `@end`, `@start` and
`@lotId` are inlined in the script:

```sql
SELECT p0.id AS "Id", p0.label AS "Label", NOT EXISTS (
    SELECT 1
    FROM reservations AS r
    WHERE r.status = 1 AND r.start_time < @end AND @start < r.end_time AND r.space_id = p0.id) AS "IsFree"
FROM parking_lots AS p
INNER JOIN parking_spaces AS p0 ON p.id = p0.lot_id
WHERE p.id = @lotId AND p0.is_active
ORDER BY p0.label
```

```text
 Nested Loop  (cost=0.14..1799.27 rows=100 width=22) (actual time=19.214..19.467 rows=100 loops=1)
   Buffers: shared hit=718
   ->  Index Scan using ix_parking_spaces_lot_id_label on parking_spaces p0  (cost=0.14..16.21 rows=100 width=37) (actual time=0.025..0.099 rows=100 loops=1)
         Index Cond: (lot_id = '00000000-0000-0000-0000-00000000a029'::uuid)
         Filter: is_active
         Buffers: shared hit=2
   ->  Materialize  (cost=0.00..1.02 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=100)
         Buffers: shared hit=1
         ->  Seq Scan on parking_lots p  (cost=0.00..1.01 rows=1 width=16) (actual time=0.036..0.041 rows=1 loops=1)
               Filter: (id = '00000000-0000-0000-0000-00000000a029'::uuid)
               Rows Removed by Filter: 3
               Buffers: shared hit=1
   SubPlan 2
     ->  Seq Scan on reservations r  (cost=0.00..1590.00 rows=7091 width=16) (actual time=14.993..18.695 rows=100 loops=1)
           Filter: ((start_time < '2026-12-10 12:00:00+00'::timestamp with time zone) AND ('2026-12-10 10:00:00+00'::timestamp with time zone < end_time) AND (status = 1))
           Rows Removed by Filter: 49900
           Buffers: shared hit=715
 Planning:
   Buffers: shared hit=163
 Planning Time: 2.803 ms
 Execution Time: 20.000 ms
```

- `SubPlan 2` with `loops=1` is a hashed subplan: PostgreSQL scans `reservations` once, keeps the
  `space_id` of every overlapping Active row in a hash table, and checks each space against it.
- The scan reads all 715 pages of the table and discards 49,900 of 50,000 rows.
- The planner estimated 7,091 matching rows instead of 100. It treats `start_time < @end` and
  `@start < end_time` as independent conditions, although they are not. With that estimate one
  full scan looks cheaper than 100 index lookups.
- No index can serve this predicate: the GiST index is on `tstzrange(start_time, end_time, '[)')`,
  not on the two columns, and there is no btree index on `start_time` or `end_time`.

### 2. Availability query with the range operator

Not used by the application. It shows what the planner does when the predicate matches the
indexed expression:

```sql
SELECT p0.id AS "Id", p0.label AS "Label", NOT EXISTS (
    SELECT 1
    FROM reservations AS r
    WHERE r.status = 1
      AND tstzrange(r.start_time, r.end_time, '[)') && tstzrange(@start, @end, '[)')
      AND r.space_id = p0.id) AS "IsFree"
FROM parking_lots AS p
INNER JOIN parking_spaces AS p0 ON p.id = p0.lot_id
WHERE p.id = @lotId AND p0.is_active
ORDER BY p0.label
```

```text
 Sort  (cost=519.11..519.36 rows=100 width=22) (actual time=9.634..9.654 rows=100 loops=1)
   Sort Key: p0.label
   Sort Method: quicksort  Memory: 28kB
   Buffers: shared hit=444
   ->  Nested Loop  (cost=0.00..515.79 rows=100 width=22) (actual time=1.826..9.050 rows=100 loops=1)
         Buffers: shared hit=441
         ->  Seq Scan on parking_lots p  (cost=0.00..1.01 rows=1 width=16) (actual time=0.018..0.023 rows=1 loops=1)
               Filter: (id = '00000000-0000-0000-0000-00000000a029'::uuid)
               Rows Removed by Filter: 3
               Buffers: shared hit=1
         ->  Seq Scan on parking_spaces p0  (cost=0.00..4.03 rows=100 width=37) (actual time=0.013..0.128 rows=100 loops=1)
               Filter: (is_active AND (lot_id = '00000000-0000-0000-0000-00000000a029'::uuid))
               Rows Removed by Filter: 62
               Buffers: shared hit=2
         SubPlan 1
           ->  Index Scan using ex_reservations_space_no_overlap on reservations r  (cost=0.28..24.38 rows=5 width=0) (actual time=0.086..0.086 rows=1 loops=100)
                 Index Cond: ((space_id = p0.id) AND (tstzrange(start_time, end_time, '[)'::text) && '["2026-12-10 10:00:00+00","2026-12-10 12:00:00+00")'::tstzrange))
                 Buffers: shared hit=438
 Planning:
   Buffers: shared hit=14
 Planning Time: 0.900 ms
 Execution Time: 9.790 ms
```

- One GiST lookup per space (`loops=100`), about 4.4 buffers each, and each stops at the first
  overlapping row.
- The partial condition `status = 1` is answered by the index itself, because the index only
  contains Active rows.

### 3. Insert of a free slot

The INSERT that EF Core sends for a new reservation, run inside `BEGIN … ROLLBACK`:

```sql
INSERT INTO reservations (id, currency, end_time, plate, price, space_id, start_time, status, user_id)
VALUES (gen_random_uuid(), 'BAM', '2026-12-10 12:00:00+00', 'A12K345', 2.00, @space_id,
        '2026-12-10 11:00:00+00', 1, @user_id)
RETURNING xmin;
```

```text
 Insert on reservations  (cost=0.00..0.01 rows=1 width=146) (actual time=14.238..14.243 rows=1 loops=1)
   Buffers: shared hit=20 dirtied=5
   ->  Result  (cost=0.00..0.01 rows=1 width=146) (actual time=0.063..0.065 rows=1 loops=1)
 Planning Time: 0.087 ms
 Trigger for constraint fk_reservations_asp_net_users_user_id: time=1.754 calls=1
 Trigger for constraint fk_reservations_parking_spaces_space_id: time=0.891 calls=1
 Execution Time: 16.983 ms
```

`EXPLAIN` does not show the exclusion check as its own node: it runs while the new row is inserted
into the GiST index. The 20 buffers cover the heap, all four indexes and the check. A sequential
check would read the whole table (715 buffers, as in query 1). The proof that the check uses the
index is query 4 and the index statistics below.

### 4. The constraint's probe as a query

The lookup that the constraint does for the insert above, written as a query:

```sql
SELECT 1
FROM reservations
WHERE status = 1
  AND space_id = @space_id
  AND tstzrange(start_time, end_time, '[)') && tstzrange('2026-12-10 11:00:00+00', '2026-12-10 12:00:00+00', '[)');
```

```text
 Bitmap Heap Scan on reservations  (cost=4.33..23.17 rows=5 width=4) (actual time=0.113..0.115 rows=0 loops=1)
   Recheck Cond: ((space_id = 'c5c474f7-0af4-4669-a0b7-a3d38c93eadb'::uuid) AND (tstzrange(start_time, end_time, '[)'::text) && '["2026-12-10 11:00:00+00","2026-12-10 12:00:00+00")'::tstzrange) AND (status = 1))
   Heap Blocks: exact=1
   Buffers: shared hit=4
   ->  Bitmap Index Scan on ex_reservations_space_no_overlap  (cost=0.00..4.33 rows=5 width=0) (actual time=0.086..0.088 rows=1 loops=1)
         Index Cond: ((space_id = 'c5c474f7-0af4-4669-a0b7-a3d38c93eadb'::uuid) AND (tstzrange(start_time, end_time, '[)'::text) && '["2026-12-10 11:00:00+00","2026-12-10 12:00:00+00")'::tstzrange))
         Buffers: shared hit=3
 Planning Time: 0.393 ms
 Execution Time: 0.256 ms
```

The index returns one row and the heap returns none: it is the row inserted and rolled back in
query 3. Its index entry stays until vacuum, and the visibility check on the heap skips it.

### 5. Index usage on `reservations`

```text
              index               |  size   | idx_scan
----------------------------------+---------+----------
 ex_reservations_space_no_overlap | 5824 kB |    45102
 ix_reservations_space_id         | 528 kB  |        0
 ix_reservations_user_id          | 336 kB  |        1
 pk_reservations                  | 2192 kB |        0
```

- After the seed, the first run showed exactly `45000` scans of the GiST index: one per Active
  insert. Cancelled rows are not in the partial index and are not checked.
- The second run shows `45102`: the 102 scans of the first run (100 from query 2, one from the
  insert in query 3, one from query 4). A session reports its statistics with a delay, so a run
  does not see its own scans.
- The GiST index (5.8 MB) is about the size of the table (715 pages, 5.6 MB): it stores the range
  and the `space_id` of every Active row.
