# ADR-003: Cookie authentication with ASP.NET Core Identity

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

CityPark needs registration, sign in, sign out and the current user for customers, and an
administrator role (see the permission matrix in the [product brief](../product-brief.md)).
The only client in scope is a React web application served to the browser. There are no mobile
apps and no third-party API consumers. The administrator account is created from configuration
at first start; there is no public admin registration.

Two questions need an answer: **how users and passwords are stored** and **how the browser
proves who it is** on every request.

## Decision

- **User store:** ASP.NET Core Identity with EF Core, in the same PostgreSQL database. The user
  key is a `Guid`. CityPark does not keep its own user table; reservations reference the
  Identity user. Administrators have the `Admin` role.
- **Session:** the Identity **application cookie**. After sign in the browser holds an
  encrypted, `HttpOnly`, `Secure`, `SameSite=Strict` cookie and sends it automatically.
- **Same origin:** the React app and the API are served from one origin (a reverse proxy in
  production, the Vite dev-server proxy in development), so the cookie is a first-party cookie.
- **API behaviour:** unauthenticated and forbidden requests return `401` and `403`. The default
  cookie behaviour of redirecting to a login page is turned off for API endpoints.

## Alternatives

### JWT bearer tokens stored by the frontend

**Rejected because** a token kept in `localStorage` can be read by any script on the page, so a
single XSS bug leaks the session. Signing out does not invalidate an issued token, and short
token lifetimes require a refresh-token flow. All of that solves problems (other clients, other
domains) that CityPark does not have.

### Identity API bearer tokens (`MapIdentityApi` in token mode)

**Rejected because** it has the same storage problem in the browser as JWT, only with opaque
tokens.

### External identity provider (Keycloak, Entra ID, Auth0)

**Rejected because** it adds another service to run and configure, plus an OAuth/OIDC flow,
for a single application with local accounts only.

### Custom user table and password hashing

**Rejected because** password hashing, lockout and security stamps are easy to get wrong.
Identity provides tested implementations.

## Consequences

### Positive

- JavaScript cannot read the session cookie (`HttpOnly`), which limits the damage of an XSS bug.
- Sign out and a security-stamp change invalidate the session on the server side.
- Registration, password hashing, lockout and roles come from a maintained framework.

### Negative and trade-offs

- Cookies are sent automatically, so cross-site request forgery has to be addressed.
  `SameSite=Strict` stops the browser from sending the cookie on cross-site requests, and the API
  accepts only `application/json` bodies, which a plain HTML form cannot send.
- The frontend and the API must share one origin. Hosting them on different domains would need
  CORS with credentials and `SameSite=None`, which weakens the CSRF protection.
- A future mobile app or third-party client would need token authentication added in a new ADR.
- Identity brings its own tables (`AspNetUsers`, `AspNetRoles`, ...) into the schema and
  migrations.
