# Architecture

CityPark is a layered monolith with four projects
([ADR-001](adr/0001-monolith-with-four-projects.md)). Dependencies point inwards only:

```text
CityPark.Api            → CityPark.Application, CityPark.Infrastructure
CityPark.Infrastructure → CityPark.Application, CityPark.Domain
CityPark.Application    → CityPark.Domain
CityPark.Domain         → nothing
```

## Layer rules

| Rule | Enforced by |
| --- | --- |
| Domain depends on no other layer | Compiler and architecture tests |
| Application does not depend on Infrastructure or Api | Compiler and architecture tests |
| Domain does not use EF Core (any package), Npgsql or ASP.NET Core | Architecture tests |
| Application does not use an EF Core provider (Npgsql) or ASP.NET Core | Architecture tests |

Application may use core EF Core (`Microsoft.EntityFrameworkCore`) for `DbSet<T>` and LINQ in
`IAppDbContext`. This trade-off is recorded in ADR-001.

### Why the compiler alone is not enough

The project references already form a one-way graph. A reference from Domain or Application to
an outer layer creates a cycle, and the restore fails with `MSB4006: There is a circular
dependency`. NuGet packages do not create cycles, so a project can add `Npgsql` or ASP.NET Core
without any build error. The architecture tests catch that case.

## Architecture tests

`tests/CityPark.Architecture.Tests` uses [NetArchTest](https://github.com/BenMorris/NetArchTest)
and runs with `dotnet test`, so CI runs it as well. The tests check two things:

- **Namespaces of the types each layer uses** (NetArchTest). For example, no type in Domain may
  use a type from `Microsoft.EntityFrameworkCore`.
- **Assemblies each layer references.** Provider extension methods such as `UseNpgsql` are
  declared in the `Microsoft.EntityFrameworkCore` namespace, so the namespace rule does not see
  them. The compiled assembly still references `Npgsql.EntityFrameworkCore.PostgreSQL`.

Both checks look at the compiled assembly. A package that is added but never used leaves no
trace in it, so it is not reported. Code review covers that case.

### Checking that the tests work

During CP-038 the rules were broken on purpose and then the change was reverted:

| Change | Result |
| --- | --- |
| `ProjectReference` from Domain to Infrastructure | Restore fails with `MSB4006` (circular dependency) |
| `Microsoft.EntityFrameworkCore` package in Domain and a property of type `DbContext` | `Domain_DoesNotDependOnEfCoreOrAspNetCore` fails and names the type |

Run only the architecture tests:

```bash
dotnet test --project tests/CityPark.Architecture.Tests/CityPark.Architecture.Tests.csproj
```
