# CityPark

Modular monolith built on .NET 10.

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

## Build

```bash
dotnet build
```

Warnings are treated as errors (see `Directory.Build.props`), so the build must be warning-free.

## Test

```bash
dotnet test
```
