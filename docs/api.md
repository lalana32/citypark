# API contract

This file describes every endpoint of the HTTP API: what it returns, which errors it gives and why. The rules that hold for all endpoints (versioning, the ProblemDetails error contract, health checks) are in [api-conventions.md](api-conventions.md); sign-in, the session cookie and CSRF are in [auth.md](auth.md).

## OpenAPI

The machine-readable contract is the OpenAPI 3.1 document. This file explains it; when the two disagree, the document is what the API really does.

| Where | URL or path |
| --- | --- |
| Running API, every environment | `/openapi/v1.json`, for example <http://localhost:8080/openapi/v1.json> on the [Docker stack](../README.md#quick-start) or <http://localhost:5114/openapi/v1.json> with `dotnet run` |
| Interactive UI (Scalar), Development only | <http://localhost:5114/scalar>, opened by `dotnet run --project src/CityPark.Api --launch-profile http` |
| Committed copy in the repository | [`web/openapi/v1.json`](../web/openapi/v1.json) |

- The committed copy is kept equal to the API by `OpenApiSnapshotTests`, and the frontend types (`web/src/api/schema.d.ts`) are generated from it. CI fails when either is out of date ([README](../README.md#api-types)).
- Every endpoint declares all its responses, and the main ones carry examples built from the real response types ([api-conventions.md](api-conventions.md#openapi)).
- Tags group the endpoints as in the overview below: `Authentication`, `Lots`, `Reservations`, `Admin`, `Health`.

## Endpoint overview

| Method and route | Who | Success | Details |
| --- | --- | --- | --- |
| `POST /api/v1/auth/register` | anyone, rate limited | 201 | [auth.md](auth.md#endpoints) |
| `POST /api/v1/auth/login` | anyone, rate limited | 200, sets the cookie | [auth.md](auth.md#endpoints) |
| `POST /api/v1/auth/logout` | signed in | 204 | [auth.md](auth.md#endpoints) |
| `GET /api/v1/auth/me` | signed in | 200 | [auth.md](auth.md#endpoints) |
| `GET /api/v1/auth/csrf` | anyone | 200 | [auth.md](auth.md#csrf-protection) |
| `GET /api/v1/lots` | anyone | 200, paged | [Lots](#lots) |
| `GET /api/v1/lots/{id}` | anyone | 200 | [Lots](#lots) |
| `GET /api/v1/lots/{id}/availability` | anyone | 200 | [Availability](#availability) |
| `GET /api/v1/lots/{id}/quote` | anyone | 200 | [Price quote](#price-quote) |
| `POST /api/v1/reservations` | signed in | 201 | [Own reservations](#own-reservations) |
| `GET /api/v1/reservations` | signed in | 200, paged | [Own reservations](#own-reservations) |
| `GET /api/v1/reservations/{id}` | signed in, owner | 200 | [Own reservations](#own-reservations) |
| `POST /api/v1/reservations/{id}/cancel` | signed in, owner | 204 | [Own reservations](#own-reservations) |
| `POST /api/v1/admin/lots` | Admin | 201 | [Admin: lots and spaces](#admin-lots-and-spaces) |
| `PUT /api/v1/admin/lots/{id}` | Admin | 200 | [Admin: lots and spaces](#admin-lots-and-spaces) |
| `POST /api/v1/admin/lots/{id}/spaces` | Admin | 201 | [Admin: lots and spaces](#admin-lots-and-spaces) |
| `POST /api/v1/admin/lots/{lotId}/spaces/{spaceId}/deactivate` | Admin | 204 | [Admin: lots and spaces](#admin-lots-and-spaces) |
| `GET /api/v1/admin/reservations` | Admin | 200, paged | [Admin: reservations](#admin-reservations) |
| `POST /api/v1/admin/reservations/{id}/cancel` | Admin | 200 | [Admin: reservations](#admin-reservations) |
| `GET /health/live` | anyone | 200 | [api-conventions.md](api-conventions.md#health-checks) |
| `GET /health/ready` | anyone | 200 or 503 | [api-conventions.md](api-conventions.md#health-checks) |

Common to all `/api/v1` routes:

- Every `POST`, `PUT`, `PATCH` and `DELETE` needs the `X-XSRF-TOKEN` header from `GET /auth/csrf`, otherwise **400** ([auth.md](auth.md#csrf-protection)).
- A route that needs sign-in answers **401** without a valid session cookie, and an Admin route answers **403** to a customer, before the body is validated.
- Every error is `application/problem+json`. **400** is a malformed request, **404** an unknown or foreign resource, **409** a conflict with the current state, **422** a broken business rule ([api-conventions.md](api-conventions.md#error-contract)).

## Conflicts (409)

A 409 means the request is valid but the current data does not allow it; sending it again later may succeed. Every 409 except the one on register comes from a `DomainConflictException`, which `ApiExceptionHandler` maps in one place.

| Case | Rule | Raised by | Decided by |
| --- | --- | --- | --- |
| The given space already has an overlapping Active reservation | R6 | `ReservationConflictException` | Exclusion constraint `ex_reservations_space_no_overlap`, SQLSTATE `23P01` ([ADR-002](adr/0002-overlap-prevention-with-exclusion-constraint.md)) |
| No space of the lot is free, or all candidates were lost to parallel requests | R6 | `NoFreeSpaceException` | The same constraint, once per candidate ([Automatic space assignment](#automatic-space-assignment)) |
| The reservation is already cancelled, also by a simultaneous request | R9 | `DomainConflictException` | Domain status check, or the `xmin` concurrency token ([ADR-005](adr/0005-optimistic-concurrency-on-xmin-for-cancellation.md)) |
| The lot already has a space with this label | I1 | `DomainConflictException` | Domain check in `ParkingLot`, and the unique index `ix_parking_spaces_lot_id_label` (`23505`) for parallel requests |
| The space has Active reservations that have not ended | R12 | `SpaceHasActiveReservationsException`, lists `reservationIds` | Application check |
| The email is already registered | – | The register handler returns the 409 itself (`TypedResults.Problem`) | Identity ([D5](decisions.md#d5-register-reveals-that-an-email-is-taken)) |

The database errors are recognised by SQLSTATE **and** constraint name in `PostgresDatabaseErrors`, never by message text. Any other database error is not turned into a 409; it stays a 500.

## Pagination

Every list is paged with two parameters.

| Parameter | Meaning | Rule |
|---|---|---|
| `page` | 1-based page number | Must be 1 or greater, otherwise 400 Bad Request |
| `pageSize` | Items per page | Must be 1 or greater, otherwise 400 Bad Request. A value above **50** is clamped to 50, not rejected |

The response echoes the values actually used, so a client that asked for `pageSize=100` sees that it got 50:

```json
{
  "items": [ ... ],
  "page": 2,
  "pageSize": 50,
  "totalCount": 51,
  "totalPages": 2
}
```

- `pageSize` is the size after clamping, and the offset uses it: page 2 with `pageSize=100` starts at the 51st item.
- A page after the last one is not an error; it returns an empty `items` with the correct `totalCount`.
- A page so large that its offset does not fit a 32-bit integer is rejected with 400.

**Why clamp instead of reject above 50:** a request for too many items can still be answered usefully, and the echoed `pageSize` tells the client what happened. Values below 1 cannot mean anything, so they are rejected. Implemented by `PageRequest` and `PagedResult<T>` in `CityPark.Application.Common`.

### Ordering and stability

Each list has a total order: a business sort key followed by the unique id as a tie-breaker. For the same data, every page boundary falls in the same place, so no item appears on two pages or on none.

This is offset pagination, not a snapshot. If items are added or cancelled between two page requests, later pages shift by those items. Keyset pagination would avoid that but was not needed for lists of this size.

## Lots

Public: no sign-in needed. Added in CP-023.

| Endpoint | Returns | Errors |
|---|---|---|
| `GET /api/v1/lots?page=&pageSize=` | A page of lots (default `page=1`, `pageSize=20`) | 400 for page or page size below 1 |
| `GET /api/v1/lots/{id}` | One lot | 404 for an unknown id |

- **Order:** name ascending, then id.
- **Fields:** `id`, `name`, `address`, `hourlyPrice`, `currency`, `opensAt`, `closesAt` (local Europe/Sarajevo wall-clock times, for example `"07:00:00"`), `totalSpaces` (active spaces only).

```json
{
  "id": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01",
  "name": "Centar",
  "address": "Ferhadija 1",
  "hourlyPrice": 2.50,
  "currency": "BAM",
  "opensAt": "07:00:00",
  "closesAt": "22:00:00",
  "totalSpaces": 30
}
```

## Availability

`GET /api/v1/lots/{id}/availability?start=...&end=...` returns the active spaces of the lot that no active reservation overlaps in `[start, end)`. Public. Added in CP-023.

- **`start` and `end`** are required ISO 8601 instants **with an offset**: `2026-12-01T09:00:00Z` or `2026-12-01T10:00:00%2B01:00`. In a query string `+` means a space, so it must be sent as `%2B`. The instants are converted to UTC before any rule is checked ([ADR-004](adr/0004-utc-storage-and-sarajevo-business-day.md)).
- The interval must pass the same booking rules as a reservation (R1 grid, R2 minimum duration, opening hours, R4 horizon, R5 start in the past), so a free space in the answer can actually be booked.
- The response echoes `start` and `end` with the Europe/Sarajevo offset.
- **`freeSpaces`** lists the free spaces only, in label order.
- **`spaces`** lists every active space of the lot in label order, each with `isFree` for the checked interval, so a client can draw the whole lot with its occupied spaces. Inactive spaces are left out of both lists. Added in CP-048.

| Case | Status | `errors` key |
|---|---|---|
| `start` or `end` missing | 400 | that parameter |
| Off the 15-minute grid | 422 | `start` or `end` |
| Shorter than 30 minutes, outside opening hours, too far ahead or in the past | 422 | `general` |
| Unknown lot | 404 | — |

```json
{
  "lotId": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01",
  "start": "2026-12-01T10:00:00+01:00",
  "end": "2026-12-01T11:00:00+01:00",
  "totalSpaces": 30,
  "availableSpaces": 2,
  "freeSpaces": [
    { "id": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02", "label": "A-02" },
    { "id": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e17", "label": "A-17" }
  ],
  "spaces": [
    { "id": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e01", "label": "A-01", "isFree": false },
    { "id": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02", "label": "A-02", "isFree": true },
    { "id": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e03", "label": "A-03", "isFree": false }
  ]
}
```

The example shows three of the 30 entries of `spaces`.

**Known limit:** a value without an offset (`2026-12-01T10:00:00`) is parsed in the server's time zone by ASP.NET Core binding, so clients must always send one.

## Price quote

`GET /api/v1/lots/{id}/quote?start=...&end=...` returns the price a reservation of the lot for `[start, end)` would get. Public and read-only: nothing is reserved and free spaces are not checked (use [availability](#availability) for that). Added in CP-031.

- **`start` and `end`** follow the same format and the same booking rules as [availability](#availability), with the same 400, 404 and 422 cases. A quote is only given for an interval that can be booked.
- **`billedHours`** is the rounded duration: every started hour is billed as a full hour (R8).
- **`price`** is `billedHours × hourlyPrice` in `currency`, computed by the same domain method as a reservation's price. It equals the price of a reservation made for the same interval while the lot's hourly price is unchanged.

```json
{
  "lotId": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01",
  "start": "2026-12-01T10:00:00+01:00",
  "end": "2026-12-01T11:15:00+01:00",
  "billedHours": 2,
  "price": 5.00,
  "currency": "BAM"
}
```

## Own reservations

The signed-in customer's reservations. Only the caller's own reservations are returned; the user id comes from the sign-in cookie, never from a parameter ([ADR-003](adr/0003-cookie-authentication-with-identity.md)). Added in CP-024.

Every endpoint requires sign-in: without a valid session cookie it returns **401**.

| Endpoint | Success | Errors |
|---|---|---|
| `POST /api/v1/reservations` | **201** with a `Location` header (`/api/v1/reservations/{id}`) and the reservation, including the stored `price` and the `spaceLabel` it got | 400 for a missing or malformed field, or for both or neither of `spaceId` and `lotId`; 404 for an unknown space or lot; **409** when the space already has an overlapping Active reservation (R6), or when the lot has no free space; 422 for a broken booking rule (R1–R5, R11, R14) |
| `GET /api/v1/reservations?page=&pageSize=` | 200 with a page of own reservations (default `page=1`, `pageSize=20`) | 400 for page or page size below 1 |
| `GET /api/v1/reservations/{id}` | 200 with the reservation | **404** for an unknown id **and for another user's reservation** |
| `POST /api/v1/reservations/{id}/cancel` | 204 | **404** as above (the reservation stays Active); **409** when it is already cancelled; **422** once it has started (R9) |

Request body of the create endpoint:

```json
{
  "spaceId": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02",
  "start": "2026-12-01T10:00:00+01:00",
  "end": "2026-12-01T11:30:00+01:00",
  "plate": "A12-K-345"
}
```

**Why 404 and not 403 for another user's reservation:** 403 would confirm that the id exists, so ids of other users could be probed. The owner is part of the query, so a foreign row is never even read ([product brief, R9](product-brief.md)).

**Why the customer cancel ignores the Admin role:** these routes act on the caller's own reservations only. Cancelling any reservation at any time is a separate admin endpoint ([Admin: reservations](#admin-reservations)).

- **Order:** start time descending (newest first), then id descending.
- **Fields per item:** `id`, `lotName`, `spaceLabel`, `start`, `end` (UTC, [ADR-004](adr/0004-utc-storage-and-sarajevo-business-day.md)), `plate`, `price`, `currency`, `state`.
- **`state`** is derived when the list is read and never stored:

| State | When |
|---|---|
| `Upcoming` | Active and the start is in the future |
| `InProgress` | Active, start ≤ now < end |
| `Finished` | Active and the end has passed; at exactly the end the reservation is already Finished (`[start, end)`) |
| `Cancelled` | Cancelled, whatever the time |

`InProgress` is used instead of `Active`, so it cannot be confused with the stored status Active ([product brief, glossary](product-brief.md#7-domain-glossary)).

Implemented by `OwnReservationQueryService` (`ListAsync` and `GetAsync`); tested against PostgreSQL in `OwnReservationQueryServiceTests`.

### Automatic space assignment

Instead of `spaceId`, the body may name a lot with `lotId`; the system then picks the space. Added in CP-030.

```json
{
  "lotId": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01",
  "start": "2026-12-01T10:00:00+01:00",
  "end": "2026-12-01T11:30:00+01:00",
  "plate": "A12-K-345"
}
```

- **Exactly one** of `spaceId` and `lotId`. Both or neither gives **400** with the message under both keys in `errors`.
- **`spaceId` works as before.** That space or a 409; the request never falls back to another space.
- **Which space:** the first active space of the lot, ordered by label, that no Active reservation overlaps. No fairness or optimization. The response shows it in `spaceLabel`.
- **Rules first:** a broken booking rule is 422 even when the lot is full.
- **Parallel requests:** the free spaces are only candidates; the exclusion constraint decides ([ADR-002](adr/0002-overlap-prevention-with-exclusion-constraint.md#create-reservation-by-lot)). A request that loses a space to a parallel request (`23P01`) tries the next candidate, up to `CreateReservationService.MaxAttempts` (10) inserts. When no candidate is left the answer is **409** and nothing is written.

**Known limit:** under heavy load on a lot with more than 10 free spaces, a request can lose all 10 candidates and get 409 while a free space still exists. The client can simply send the request again.

## Admin: lots and spaces

Inventory maintenance for the Admin role, OpenAPI tag `Admin`. Added in CP-027. Without a session cookie every route returns **401**; a signed-in customer gets **403**, before the body is validated.

| Endpoint | Success | Errors |
|---|---|---|
| `POST /api/v1/admin/lots` | **201** with a `Location` header (`/api/v1/lots/{id}`) and the lot as the public route returns it | 400 for a missing or malformed field; **422** for a broken lot rule |
| `PUT /api/v1/admin/lots/{id}` | 200 with the updated lot | 400; 404 for an unknown lot; 422 |
| `POST /api/v1/admin/lots/{id}/spaces` | **201** with `id`, `label`, `isActive`; no `Location`, as there is no route for one space | 400; 404; **409** when the lot already has the label (I1); 422 for a blank label |
| `POST /api/v1/admin/lots/{lotId}/spaces/{spaceId}/deactivate` | 204 | 404 for an unknown lot or a space of another lot; **409** while the space has Active reservations that have not ended (R12) |

Body of both lot routes; `PUT` replaces all six values:

```json
{
  "name": "Centar",
  "address": "Ferhadija 1",
  "hourlyPrice": 2.50,
  "currency": "BAM",
  "opensAt": "07:00:00",
  "closesAt": "22:00:00"
}
```

- **422 rules** come from the domain, with the field as the `errors` key: `hourlyPrice` positive with at most two decimals, `currency` three upper-case letters, `closesAt` after `opensAt`, `name` and `address` not blank.
- **Labels** are trimmed and upper-cased, so `" a-01 "` and `A-01` are the same label. A duplicate is a 409 and not a 422: the input is valid, it conflicts with the lot's current spaces. Two parallel requests with the same label are stopped by the unique index `ix_parking_spaces_lot_id_label`, which the service also maps to 409.
- **A new rate or new opening hours** apply only to new reservations. Existing reservations keep their stored price and stay valid (R13, I6).
- **Deactivation** is blocked by every Active reservation whose end is after now, including one in progress: its car is still in the space. The 409 lists them, earliest start first, so the admin can cancel them first:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The request conflicts with the current state.",
  "status": 409,
  "detail": "Space 019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02 has 1 active reservation(s) that have not ended; cancel them first.",
  "reservationIds": [ "019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01" ]
}
```

Deactivating an inactive space again returns 204. There is no route to reactivate a space or to deactivate a lot yet.

**Known limit:** the deactivation check and a new reservation are not serialized. A reservation created in the milliseconds between the check and the save can land on a space that becomes inactive. Closing that gap would mean locking the space row on every reservation, which was not worth it for a rare admin action; the admin sees the reservation in the list and can cancel it.

Implemented by `AdminLotService`; tested against PostgreSQL in `AdminLotServiceTests` and over HTTP in `AdminEndpointsTests`.

## Admin: reservations

Every user's reservations for the Admin role, OpenAPI tag `Admin`. Added in CP-028. Without a session cookie both routes return **401**; a signed-in customer gets **403**.

| Endpoint | Success | Errors |
|---|---|---|
| `GET /api/v1/admin/reservations?lotId=&from=&to=&status=&page=&pageSize=` | 200 with a page of reservations of all users (default `page=1`, `pageSize=20`) | **400** for an unknown `status`, `from` after `to`, a malformed value, or page or page size below 1 |
| `POST /api/v1/admin/reservations/{id}/cancel` | **200** with the cancelled reservation | 404 for an unknown id; **409** when it is already cancelled |

- **Filters** are optional and combine with AND:
  - `lotId`: only reservations in that lot. An unknown lot gives an empty page, not 404.
  - `from`, `to`: local Europe/Sarajevo dates (`2026-12-01`), both inclusive, matched against the day the reservation **starts**. A reservation at 00:00 local on 3 December is 23:00 UTC on 2 December and is listed under 3 December ([ADR-004](adr/0004-utc-storage-and-sarajevo-business-day.md)).
  - `status`: the stored status, `Active` or `Cancelled`. The derived `state` is not a filter, because it depends on the time of the request.
- **Order:** start time descending, then id descending, as in the customer's list.
- **Fields per item:** the fields of the [customer's list](#own-reservations) plus `userId`, `lotId`, `spaceId` and `status`.
- **Cancel at any time:** the admin may cancel before, during or after the reservation (R9). It uses the same `CancelReservationService` as the customer route, with the Administrator actor, so the rule lives in the domain only. It answers 200 with the body instead of 204, so the admin sees whose reservation was cancelled.

```json
{
  "id": "019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01",
  "userId": "019ad0a3-2f1e-7c4d-8b5a-6e7f8091a2b3",
  "lotId": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01",
  "lotName": "Centar",
  "spaceId": "019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02",
  "spaceLabel": "A-02",
  "start": "2026-12-01T09:00:00+00:00",
  "end": "2026-12-01T10:30:00+00:00",
  "plate": "A12K345",
  "price": 5.00,
  "currency": "BAM",
  "status": "Cancelled",
  "state": "Cancelled"
}
```

There is no export or report (out of scope of CP-028).

Implemented by `AdminReservationQueryService` and `CancelReservationService`; tested against PostgreSQL in `AdminReservationQueryServiceTests` and over HTTP in `AdminReservationEndpointsTests`.
