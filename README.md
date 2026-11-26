# CityPark

[![CI](https://github.com/lalana32/citypark/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/lalana32/citypark/actions/workflows/ci.yml)

CityPark reserves parking spaces in the lots of one city. A driver picks a lot and a time
interval, sees the free spaces and the price, and reserves a space. An administrator maintains
the lots and spaces and can see and cancel every reservation.

The central requirement is that **one parking space is never reserved twice for overlapping
times, even when two requests arrive at the same moment**. PostgreSQL enforces it with an
exclusion constraint, and the API answers the losing request with `409 Conflict`
([how](#how-double-booking-is-prevented)).

| Part | Technology |
| --- | --- |
| API | .NET 10, ASP.NET Core minimal APIs, EF Core 10, ASP.NET Core Identity (cookie session) |
| Database | PostgreSQL 17 |
| Web app | React, TypeScript (strict), Vite, TanStack Query; types generated from OpenAPI |
| Tests | xUnit v3, Testcontainers (real PostgreSQL), `WebApplicationFactory`, NetArchTest, Vitest |
| Delivery | Docker Compose, GitHub Actions |

## Contents

- [Quick start](#quick-start)
- [Architecture](#architecture)
- [Business rules](#business-rules)
- [How double booking is prevented](#how-double-booking-is-prevented)
- [Running the tests](#running-the-tests)
- [Local development](#local-development)
- [Documentation](#documentation)
- [Known limitations](#known-limitations)
- [Next steps](#next-steps)

## Quick start

Only [Git](https://git-scm.com/) and Docker with Compose v2 are needed; no .NET SDK or Node.js.

```bash
git clone https://github.com/lalana32/citypark.git
cd citypark
docker compose --profile app up --build -d --wait
```

The first build takes a few minutes. `--wait` returns once every service is healthy. Then open
<http://localhost:8080>.

| What to try | Where |
| --- | --- |
| Browse the three demo lots, pick a time, see free spaces and the price | **Parking lots** |
| Register, log in and reserve a space | **Register**, **Log in**, then a lot |
| See and cancel your reservations | **My reservations** |
| Manage lots and spaces, see and cancel every reservation | Sign in as the administrator below, then **Admin** |

| Administrator | |
| --- | --- |
| Email | `admin@citypark.test` |
| Password | `Admin-Pass-2026` |

These are local placeholders from `docker-compose.yml`; override them in `.env`
([Configuration](#configuration)). If port 8080 is taken, set `APP_PORT`, for example
`APP_PORT=8081`.

The OpenAPI document is at <http://localhost:8080/openapi/v1.json>. Check that the API and the
database are up:

```bash
curl -i http://localhost:8080/health/ready
```

Stop the stack, and add `-v` to remove the database as well:

```bash
docker compose --profile app down
```

More about the stack: [Run the whole stack](#run-the-whole-stack).

## Architecture

A layered monolith in four .NET projects ([ADR-001](docs/adr/0001-monolith-with-four-projects.md)),
served together with the web app behind one origin:

```mermaid
flowchart LR
    browser["Browser<br/>React app"]
    subgraph compose["docker compose --profile app"]
        web["web<br/>nginx: static app,<br/>proxies /api, /health, /openapi"]
        subgraph api["api (ASP.NET Core)"]
            direction TB
            A["CityPark.Api<br/>endpoints, auth, errors"]
            B["CityPark.Application<br/>use cases"]
            C["CityPark.Domain<br/>rules R1–R14"]
            D["CityPark.Infrastructure<br/>EF Core, Identity"]
            A --> B --> C
            A --> D
            D --> B
        end
        migrate["migrate<br/>one-shot migration bundle"]
        db[("PostgreSQL 17<br/>exclusion constraint")]
    end
    browser -->|"HTTP, session cookie"| web
    web --> A
    D --> db
    migrate --> db
```

- **Domain** holds the entities and the booking rules and depends on nothing.
- **Application** holds the use cases (`CreateReservationService`, `AvailabilityService`, ...)
  and depends only on Domain.
- **Infrastructure** implements persistence with EF Core and Npgsql, including the PostgreSQL
  error mapping.
- **Api** maps HTTP to the use cases and turns every exception into a ProblemDetails response in
  one place.
- The browser talks only to nginx, so the API and its cookies are same-origin and CORS stays off.

The layer rules are checked by `tests/CityPark.Architecture.Tests`. Details, including a request
walked through every layer, are in [docs/architecture.md](docs/architecture.md).

## Business rules

The full rules with examples are in the [product brief](docs/product-brief.md#6-business-rules).
Times are evaluated in Europe/Sarajevo and stored in UTC
([ADR-004](docs/adr/0004-utc-storage-and-sarajevo-business-day.md)). An interval is half-open,
`[start, end)`, so 10:00–11:00 and 11:00–12:00 do not overlap.

| # | Rule | Enforced in | Status |
| --- | --- | --- | --- |
| R1 | Start and end on a 15-minute grid | Domain | Implemented |
| R2 | At least 30 minutes | Domain | Implemented |
| R3 | One day, inside the lot's opening hours | Domain | Implemented |
| R4 | Start at most 30 days ahead | Domain | Implemented |
| R5 | Start at most 5 minutes in the past | Domain | Implemented |
| R6 | No two overlapping Active reservations of one space | **Database** | Implemented |
| R7 | No two overlapping Active reservations of one plate | – | **Not implemented** ([limitations](#known-limitations)) |
| R8 | Every started hour is billed in full; price stored at creation | Domain | Implemented |
| R9 | Customer cancels until the start, admin at any time; twice is a conflict | Domain, database (`xmin`) | Implemented |
| R10 | At most 10 Active future reservations per user | – | **Not implemented** |
| R11 | Inactive spaces get no new reservations | Application | Implemented for spaces; lots cannot be deactivated |
| R12 | A space with Active reservations that have not ended cannot be deactivated | Application | Implemented for spaces |
| R13 | A new rate or new opening hours apply only to new reservations | Domain | Implemented |
| R14 | Bosnia and Herzegovina plate format, normalized | Domain | Implemented |

A broken rule is a **422** with the field in `errors`, a conflict with the current state is a
**409**, and a malformed request is a **400**
([error contract](docs/api-conventions.md#error-contract)).

## How double booking is prevented

**The problem.** "Check whether the space is free, then insert" is a race condition: two
requests can both run the check before either inserts, both see a free space, and both insert.
No check in application code can close that gap.

**The constraint.** The database decides instead. The migration `AddReservationOverlapConstraint`
adds a PostgreSQL exclusion constraint
([ADR-002](docs/adr/0002-overlap-prevention-with-exclusion-constraint.md)):

```sql
ALTER TABLE reservations
ADD CONSTRAINT ex_reservations_space_no_overlap
EXCLUDE USING gist (
    space_id WITH =,
    tstzrange(start_time, end_time, '[)') WITH &&
) WHERE (status = 1);  -- 1 = Active
```

- `tstzrange(..., '[)')` is the half-open interval, and `&&` means "overlaps".
- `btree_gist` lets the GiST index compare `space_id` with `=`.
- `WHERE (status = 1)` makes it partial: a cancelled reservation blocks nothing.

The service does not query for overlaps before the insert. Under the default READ COMMITTED
isolation, a second overlapping insert waits for the first transaction and then fails.

**From SQLSTATE to HTTP 409.**

```mermaid
sequenceDiagram
    participant C as Client
    participant A as Api endpoint
    participant S as CreateReservationService
    participant DB as PostgreSQL
    C->>A: POST /api/v1/reservations
    A->>S: CreateAsync / CreateInLotAsync
    S->>DB: INSERT reservation
    DB-->>S: 23P01 exclusion_violation on ex_reservations_space_no_overlap
    S->>S: IDatabaseErrors.IsSpaceOverlapViolation → ReservationConflictException
    S-->>A: DomainConflictException
    A->>A: ApiExceptionHandler maps DomainConflictException → 409
    A-->>C: 409 application/problem+json
```

1. The insert fails with SQLSTATE **`23P01`** (`exclusion_violation`). EF Core wraps it in a
   `DbUpdateException`.
2. `PostgresDatabaseErrors.IsSpaceOverlapViolation` (Infrastructure) matches the SQLSTATE **and**
   the constraint name `ex_reservations_space_no_overlap`, never the message text. Application
   sees only the `IDatabaseErrors` interface, so it does not depend on Npgsql.
3. **A given space** (`spaceId` in the body): `CreateReservationService.CreateAsync` throws
   `ReservationConflictException`. Any other database error is rethrown unchanged.
4. **A lot** (`lotId` in the body): `CreateInLotAsync` treats the free spaces as candidates only.
   On `23P01` it detaches the failed reservation and tries the next candidate, up to 10 inserts.
   When none is left it throws `NoFreeSpaceException`.
5. Both exceptions derive from `DomainConflictException`, which `ApiExceptionHandler` maps to
   **409 Conflict** with a ProblemDetails body:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The request conflicts with the current state.",
  "status": 409,
  "detail": "Space 019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02 already has an active reservation that overlaps 2026-12-01T09:00:00.0000000+00:00 – 2026-12-01T10:30:00.0000000+00:00.",
  "instance": "/api/v1/reservations",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

**Deadlocks.** Two parallel inserts can also deadlock while PostgreSQL checks the constraint
(`40P01`). `EnableRetryOnFailure()` retries the insert, which then succeeds or fails with a real
`23P01`. A deadlock is not mapped to 409, because the aborted request may not overlap any
reservation that is finally committed.

**Proof.** The guarantee is tested with real parallel requests against PostgreSQL 17:

| Test | What it shows |
| --- | --- |
| `ReservationOverlapConstraintTests` | The constraint rejects an overlap and allows touching intervals, cancelled rows and other spaces |
| `ParallelReservationTests` | 20 simultaneous requests for one space: exactly 1 succeeds, 19 conflict, in 20 rounds; also staggered, many-space and by-lot scenarios |
| `ParallelReservationHttpTests` | Two signed-in customers over HTTP: exactly one `201` and one `409` per round |

The scenarios are described in [docs/testing.md](docs/testing.md#concurrency-tests).
Cancellation uses optimistic concurrency on `xmin`, so two simultaneous cancels give one success
and one 409 ([ADR-005](docs/adr/0005-optimistic-concurrency-on-xmin-for-cancellation.md)).

## Running the tests

### Backend

Needs the .NET SDK (see [Prerequisites](#prerequisites)) and a **running Docker engine**: the
integration tests start disposable `postgres:17` containers with Testcontainers. From the
repository root:

```bash
dotnet test
```

| Project | What it covers | Docker |
| --- | --- | --- |
| `CityPark.Domain.Tests` | Booking rules, value objects, state transitions | no |
| `CityPark.Application.Tests` | Paging and filter rules | no |
| `CityPark.Architecture.Tests` | Layer dependencies | no |
| `CityPark.Infrastructure.Tests` | Services, migrations, the exclusion constraint, concurrency | yes |
| `CityPark.Api.Tests` | HTTP contract, auth, CSRF, error mapping, OpenAPI snapshot | yes |

One project at a time, for example the fast ones without Docker:

```bash
dotnet test --project tests/CityPark.Domain.Tests
```

If Docker runs somewhere the tests cannot find it by default (for example Docker Engine inside
WSL with the SDK on Windows), expose the engine and set `DOCKER_HOST`, for example
`DOCKER_HOST=tcp://localhost:2375`.

### Frontend

Needs Node.js 22. From `web/`:

```bash
npm ci
npm run lint
npm run typecheck
npm test
npm run build
```

### Smoke test of the whole stack

With the stack from [Quick start](#quick-start) running, from a Bash shell (Git Bash on Windows):

```bash
bash scripts/smoke.sh
```

### CI

`.github/workflows/ci.yml` runs on every pull request and on `main`:

| Job | Steps |
| --- | --- |
| Build and test | `dotnet build` and `dotnet test` in Release; warnings are errors |
| Frontend | `npm ci`, generated API types up to date, lint, type-check, tests, build |
| Docker smoke test | `docker compose --profile app up --build -d --wait`, then `scripts/smoke.sh` |

## Local development

### Prerequisites

- .NET SDK 10.0.404 or a later 10.0.4xx patch (pinned in `global.json`)
- Docker with Compose v2 (for the local database and the [whole stack](#run-the-whole-stack))
- Node.js 22 (pinned in `web/.nvmrc`) for the [frontend](#frontend)

See [CONTRIBUTING.md](CONTRIBUTING.md) for the branch, commit and pull request workflow.

### Run the API and the web app from source

1. Start the database: `docker compose up -d --wait` ([Local database](#local-database)).
2. Restore the tools and apply the migrations once:

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/CityPark.Infrastructure --startup-project src/CityPark.Infrastructure
   ```

3. Start the API; it seeds the [demo data](#demo-data) and opens the Scalar UI at
   <http://localhost:5114/scalar>:

   ```bash
   dotnet run --project src/CityPark.Api --launch-profile http
   ```

4. In a second terminal, start the web app on <http://localhost:5173>:

   ```bash
   cd web
   npm ci
   npm run dev
   ```

No administrator is seeded this way. To have one, set `Admin__Email` and `Admin__Password`
before step 3 ([docs/auth.md](docs/auth.md#administrator-seeding)).

### Build

```bash
dotnet build
```

Warnings are treated as errors (see `Directory.Build.props`), so the build must be warning-free.

### Run the whole stack

```bash
docker compose --profile app up --build -d --wait
```

| Service | Image | Role |
| --- | --- | --- |
| `postgres` | `postgres:17` | Database, also started without the profile |
| `migrate` | `src/CityPark.Api/Dockerfile`, target `migrator` | One-shot job: an EF Core migration bundle applies the pending migrations and exits |
| `api` | same Dockerfile, target `api` | The API in `Production`; starts only after `migrate` succeeded; no published port |
| `web` | `web/Dockerfile` | nginx with the built app; proxies `/api`, `/health` and `/openapi` to `api` |

Each service has a health check, and `--wait` returns once all are healthy.

#### Differences from a real deployment

The images are built as for production, but the stack is meant for a local review over plain
HTTP. The API runs in `Production` with two switches that a real deployment never sets:

| Variable | Why |
| --- | --- |
| `Cookies__AllowHttp=true` | There is no TLS on localhost, so the session and antiforgery cookies cannot be `Secure`. `HttpOnly` and `SameSite=Strict` stay on ([docs/auth.md](docs/auth.md#secure-flag-on-the-local-http-stack)) |
| `DemoData__Enabled=true` | Seeds the demo lots, which otherwise happens only in `Development` |

No secret is in an image: `.dockerignore` keeps `.env` out of the build context, and passwords
are passed as environment variables when the containers start.

The smoke test (`bash scripts/smoke.sh`) checks `/health/ready`, then registers a new user, logs
in, checks that the session cookie is sent back, and creates a reservation.

### Frontend

A React app in `web/` (Vite, TypeScript strict, TanStack Query, react-hook-form). Run the
commands from `web/`:

| Command | What it does |
| --- | --- |
| `npm ci` | Installs the exact versions from `package-lock.json` |
| `npm run dev` | Starts the dev server on `http://localhost:5173`; `/api` is proxied to the API on `http://localhost:5114` |
| `npm run generate` | Generates `src/api/schema.d.ts` from `openapi/v1.json` |
| `npm run lint` | ESLint |
| `npm run typecheck` | `tsc -b`, type errors only, no output |
| `npm test` | Vitest component tests in jsdom |
| `npm run build` | Type-checks and builds into `dist/` |

The browser talks only to the dev server, so the session and antiforgery cookies are
same-origin and CORS stays off ([docs/auth.md](docs/auth.md#cors)).

#### API types

The types come from a committed copy of the OpenAPI document, `web/openapi/v1.json`. CI fails
when either step is out of date:

1. `OpenApiSnapshotTests` (in `dotnet test`) compares the API's `/openapi/v1.json` with the file.
2. The Frontend job runs `npm run generate` and fails if `src/api/schema.d.ts` changes.

After changing an endpoint, update both, from the repository root:

```bash
UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --project tests/CityPark.Api.Tests/CityPark.Api.Tests.csproj
cd web
npm run generate
```

In PowerShell, set the variable with `$env:UPDATE_OPENAPI_SNAPSHOT = "1"` first and remove it
afterwards.

#### API client

`src/api/client.ts` is a typed client (`openapi-fetch`). Its fetch wrapper (`src/api/http.ts`)
gets a token from `GET /api/v1/auth/csrf` before every `POST`, `PUT`, `PATCH` and `DELETE` and
sends it in the `X-XSRF-TOKEN` header. The token is fetched for each request and never stored,
neither in `localStorage` nor in `sessionStorage`.

### Local database

PostgreSQL 17 runs in Docker, defined in `docker-compose.yml`.

#### Configuration

All settings have local-development defaults, so no setup is required. To override them,
copy `.env.example` to `.env` (git-ignored) and edit the values:

```bash
cp .env.example .env
```

| Variable            | Default    | Purpose                    |
| ------------------- | ---------- | -------------------------- |
| `POSTGRES_DB`       | `citypark` | Database created on start  |
| `POSTGRES_USER`     | `citypark` | Superuser name             |
| `POSTGRES_PASSWORD` | `citypark` | Superuser password         |
| `POSTGRES_PORT`     | `5432`     | Host port (localhost only) |
| `APP_PORT`          | `8080`     | Host port of the [whole stack](#run-the-whole-stack) |
| `ADMIN_EMAIL`       | `admin@citypark.test` | Administrator of the whole stack |
| `ADMIN_PASSWORD`    | `Admin-Pass-2026`     | Its password |

#### Start

```bash
docker compose up -d --wait
```

`--wait` returns once the healthcheck (`pg_isready`) reports the database as healthy.
Check the status with `docker compose ps`.

#### Connect

With the default settings:

- .NET (Npgsql): `Host=localhost;Port=5432;Database=citypark;Username=citypark;Password=citypark`
- URI (psql and other tools): `postgresql://citypark:citypark@localhost:5432/citypark`

Without a local `psql` install, use the one inside the container:

```bash
docker compose exec postgres psql -U citypark -d citypark
```

#### Stop

```bash
docker compose down
```

The data is kept in the `citypark_postgres-data` volume and is available on the next start.

#### Reset

```bash
docker compose down -v
```

`-v` also removes the volume, so the next start creates an empty database.

### Database migrations

The schema is created by EF Core migrations in `src/CityPark.Infrastructure/Persistence/Migrations`.
`dotnet-ef` is a local tool pinned in `dotnet-tools.json`; restore it once after cloning:

```bash
dotnet tool restore
```

#### Apply

With the local database running (see [Start](#start)):

```bash
dotnet ef database update --project src/CityPark.Infrastructure --startup-project src/CityPark.Infrastructure
```

The command applies every migration that is not yet in the `__EFMigrationsHistory` table, so it
is safe to run again. By default it connects to the Compose database with the settings from
[Connect](#connect). To target another database, set `CITYPARK_DB` to an Npgsql connection string
first, for example in PowerShell:

```powershell
$env:CITYPARK_DB = "Host=localhost;Port=5433;Database=citypark;Username=citypark;Password=citypark"
```

Check the result with `\dt` in `psql` (see [Connect](#connect)).

#### Add a migration

After changing an entity configuration:

```bash
dotnet ef migrations add <Name> --project src/CityPark.Infrastructure --startup-project src/CityPark.Infrastructure --output-dir Persistence/Migrations
```

Read the generated migration before committing it. Exclusion constraints are not modelled by
EF Core and are added to a migration as raw SQL (see
[ADR-002](docs/adr/0002-overlap-prevention-with-exclusion-constraint.md)).

### Demo data

When the API starts in the `Development` environment, it seeds three demo lots so a fresh local
database has something to show. Both launch profiles in `launchSettings.json` set `Development`.

| Lot          | Address          | Rate per hour | Opening hours | Spaces          |
| ------------ | ---------------- | ------------- | ------------- | --------------- |
| Centar       | Ferhadija 1      | 2.50 BAM      | 07:00–22:00   | 30 (A-01…A-30)  |
| Marijin Dvor | Zmaja od Bosne 8 | 2.00 BAM      | 06:00–23:00   | 20 (A-01…A-20)  |
| Baščaršija   | Bravadžiluk 2    | 3.00 BAM      | 08:00–20:00   | 12 (A-01…A-12)  |

- The seeder runs on every start and adds only the lots whose name is not yet in the database,
  so restarting the API does not duplicate them.
- In any other environment (`Production`, `Staging`, ...) it does nothing, unless
  `DemoData__Enabled=true` is set, as the Compose app profile does.
- No demo users are seeded. The admin account comes from environment variables, see
  [docs/auth.md](docs/auth.md#administrator-seeding).

Check the result in `psql` (see [Connect](#connect)):

```sql
SELECT l.name, l.hourly_price, l.opens_at, l.closes_at, count(s.id) AS spaces
FROM parking_lots l JOIN parking_spaces s ON s.lot_id = l.id
GROUP BY l.id ORDER BY l.name;
```

To start over with only the demo data, [reset](#reset) the database, apply the migrations and
run the API again.

## Documentation

| Document | Content |
| --- | --- |
| [docs/product-brief.md](docs/product-brief.md) | Actors, journeys, scope, business rules R1–R14, data model, invariants |
| [docs/architecture.md](docs/architecture.md) | Layers, request flow, where each concern lives, architecture tests |
| [docs/api.md](docs/api.md) | Every endpoint, its statuses and examples; links to the OpenAPI document |
| [docs/api-conventions.md](docs/api-conventions.md) | Routes and versioning, the error contract, health checks, OpenAPI |
| [docs/auth.md](docs/auth.md) | Sign-in, session cookie, roles, CSRF, rate limiting, admin seeding |
| [docs/testing.md](docs/testing.md) | Concurrency tests, authorization matrix, error contract tests |
| [docs/performance.md](docs/performance.md) | Query plans of availability and the overlap check |
| [docs/adr/](docs/adr/README.md) | Architecture decisions and the alternatives rejected |
| [docs/decisions.md](docs/decisions.md) | Smaller tooling decisions |
| [AI_USAGE.md](AI_USAGE.md) | Where AI assistance was used and how its output was verified |

## Known limitations

What the project does **not** do, or does only partly. Each item is a known gap, not a hidden
one.

**Business rules from the product brief that are not implemented**

- **R7, no overlap per plate.** The same plate can get two overlapping reservations in different
  spaces or lots. Only the space constraint (R6) exists; the brief and ADR-002 describe a second
  exclusion constraint on `plate`, which was never added.
- **R10, at most 10 Active future reservations per user.** There is no limit per user.
- **Deactivating a lot.** Only spaces can be deactivated (R11, R12). A lot has no active flag, so
  a whole lot is taken out of service by deactivating its spaces. A space cannot be reactivated
  through the API.

**Behaviour to know about**

- **First-fit space assignment.** A reservation by lot takes the first free space by label. This
  can fragment capacity: a later request may be rejected although another assignment would have
  fitted it.
- **At most 10 candidates per request by lot.** Under heavy load on a lot with more than 10 free
  spaces, a request can lose all 10 races and get 409 while a free space still exists. Sending
  it again works.
- **Deactivation is not serialized with new reservations.** A reservation created in the
  milliseconds between the deactivation check and the save can land on a space that becomes
  inactive ([docs/api.md](docs/api.md#admin-lots-and-spaces)).
- **Creating a reservation is not idempotent.** If the `201` response is lost and the client
  sends the same request again, it gets 409 against its own reservation.
- **Times without an offset** are parsed in the server's time zone. Clients must send ISO 8601
  instants with an offset ([docs/api.md](docs/api.md#availability)).
- **Rate limiting in the Docker stack is shared by all clients.** The API sees nginx as the
  client address, and forwarded headers are not configured, so all clients share one budget of
  10 sign-in and registration requests per minute ([docs/auth.md](docs/auth.md#rate-limiting)).
- **Registration reveals that an email is taken** (409), slowed down by the rate limit
  ([D5](docs/decisions.md#d5-register-reveals-that-an-email-is-taken)).
- **Lockout can be triggered on purpose.** Five wrong passwords lock an account, the
  administrator's included, for 15 minutes.
- **Availability reads the whole reservations table** instead of the constraint's index: 20 ms at
  50,000 rows, growing linearly with history ([docs/performance.md](docs/performance.md)).
- **Offset pagination.** Pages shift when reservations are added between two page requests.
- **`parking_spaces.lot_id` is nullable** in the schema. No code path creates a space without a
  lot, but the database does not forbid it.

**Out of scope by design** ([product brief](docs/product-brief.md#5-functional-scope)):
payments, email or push notifications, email verification, check-in and no-show handling, guest
reservations, overnight and multi-day reservations, foreign plates, several cities or time zones,
and a translated UI ([D6](docs/decisions.md#d6-language-of-the-user-interface)).

**Not production-ready as shipped.** The Docker stack serves plain HTTP with `Cookies__AllowHttp`,
seeds demo data, and has no TLS, backups, monitoring or log shipping.

## Next steps

In order of value:

1. **R7:** add `ex_reservations_plate_no_overlap`, map its `23P01` to its own 409 by the
   constraint name, stop the candidate loop on it, and test it in parallel like R6.
2. **R10:** count the user's Active future reservations in `CreateReservationService`, documented
   as a soft rule that two simultaneous requests can exceed by one.
3. **Lot deactivation:** an active flag on the lot with the same R11 and R12 checks as a space.
4. **Schema hardening:** `lot_id NOT NULL` and `CHECK` constraints such as `end_time > start_time`.
5. **Forwarded headers** behind nginx with the proxy as a known proxy, so the rate limit is per
   client again.
6. **Availability with the range operator,** so the query uses the existing GiST index.
7. **Idempotency key** on reservation creation.
8. **i18n** for local users ([D6](docs/decisions.md#d6-language-of-the-user-interface)).
