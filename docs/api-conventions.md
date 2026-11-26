# API conventions

Rules that hold for every HTTP endpoint. The contract of the individual use cases is in
[api.md](api.md). Added in CP-020.

## Routes and versioning

- Business endpoints live under **`/api/v1`**. They are mapped on the group from
  `ApiRoutes.MapApiV1()`, never on the root.
- A change that breaks existing clients (a removed field, a new required parameter, a different
  meaning) gets a new prefix, `/api/v2`, and v1 keeps working. Adding an optional field or a new
  endpoint is not breaking and stays in v1.
- Health endpoints are not versioned: they are for the hosting platform, not for API clients.

## Error contract

Every error response is a ProblemDetails object ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457))
with the content type `application/problem+json`. Endpoints do not catch exceptions themselves.
`ApiExceptionHandler` maps them in one place.

| Exception | Status | `type` |
| --- | --- | --- |
| `RequestValidationException` (input that cannot mean anything, for example page 0, or a request DTO that fails its DataAnnotations) | 400 Bad Request | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `BadHttpRequestException` from minimal API binding: a body that is missing, not JSON, or has a value of the wrong type | 400 Bad Request (413 or 415 when binding reports those) | `https://tools.ietf.org/html/rfc9110#section-15.5.1` for 400 |
| `NotFoundException` | 404 Not Found | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| `DomainConflictException`, including `ReservationConflictException` | 409 Conflict | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| `DomainException` (any other broken business rule) | 422 Unprocessable Entity | `https://tools.ietf.org/html/rfc9110#section-15.5.21` |
| Anything else | 500 Internal Server Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

