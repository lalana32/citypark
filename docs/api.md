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

## Own reservations

The signed-in customer's reservations. Only the caller's own reservations are returned; the user id comes from the sign-in cookie, never from a parameter ([ADR-003](adr/0003-cookie-authentication-with-identity.md)).

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

Implemented by `ListOwnReservationsService`; tested against PostgreSQL in `ListOwnReservationsServiceTests`.
