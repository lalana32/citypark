# ADR-005: Optimistic concurrency on xmin for cancellation

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

Cancelling twice is a conflict (R9, I9 in the [product brief](../product-brief.md)).
`Reservation.Cancel` enforces this for requests that come one after another: the second request
reads `Cancelled` and is rejected.

Two cancel requests for the same reservation can also arrive at the same moment: a double click,
two browser tabs, a client that retries after a timeout, or the owner and an administrator
together. Both read the row while it is still `Active`, both pass the domain check and both write
`Cancelled`. The end state is correct, but both callers are told they succeeded (a *lost update*).
Today nothing else happens on cancel; once a refund or a notification is attached, the second
"success" would run it twice.

Unlike overlap (ADR-002), this is not a rule across rows. It is a race on one row that was read
and then written.

## Decision

Reservations carry an optimistic concurrency token mapped to PostgreSQL's `xmin` system column.

- `xmin` holds the id of the transaction that last wrote the row, so it changes on every update.
  No real column is added: the migration `AddReservationConcurrencyToken` records the mapping in
  the EF Core model, and Npgsql emits no SQL for system columns.
- The token is a shadow property (`Version`, `uint`, `xid`) in `ReservationConfiguration`, so the
  domain does not know about it.
- EF Core sends `UPDATE ... WHERE id = @id AND xmin = @read_version`. If another request changed
  the row in between, no row is updated and EF Core throws `DbUpdateConcurrencyException`.
- `CancelReservationService` maps that exception to `DomainConflictException`, the same 409 a
  sequential second cancel gets.

## Alternatives

### Leave it and document it

The end state is already right. **Rejected because** the API would report success to a request
that changed nothing, and the gap becomes a double refund as soon as cancel has side effects.

### Pessimistic lock (`SELECT ... FOR UPDATE`)

Lock the row when reading it, so the second request waits. **Rejected because** it needs an
explicit transaction around read and write, inside the retrying execution strategy (CP-017), and
holds a lock for a case that is rare. Optimistic checking costs nothing when there is no race.

### Conditional update (`ExecuteUpdate ... WHERE status = Active`)

One statement, and zero updated rows means a conflict. **Rejected because** it bypasses
`Reservation.Cancel`, so the customer and administrator rules would have to be repeated in SQL.

### A `version` column of our own

An integer incremented on every save. **Rejected because** `xmin` already exists and is
maintained by PostgreSQL, so there is nothing to add or keep in sync.

## Consequences

### Positive

- Exactly one of N simultaneous cancels succeeds; the others get a conflict (covered by
  `ParallelCancelReservationTests`, 20 rounds of 20 calls).
- Every later update of a reservation (for example an extension) is protected the same way
  without more code.

### Negative and trade-offs

- `xmin` is PostgreSQL-specific; another database needs a real version column.
- Every use case that updates a reservation must handle `DbUpdateConcurrencyException`;
  otherwise the race surfaces as a 500 instead of a 409.
- The conflict message after a race ("changed by another request") is less specific than
  "already cancelled", because the service does not reload the row to find out why.
