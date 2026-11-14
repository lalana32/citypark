# CityPark

[![CI](https://github.com/lalana32/citypark/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/lalana32/citypark/actions/workflows/ci.yml)

Modular monolith built on .NET 10.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the branch, commit and pull request workflow.

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
