# ADR-002: Overlap prevention with a PostgreSQL exclusion constraint

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

The central requirement of CityPark is that a parking space is never reserved twice for
overlapping times, even when two requests arrive at the same moment (R6). The same holds for a
vehicle: one plate cannot have two overlapping reservations in any lot (R7). Intervals are
half-open, `[start, end)`, so 10:00–11:00 and 11:00–12:00 do not overlap. Only Active
reservations count; a cancelled reservation frees the space and the plate (R9).

A check in application code ("is there an overlapping reservation?") followed by an insert is a
**race condition**: two requests can both run the check before either inserts, both see a free
space and both insert. The guarantee must hold under concurrency, not only in a single-user test.

## Decision

The database enforces both rules with PostgreSQL **exclusion constraints** on the reservation
table:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE reservations
  ADD CONSTRAINT ex_reservations_space_no_overlap
  EXCLUDE USING gist (
    space_id WITH =,
    tstzrange(start_time, end_time, '[)') WITH &&
  ) WHERE (status = 1);  -- 1 = Active

ALTER TABLE reservations
  ADD CONSTRAINT ex_reservations_plate_no_overlap
  EXCLUDE USING gist (
    plate WITH =,
    tstzrange(start_time, end_time, '[)') WITH &&
  ) WHERE (status = 1);
```

- `tstzrange(..., '[)')` builds the half-open interval; `&&` means "overlaps".
- `btree_gist` lets a GiST index compare plain values (`space_id`, `plate`) with `=`.
- The `WHERE` clause makes the constraint partial, so cancelled reservations are ignored.

The constraints are created in an EF Core migration with raw SQL, because EF Core cannot model
exclusion constraints.

Space assignment: the application selects free candidate spaces for the interval and inserts the
reservation for the first one. If the insert fails with SQLSTATE `23P01`
(`exclusion_violation`) on the space constraint, another request took that space in the
meantime, and the next candidate is tried. When no candidate is left, or the plate constraint is
violated, the API returns `409 Conflict`.

## Alternatives

### Check in application code, then insert

**Rejected because** of the race condition described above. It works in every manual test and
fails under real concurrent load.

### Pessimistic locking (`SELECT ... FOR UPDATE` on the space, or advisory locks)

Lock the space row before checking and inserting. **Rejected because** every code path that
writes reservations must remember to take the lock in the same order; one missed lock breaks the
guarantee silently. It also does not cover R7, which spans spaces and lots.

### `SERIALIZABLE` transaction isolation

**Rejected because** it detects the conflict only as a generic serialization failure that needs
retries for every reservation write, and the guarantee depends on every writer using that
isolation level.

### One row per 15-minute slot with a unique index

Store a reservation as rows `(space_id, slot_start)` and rely on a unique index. **Rejected
because** a two-hour reservation becomes eight rows, cancellation and extension touch many rows,
and the interval is no longer one value. It would work on any database, which is its main merit.

## Consequences

### Positive

- The rule holds for every writer: the API, a future background job, a manual SQL statement.
- No locks are held while the application chooses a space; the database resolves the race.
- The rule is stated once, declaratively, next to the data it protects.

### Negative and trade-offs

- **PostgreSQL only.** The application cannot switch to SQL Server or SQLite without replacing
  this mechanism. The EF Core in-memory provider and SQLite cannot be used in tests that touch
  reservations; those tests run against real PostgreSQL (for example with Testcontainers).
- The constraint lives in a raw SQL migration, invisible in the EF Core model. A comment in the
  model configuration points to the migration.
- The application must translate `23P01` into a domain conflict and retry the next candidate
  space. This mapping needs its own concurrency tests.
- The GiST indexes add write cost to every reservation insert and update; at the expected volume
  this is negligible.
- The constraint covers overlap only. R10 (at most 10 active reservations per user) is still
  checked in application code and can be exceeded by one under simultaneous requests.