An unknown route also gets a 404 ProblemDetails body. A request without a valid session cookie
gets **401**, and one without the required role gets **403**, never a redirect to a login page
([auth.md](auth.md#session-cookie)).

### Fields

| Field | Content |
| --- | --- |
| `type` | One of the URIs above. Clients branch on `status`, `type` and `code`, never on the text |
| `title` | Short, fixed summary of the status, for example "A business rule was violated." |
| `status` | The HTTP status code, repeated in the body |
| `detail` | The exception message, written for the user. Absent on 500 outside Development |
| `instance` | The request path |
| `traceId` | W3C trace id of the request; the same id is in the server log |
| `errors` | Only on 400 and 422: field name to list of messages |
| `code` | Only on a 409 from creating a reservation: `space_taken` (R6), `plate_overlap` (R7) or `reservation_limit` (R10) ([api.md](api.md#conflicts-409)) |
| `reservationIds` | Only on the R12 409 from deactivating a space: the reservations that block it |

### Validation errors

400 and 422 carry an `errors` object with the same shape. Keys are the JSON (camelCase) names of
the request fields, for example `plate`. An error that belongs to no single field is listed under
the key `general`.

| Case | Status | `errors` key |
| --- | --- | --- |
| A required field is missing, `null`, empty or only spaces | 400 | that field |
| A value has the wrong JSON type or format, for example `"start": 42` | 400 | that field |
| The body is missing or is not valid JSON | 400 | `general` |
| The shape is valid but a business rule names a field, for example a start off the 15-minute grid (R1) | 422 | that field |
| The shape is valid but a rule is about the request as a whole, for example the minimum duration (R2) | 422 | `general` |

Unknown properties in a body are ignored, not rejected.

How it works:

- **Request DTOs** (for example `CreateReservationRequest`) declare the shape with DataAnnotations.
  Value fields are nullable, so a missing field fails `[Required]` instead of silently becoming
  `Guid.Empty` or `0001-01-01`. An endpoint opts in with `.WithValidation<TRequest>()`, which adds
  `ValidationFilter<TRequest>` and documents the 400 response in OpenAPI. Minimal APIs have no
  `[ApiController]`, so the filter does the automatic validation that attribute does for controllers.
- **Shape before rules:** the filter runs before the handler, so a request with an empty plate and an
  off-grid start gets 400, not 422.
- **Unreadable JSON:** `RouteHandlerOptions.ThrowOnBadRequest` is on in every environment (by default
  only Development), so binding failures reach `ApiExceptionHandler` and get the same shape. The
  field comes from `JsonException.Path`; the framework message is not passed on because it names
  C# types.
- **Rule violations by field:** a `DomainException` may set `Field` (the C# name, for example
  `Start`); the handler reports it under the camelCase key.

Example of a 422:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "A business rule was violated.",
  "status": 422,
  "detail": "A customer can cancel a reservation only before it starts (R9).",
  "instance": "/api/v1/reservations/0192f3c4-.../cancel",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
  "errors": {
    "general": ["A customer can cancel a reservation only before it starts (R9)."]
  }
}
```

**400 or 422:** 400 means the request itself is malformed or meaningless, whatever the data in
the database. 422 means the request is well formed but breaks a business rule (R1–R14 in the
[product brief](product-brief.md)). 409 is a 422 caused by the current state of an object rather
than by the input, so repeating the same request later may succeed.

### Unexpected errors

A 500 response never contains the exception message or stack trace outside Development. The
full exception is logged at Error level, and `traceId` links the response to that log entry.

## Health checks

| Endpoint | Checks | 200 | 503 |
| --- | --- | --- | --- |
| `GET /health/live` | none: the process answers | always | never |
| `GET /health/ready` | checks tagged `ready`: the database (`SELECT 1`, 5 s timeout) | all checks Healthy or Degraded | any check Unhealthy |

```json
{ "status": "Unhealthy", "checks": { "database": "Unhealthy" } }
```

- Liveness runs no checks on purpose. If it depended on the database, a database outage would
  make the platform restart every API instance, which fixes nothing.
- The database check uses a plain connection instead of `CanConnectAsync`, so EF Core's retry
  strategy ([ADR-002](adr/0002-overlap-prevention-with-exclusion-constraint.md)) cannot hold a
  failing check for many seconds.
- The body lists only check names and statuses; exception messages stay in the log.

## OpenAPI

- The OpenAPI document is served at **`/openapi/v1.json`** in every environment. It is the public
  contract and holds no secrets.
- The interactive **Scalar** UI is served at **`/scalar`** in Development only, and `dotnet run`
  opens it.
- Every endpoint declares its responses (`Produces`, `ProducesProblem`) so the document is
  complete. The `/api/v1` group already declares the 500 ProblemDetails response.
- Examples are added with `.WithResponseExample(status, value)` and `.WithParameterExample(name, value)`
  (`CityPark.Api.OpenApi`). The value is a real response object serialized with the API's JSON
  options, so an example cannot drift from the type. An example for an undeclared response or
  parameter throws when the document is generated, and the OpenAPI tests catch it.
- `Microsoft.OpenApi` is pinned to 2.13.1 in `CityPark.Api.csproj`, because the 2.0.0 that
  `Microsoft.AspNetCore.OpenApi` 10.0.4 brings in has a known vulnerability
  (GHSA-v5pm-xwqc-g5wc). It stays on 2.x, the line `Microsoft.AspNetCore.OpenApi` 10 is built on.

## Tests

`tests/CityPark.Api.Tests` hosts the real API in memory with `WebApplicationFactory`:

- `ApiFactory` runs in Production against an unreachable database (port 1) and adds test-only
  routes under `/_test/throw/{kind}` through an `IStartupFilter`. Those routes run after the real
  pipeline, so their exceptions pass through the real exception handler.
- `ValidationEndpoints` adds test-only routes under `/_test/validation/` that bind the real
  `CreateReservationRequest` with `.WithValidation<T>()`; one of them then runs the domain booking
  rules, so both 400 and 422 are tested through real binding.
- `PostgresApiFactory` connects to a PostgreSQL 17 container (Testcontainers), migrated before
  the API starts, for the healthy readiness case and the authentication tests
  ([auth.md](auth.md#tests)).
