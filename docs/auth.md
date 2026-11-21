# Authentication

Sign-in, the session cookie, roles and the administrator account. The decision behind it is
[ADR-003](adr/0003-cookie-authentication-with-identity.md). Added in CP-022; CSRF protection, rate
limiting and the CORS stance ([Security](#security)) in CP-025.

## Endpoints

All under `/api/v1/auth`. Request bodies are JSON and go through the request validation of
[api-conventions.md](api-conventions.md#validation-errors).

| Endpoint | Needs cookie | Success | Errors |
| --- | --- | --- | --- |
| `POST /register` `{ email, password }` | no | **201** with the user, `Location: /api/v1/auth/me`. Does not sign in | 400 missing field or malformed email, **409** email already taken (in any letter case), **422** weak password |
| `POST /login` `{ email, password }` | no | **200** with the user, sets the session cookie | **401** |
| `POST /logout` | yes | **204**, deletes the cookie and invalidates every copy of it | 401 |
| `GET /me` | yes | **200** with the user | 401 |
| `GET /csrf` | no | **200** `{ "token": "..." }`, sets the antiforgery cookie | |

Every `POST` above also needs the `X-XSRF-TOKEN` header (400 without it), and `register` and
`login` answer **429** past the rate limit. Both are described under [Security](#security).

The user in a response:

```json
{ "id": "0193a1c0-...", "email": "ana@example.com", "roles": ["Customer"] }
```

- Registration always gives the `Customer` role. User and role are written in one transaction,
  so an account never exists without a role.
- A weak password is a 422 with the messages under `errors.password`, the same shape as any
  other broken rule.
- Login answers an unknown email, a wrong password and a locked account with the **same** 401
  body, so a caller cannot tell whether an email is registered.

## Roles

| Role | How a user gets it |
| --- | --- |
| `Customer` | `POST /register` |
| `Admin` | Only the seeded administrator (below). There is no public admin registration |

Both roles are inserted by the `SeedIdentityRoles` migration with fixed ids, so they exist in
every migrated database. The names are in `AppRoles` (`CityPark.Application.Common`).

## Session cookie

Configured in `AuthSetup` (`src/CityPark.Api/Auth`).

| Setting | Value | Why |
| --- | --- | --- |
| Name | `citypark.auth` | |
| `HttpOnly` | yes | JavaScript cannot read the session, so an XSS bug cannot steal it |
| `SameSite` | `Strict` | The browser does not send it on requests started by another site |
| `Secure` | always, except in `Development` | The Vite dev server proxies over plain HTTP; everywhere else is HTTPS |
| Lifetime | 8 hours, sliding | Expires after 8 hours without a request. The per-request stamp check (below) reissues it on every request |
| Persistent | no | A session cookie: closing the browser ends it |

- **No redirects.** A request without a valid cookie gets **401**, and a signed-in user without
  the required role gets **403**. Both have a ProblemDetails body. The cookie handler's default
  302 to a login page is turned off, because an API has no login page.
- **Logout really ends the session.** Deleting the browser's cookie is not enough on its own: the
  cookie is self-contained, so a copy of it would stay valid until it expires. Logout therefore
  changes the user's security stamp, and `SecurityStampValidatorOptions.ValidationInterval` is
  zero, so every request compares the cookie's stamp with the database.
  - Cost: one user lookup per authenticated request.
  - Side effect: logout signs the user out on every device.
  - A server-side session store (`ITicketStore`) would end only one session, but needs a table
    and more code. It was not needed for CityPark.

## Passwords and lockout

| Rule | Value |
| --- | --- |
| Minimum length | 8 |
| Must contain | a digit, a lower-case letter, an upper-case letter and a symbol (Identity defaults) |
| Unique email | yes; the email is also the user name |
| Lockout | after **5** failed logins in a row, for **15 minutes**; also for new accounts |

A locked account gets the same 401 as a wrong password. The failure counter resets after a
successful login.

Known limit: Identity computes no password hash for an unknown email, so that answer is slightly
faster. The [rate limit](#rate-limiting) makes measuring this impractical.

## Security

### CSRF protection

The session is a cookie, which the browser attaches to every request to the API, including one
started by another site. Two layers stop such a forged request:

1. `SameSite=Strict` on the session cookie ([Session cookie](#session-cookie)).
2. An antiforgery token in a custom header, for browsers or cases where SameSite does not hold.

The contract:

| Rule | Value |
| --- | --- |
| Get a token | `GET /api/v1/auth/csrf` returns `{ "token": "..." }` and sets the `citypark.csrf` cookie (`HttpOnly`, `SameSite=Strict`, `Secure` outside `Development`) |
| Send it | In the `X-XSRF-TOKEN` header on every `POST`, `PUT`, `PATCH` and `DELETE` under `/api/v1`, together with the cookie |
| Missing or invalid | **400** ProblemDetails, `type` `https://tools.ietf.org/html/rfc9110#section-15.5.1`, the message under `errors.general`. The handler does not run, so nothing is created or changed |
| Safe methods | `GET`, `HEAD`, `OPTIONS` and `TRACE` need no token |
| After login and logout | Get a new token. A token is bound to the user it was issued for, so a token from before login is rejected after it |

- `login` and `register` need the token too. Otherwise another site could sign the browser in to
  the attacker's account (login CSRF).
- **Why 400 and not 403:** 403 already means "signed in, but without the required role". A
  request without the token is malformed, and 400 is also what MVC's antiforgery check returns.
- **Why an endpoint filter:** the built-in antiforgery middleware only marks a request as invalid
  and rejects it when a form is bound. The API reads JSON, so `CsrfProtection` calls
  `IAntiforgery.IsRequestValidAsync` itself. The filter is on the `/api/v1` group, so every
  business endpoint gets it without opting in.
- It runs after authentication and authorization: a request without a cookie still gets 401.

In the SPA:

```ts
const { token } = await fetch("/api/v1/auth/csrf").then(r => r.json());
await fetch("/api/v1/reservations", {
  method: "POST",
  headers: { "Content-Type": "application/json", "X-XSRF-TOKEN": token },
  body: JSON.stringify(reservation),
});
```

### Rate limiting

A fixed-window limiter (`AuthRateLimiting`) guards the credential endpoints against brute force
and credential stuffing. Lockout protects one account; the limit protects against one client
trying many accounts.

| Rule | Value |
| --- | --- |
| Endpoints | `POST /auth/login` and `POST /auth/register`, sharing **one** budget |
| Limit | **10** requests per **1-minute** fixed window, no queue |
| Partition | The client IP address (`HttpContext.Connection.RemoteIpAddress`) |
| Past the limit | **429** ProblemDetails, `type` `https://tools.ietf.org/html/rfc6585#section-4`, with `Retry-After` in seconds |
| Other endpoints | Not limited, for example `GET /api/v1/lots` |

- Every request counts, also a successful or malformed one.
- A fixed window allows a burst at the edge: up to 20 requests within a few seconds across two
  windows. That is acceptable for these endpoints; a sliding window would smooth it.
- **Known limit:** behind a reverse proxy the remote address is the proxy's, and all clients
  would share one budget. Such a deployment must configure `ForwardedHeaders` with the proxy as
  a known proxy. `X-Forwarded-For` must never be trusted without that, because any client can
  send it. A WAF or gateway is out of scope.

### CORS

The SPA and the API are served from the same origin, so CORS is **not** enabled. The API returns
no `Access-Control-Allow-Origin` header, also not to a preflight. A page on another site can
therefore neither read API responses nor send the `X-XSRF-TOKEN` header, which a browser allows
on a cross-origin request only after a successful preflight.

## Administrator seeding

On every start, in every environment, the API creates the administrator from two settings:

| Environment variable | Configuration key |
| --- | --- |
| `Admin__Email` | `Admin:Email` |
| `Admin__Password` | `Admin:Password` |

| Situation | What happens |
| --- | --- |
| Either variable is missing or empty | Seeding is skipped and logged. The database is not touched |
| No user with that email | The user is created with the `Admin` role, in one transaction |
| The user already exists | Gets the `Admin` role if it lacks it. **The password is not changed**, so a later change of `Admin__Password` does not rotate it |
| The password breaks the rules above | The start fails with the Identity error codes, for example `PasswordRequiresUpper` |

- The password never appears in a log or an exception message. A test captures every log entry
  of the host and checks this.
- Set the variables in the environment or a secret store, never in `appsettings*.json`.

Locally, in PowerShell, before `dotnet run`:

```powershell
$env:Admin__Email = "admin@citypark.local"
$env:Admin__Password = "<a strong password>"
dotnet run --project src/CityPark.Api
```

## Tests

`tests/CityPark.Api.Tests/Auth`, against a migrated PostgreSQL 17 container (`PostgresApiFactory`):

- `AuthenticationEndpointsTests`: register, login, lockout, `me` and logout, including a replayed
  copy of the cookie after logout.
- `CookieAuthenticationTests`: 401 and 403 instead of redirects, and the cookie flags.
- `AdminSeedingTests`: seeding with and without the variables, a second start, a weak password,
  and that the password is never logged.
- `CsrfProtectionTests`: the token endpoint and its cookie, a reservation without the header (400,
  nothing created) and with it (201), a token from before login, and login without the header.
- `RateLimitingTests`: the 11th login and register attempt (429 with `Retry-After`), a second
  client that is not limited, and `GET /api/v1/lots` after the budget is used up.
- `CorsTests`: no `Access-Control-Allow-Origin` on a request or a preflight from another origin.

`AuthClient` creates cookie-aware clients with an `https://localhost` base address, because the
client only keeps a `Secure` cookie for HTTPS requests. It also:

- adds `CsrfHandler`, which does what the SPA does: it fetches a token before every unsafe
  request and sends the header. A request with the `CsrfHandler.Skip` option is sent as built.
- gives each client its own address in the `X-Test-Client-Address` header. TestServer has no
  remote address, so without this all tests would share one rate-limit budget. Only the
  test-only `TestClientAddress` middleware reads the header; the API never trusts it.
