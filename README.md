# CityPark

[![CI](https://github.com/lalana32/citypark/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/lalana32/citypark/actions/workflows/ci.yml)

Layered monolith built on .NET 10 (see [ADR-001](docs/adr/0001-monolith-with-four-projects.md)).

See [CONTRIBUTING.md](CONTRIBUTING.md) for the branch, commit and pull request workflow.
Architecture decisions are recorded in the [ADR index](docs/adr/README.md).
The contract of the use cases, including pagination, is in [docs/api.md](docs/api.md).
Routes, the error contract, health checks and OpenAPI are described in [docs/api-conventions.md](docs/api-conventions.md).

## Solution layout

| Project                   | References                       |
| ------------------------- | -------------------------------- |
| `CityPark.Domain`         | none                             |
| `CityPark.Application`    | Domain                           |
| `CityPark.Infrastructure` | Application, Domain              |
| `CityPark.Api`            | Application, Infrastructure      |

Every project under `src/` has a matching test project under `tests/`.

## Prerequisites

- .NET SDK 10.0.404 or a later 10.0.4xx patch (pinned in `global.json`)
- Docker with Compose v2 (for the local database)

## Build

```bash
dotnet build
```

Warnings are treated as errors (see `Directory.Build.props`), so the build must be warning-free.

## Test

```bash
dotnet test
```

## Local database

PostgreSQL 17 runs in Docker, defined in `docker-compose.yml`.

### Configuration

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

### Start

```bash
docker compose up -d --wait
```

`--wait` returns once the healthcheck (`pg_isready`) reports the database as healthy.
Check the status with `docker compose ps`.

### Connect

With the default settings:

- .NET (Npgsql): `Host=localhost;Port=5432;Database=citypark;Username=citypark;Password=citypark`
- URI (psql and other tools): `postgresql://citypark:citypark@localhost:5432/citypark`

Without a local `psql` install, use the one inside the container:

```bash
docker compose exec postgres psql -U citypark -d citypark
```

### Stop

```bash
docker compose down
```

The data is kept in the `citypark_postgres-data` volume and is available on the next start.

### Reset

```bash
docker compose down -v
```

`-v` also removes the volume, so the next start creates an empty database.

## Database migrations

The schema is created by EF Core migrations in `src/CityPark.Infrastructure/Persistence/Migrations`.
`dotnet-ef` is a local tool pinned in `dotnet-tools.json`; restore it once after cloning:

```bash
dotnet tool restore
```

### Apply

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

### Add a migration

After changing an entity configuration:

```bash
dotnet ef migrations add <Name> --project src/CityPark.Infrastructure --startup-project src/CityPark.Infrastructure --output-dir Persistence/Migrations
```

Read the generated migration before committing it. Exclusion constraints are not modelled by
EF Core and are added to a migration as raw SQL (see
[ADR-002](docs/adr/0002-overlap-prevention-with-exclusion-constraint.md)).

## Demo data

When the API starts in the `Development` environment, it seeds three demo lots so a fresh local
database has something to show. Both launch profiles in `launchSettings.json` set `Development`,
so with a migrated database (see [Apply](#apply)) this is enough:

```bash
dotnet run --project src/CityPark.Api
```

| Lot          | Address          | Rate per hour | Opening hours | Spaces          |
| ------------ | ---------------- | ------------- | ------------- | --------------- |
| Centar       | Ferhadija 1      | 2.50 BAM      | 07:00–22:00   | 30 (A-01…A-30)  |
| Marijin Dvor | Zmaja od Bosne 8 | 2.00 BAM      | 06:00–23:00   | 20 (A-01…A-20)  |
| Baščaršija   | Bravadžiluk 2    | 3.00 BAM      | 08:00–20:00   | 12 (A-01…A-12)  |

- The seeder runs on every start and adds only the lots whose name is not yet in the database,
  so restarting the API does not duplicate them.
- In any other environment (`Production`, `Staging`, ...) it does nothing.
- No users are seeded; accounts come with authentication.

Check the result in `psql` (see [Connect](#connect)):

```sql
SELECT l.name, l.hourly_price, l.opens_at, l.closes_at, count(s.id) AS spaces
FROM parking_lots l JOIN parking_spaces s ON s.lot_id = l.id
GROUP BY l.id ORDER BY l.name;
```

To start over with only the demo data, [reset](#reset) the database, apply the migrations and
run the API again.
