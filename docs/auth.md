# Authentication

Sign-in, the session cookie, roles and the administrator account. The decision behind it is
[ADR-003](adr/0003-cookie-authentication-with-identity.md). Added in CP-022.

## Endpoints

All under `/api/v1/auth`. Request bodies are JSON and go through the request validation of
[api-conventions.md](api-conventions.md#validation-errors).

| Endpoint | Needs cookie | Success | Errors |
| --- | --- | --- | --- |
| `POST /register` `{ email, password }` | no | **201** with the user, `Location: /api/v1/auth/me`. Does not sign in | 400 missing field or malformed email, **409** email already taken (in any letter case), **422** weak password |
| `POST /login` `{ email, password }` | no | **200** with the user, sets the session cookie | **401** |
| `POST /logout` | yes | **204**, deletes the cookie and invalidates every copy of it | 401 |
| `GET /me` | yes | **200** with the user | 401 |

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
faster. Rate limiting (next card) makes measuring this impractical.

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

`AuthClient` creates cookie-aware clients with an `https://localhost` base address, because the
client only keeps a `Secure` cookie for HTTPS requests.
