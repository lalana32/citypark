# Architecture decision records

An architecture decision record (ADR) captures one significant decision together with its
context, the alternatives that were rejected and the consequences. New ADRs start from
[template.md](template.md).

| ADR | Decision | Status | Date |
| --- | --- | --- | --- |
| [ADR-001](0001-monolith-with-four-projects.md) | Monolith with four projects, without MediatR, CQRS, Repository or AutoMapper | Accepted | 2026-11-15 |
| [ADR-002](0002-overlap-prevention-with-exclusion-constraint.md) | Overlap prevention with a PostgreSQL exclusion constraint | Accepted | 2026-11-15 |
| [ADR-003](0003-cookie-authentication-with-identity.md) | Cookie authentication with ASP.NET Core Identity | Accepted | 2026-11-15 |
| [ADR-004](0004-utc-storage-and-sarajevo-business-day.md) | UTC storage and the Europe/Sarajevo business day | Accepted | 2026-11-15 |
| [ADR-005](0005-optimistic-concurrency-on-xmin-for-cancellation.md) | Optimistic concurrency on xmin for cancellation | Accepted | 2026-11-19 |

The business rules referenced as R1–R14 are defined in the [product brief](../product-brief.md).
