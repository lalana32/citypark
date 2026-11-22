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

### Over HTTP

Added in CP-026: `tests/CityPark.Api.Tests/Reservations/ParallelReservationHttpTests.cs`.

Two signed-in customers post `[10:00, 11:30)` and `[10:30, 12:00)` for the same space, released
together at a barrier like `ParallelRunner`. Each of 20 rounds uses a new lot and a new plate pair,
so only the space rule (R6) can fail. Every round must give exactly one `201` and one `409`
(`application/problem+json`), and availability for the interval must then show no free space.
This covers the whole pipeline the service-level tests skip: cookie, CSRF, validation, the
exception handler and the `23P01` to `409` mapping.

## Authorization matrix

Added in CP-026: `tests/CityPark.Api.Tests/Authorization/AuthorizationMatrixTests.cs`. Every row
goes through the real HTTP pipeline against PostgreSQL 17.

| Endpoint | Anonymous | Customer | Other customer | Admin |
| --- | --- | --- | --- | --- |
| `GET /api/v1/lots`, `/lots/{id}`, `/lots/{id}/availability` | open | open | open | open |
| `GET /api/v1/auth/csrf`, `GET /health/live` | open | open | open | open |
| `POST /api/v1/auth/register`, `/auth/login` | open, rate limited | – | – | – |
| `POST /api/v1/auth/logout`, `GET /auth/me` | 401 | 2xx | 2xx | 2xx |
| `POST /api/v1/reservations`, `GET /reservations` | 401 | 201 / 200 | 201 / 200 | 201 / 200 |
| `GET /api/v1/reservations/{id}` | 401 | 200 | 404 | 404 |
| `POST /api/v1/reservations/{id}/cancel` | 401 | 204 | 404 | 404 |
| Admin route (`/_test/auth/admin`) | 401 | 403 | 403 | 200 |
| `POST /api/v1/admin/lots`, `PUT /admin/lots/{id}`, `POST /admin/lots/{id}/spaces`, `POST /admin/lots/{lotId}/spaces/{spaceId}/deactivate` | 401 | 403 | 403 | see `AdminEndpointsTests` |
| `GET /api/v1/admin/reservations`, `POST /admin/reservations/{id}/cancel` | 401 | 403 | 403 | see `AdminReservationEndpointsTests` |

Notes:

- "Open" rows assert the exact status (200, or 404 for an unknown id), so a route that is closed
  by mistake fails the test.
- Register and login are covered in `AuthenticationEndpointsTests` and `RateLimitingTests`.
- Another user's reservation is `404`, not `403`, so the API never confirms that an id exists.
- The reservation routes act only on the caller's own reservations, also for an admin. Viewing
  and cancelling any reservation goes through the admin reservation routes (CP-028).
- `AdminReservationEndpointsTests` moves the fake clock (to one minute before a start, or into a
  reservation), so it has a class and a factory of its own. `FakeTimeProvider` cannot go back in
  time, so each test first moves the clock forward to a day of its own and books on that day.
- The admin routes (CP-027) get `{}` as the body, which most of them reject with 400. A 403
  proves that authorization runs before validation. The Admin column is in
  `tests/CityPark.Api.Tests/Admin/AdminEndpointsTests.cs`, which checks each acceptance criterion
  of the card over HTTP; the service rules are also tested against PostgreSQL in
  `AdminLotServiceTests`.

## Error contract

Added in CP-026: `tests/CityPark.Api.Tests/Errors/ErrorContractTests.cs`. Each status comes from a
real endpoint, because errors are written in five places: the exception handler, `TypedResults.Problem`
in the auth handlers, the CSRF filter, `UseStatusCodePages` for 401 and 403, and the rate limiter.

| Status | Scenarios |
| --- | --- |
| 400 | malformed JSON body, missing CSRF token, `page=0` |
| 401 | no cookie, wrong password |
| 403 | customer on the admin route |
| 404 | unknown lot |
| 409 | duplicate email, overlapping reservation |
| 422 | weak password, start off the 15-minute grid |
| 429 | eleventh login within the window |

Every response must have content type `application/problem+json`, the status in the body, an RFC
`type`, a `title`, the request path as `instance` and a `traceId`. The body must not contain a
stack frame (`   at `, `.cs:line`), an exception name, `Npgsql` or connection string keys (`Host=`,
`Username=`, `Password=`). The `500` case is covered by `ExceptionMappingTests`.
