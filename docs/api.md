# API contract

This file describes the contract of the use cases that the HTTP API will expose. Endpoints are added by later cards; the rules here hold for them. Routes, the error contract and health checks are in [api-conventions.md](api-conventions.md).

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
  ]
}
```

**Known limit:** a value without an offset (`2026-12-01T10:00:00`) is parsed in the server's time zone by ASP.NET Core binding, so clients must always send one.

## Own reservations

The signed-in customer's reservations. Only the caller's own reservations are returned; the user id comes from the sign-in cookie, never from a parameter ([ADR-003](adr/0003-cookie-authentication-with-identity.md)). Added in CP-024.

Every endpoint requires sign-in: without a valid session cookie it returns **401**.

| Endpoint | Success | Errors |
|---|---|---|
| `POST /api/v1/reservations` | **201** with a `Location` header (`/api/v1/reservations/{id}`) and the reservation, including the stored `price` | 400 for a missing or malformed field; 404 for an unknown space; **409** when the space already has an overlapping Active reservation (R6); 422 for a broken booking rule (R1–R5, R11, R14) |
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

**Why the customer cancel ignores the Admin role:** these routes act on the caller's own reservations only. Cancelling any reservation at any time is a separate admin endpoint.

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
