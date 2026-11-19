# Testing

## Concurrency tests

Double booking only appears when requests run at the same moment, so it is tested with real
parallel load against PostgreSQL 17 (Testcontainers). Added in CP-017.

- **Code:** `tests/CityPark.Infrastructure.Tests/Reservations/ParallelReservationTests.cs`
- **Helper:** `tests/CityPark.Infrastructure.Tests/Concurrency/ParallelRunner.cs`

### How a test runs

1. `ParallelRunner.RunAsync(n, call)` starts `n` calls on the thread pool and holds them at an
   async barrier. The last call to arrive releases all of them together.
2. Each call opens its own DI scope, so it gets its own `CreateReservationService`, `DbContext`
   and database connection, like separate HTTP requests in the API.
3. The runner returns one outcome per call: success, or the exception it threw. Any exception
   other than `ReservationConflictException` fails the test.
4. Each scenario runs 20 rounds in one test, every round on a new lot, to expose flaky behaviour.

The tests assert on outcomes and the final database state only, never on timing.

### Scenarios

All intervals are on Tuesday 1 December 2026, Sarajevo time.

| # | Requests | Space | Intervals | Expected |
| --- | --- | --- | --- | --- |
| A | 20 | one | all `[10:00, 11:00)` | exactly 1 succeeds, 19 conflict, 1 Active row |
| B | 20 | one | staggered by 15 minutes: `[10:00, 11:00)`, `[10:15, 11:15)`, … | no two Active rows overlap; Active rows = successes |
| C | 20 | five, round-robin | all `[10:00, 11:00)` | exactly 5 succeed (one per space), 15 conflict |
| D | 10 | one | adjacent 30 minutes: `[10:00, 10:30)`, `[10:30, 11:00)`, … | all 10 succeed |

In scenario B the number of successes depends on which request wins each race, so only the
invariant is checked: no overlapping Active rows remain.

### Finding: deadlocks on the exclusion constraint

The first run of scenario B failed with `40P01` (`deadlock detected`, "while checking exclusion
constraint"). Two parallel inserts can each see the other's uncommitted row and wait for each
other. PostgreSQL aborts one of them. Without a retry this reached the caller as an unexpected
error instead of a conflict.

The fix is `EnableRetryOnFailure()` in `AppDbContextOptions`: EF Core retries transient errors
such as `40P01`, and the retried insert either succeeds or fails with a real `23P01` conflict. A
deadlock is not mapped to a conflict, because the aborted request may not overlap any reservation
that is finally committed. See
[ADR-002](adr/0002-overlap-prevention-with-exclusion-constraint.md#concurrency-tests).

### Parallel cancellation

Added in CP-018: `tests/CityPark.Infrastructure.Tests/Reservations/ParallelCancelReservationTests.cs`.

| Test | Setup | Expected |
| --- | --- | --- |
| Stale copy | one context reads the reservation, the service then cancels it, the stale copy is cancelled and saved | `DbUpdateConcurrencyException` from the `xmin` token |
| Simultaneous cancels | 20 cancels of one reservation by its owner, 20 rounds | exactly 1 succeeds, 19 get `DomainConflictException` |

A late request gets the conflict either from the domain (it read `Cancelled`) or from the
token (it read a stale `Active`). Both are a 409. See
[ADR-005](adr/0005-optimistic-concurrency-on-xmin-for-cancellation.md).

### Running the suite repeatedly

The card requires 20 consecutive green runs. In PowerShell:

```powershell
1..20 | ForEach-Object { dotnet test --project tests/CityPark.Infrastructure.Tests; if ($LASTEXITCODE -ne 0) { throw "Run $_ failed" } }
```
