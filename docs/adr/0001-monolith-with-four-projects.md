# ADR-001: Monolith with four projects, without MediatR, CQRS, Repository or AutoMapper

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

CityPark has one bounded context (parking reservations), one database, one team and a delivery
window of days, not months. The [product brief](../product-brief.md) keeps the scope small on
purpose: lots, spaces and reservations, with the hard part being correctness (no double booking,
R6 and R7) rather than scale.

The structure has to:

- keep business rules free of framework and database code, so they can be unit tested;
- make the dependency direction visible and enforced by the compiler;
- stay small enough to be explained and defended end to end.

Popular .NET building blocks (MediatR, CQRS, the Repository pattern, AutoMapper) are often added
by default. Each one adds a layer of indirection, so each has to earn its place.

## Decision

CityPark is a single deployable application (a monolith) split into four projects with a
one-way dependency direction:

| Project | Contains | References |
| --- | --- | --- |
| `CityPark.Domain` | Entities, value objects, business rules (R1–R14 that are marked "Domain") | nothing |
| `CityPark.Application` | Use cases as plain services, DTOs, interfaces for infrastructure | Domain |
| `CityPark.Infrastructure` | EF Core `DbContext`, PostgreSQL, ASP.NET Core Identity | Application, Domain |
| `CityPark.Api` | Minimal API endpoints, authentication setup, composition root | Application, Infrastructure |

The following are deliberately **not** used:

- **MediatR**: endpoints call application services directly. Cross-cutting concerns
  (validation, logging, error mapping) use ASP.NET Core endpoint filters and middleware.
- **CQRS**: reads and writes use the same model and the same database. Read endpoints may
  project straight to DTOs with `Select`.
- **Repository pattern**: EF Core's `DbContext` already is a unit of work and `DbSet<T>` already
  is a repository. Application code works with the context through an interface it defines
  (`IApplicationDbContext`), implemented in Infrastructure.
- **AutoMapper**: mapping is written by hand (`ToDto()` methods), so it is checked by the
  compiler and visible with "Find all references".

## Alternatives

### Microservices

Separate services for lots, reservations and users. **Rejected because** there is one bounded
context and one team. The no-overlap guarantee needs one transaction in one database; splitting
it across services would need distributed coordination for no benefit.

### Single project

Everything in `CityPark.Api`. **Rejected because** nothing would stop domain code from depending
on EF Core or HTTP types. Separate projects make the compiler enforce the dependency direction.

### Modular monolith with vertical modules

One module per feature (lots, reservations, identity), each with its own layers. **Rejected
because** the domain is one small context; module boundaries would add ceremony without
separating anything that changes independently.

### MediatR with CQRS handlers and pipeline behaviours

One request and one handler per use case, with behaviours for validation and transactions.
**Rejected because** it adds indirection (the call site no longer shows what runs) and a package
dependency, while the project has a handful of use cases. MediatR and AutoMapper also moved to a
commercial license model in 2025, which is a further, though secondary, reason.

### Generic repositories over EF Core

`IRepository<T>` with `GetById`, `Add`, `Remove`. **Rejected because** it hides EF Core features
the project needs (projections, `AsNoTracking`, transactions, raw SQL for the exclusion
constraint) behind a weaker interface and duplicates what `DbContext` already provides.

## Consequences

### Positive

- Fewer moving parts: a request goes endpoint → application service → `DbContext`, which is
  easy to follow and to debug.
- The domain project has no package references and is unit tested without mocks.
- The compiler rejects a reference from Domain to Infrastructure.

### Negative and trade-offs

- `CityPark.Application` references the EF Core package (for `DbSet<T>` and LINQ). It is not
  persistence-ignorant; replacing EF Core would touch application code.
- Application services are tested mainly with integration tests against a real PostgreSQL
  database, because the `DbContext` is not mocked. These tests are slower than pure unit tests.
- Cross-cutting behaviour has to be wired per endpoint group (filters) instead of once in a
  MediatR pipeline.
- If the domain grows into several bounded contexts, the structure has to be revisited in a new
  ADR (for example towards a modular monolith).
