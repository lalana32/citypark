# ADR-004: UTC storage and the Europe/Sarajevo business day

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Several rules in the [product brief](../product-brief.md) depend on local time in the city:
opening hours (R3), "a reservation does not cross midnight" (R3), the booking horizon (R4) and
the start tolerance (R5). Customers choose a date and local start and end times on a
15-minute grid (R1).

The city uses the time zone **Europe/Sarajevo**: UTC+1 in winter and UTC+2 in summer. On the
last Sunday of March the local clock jumps from 02:00 to 03:00, so 02:00–02:59 does not exist.
On the last Sunday of October it falls back from 03:00 to 02:00, so 02:00–02:59 happens twice.
Servers, containers and CI runners usually run in UTC, so the server's local time zone cannot be
relied on.

## Decision

- **Storage:** every instant is stored as PostgreSQL `timestamptz` and handled in .NET as a
  `DateTime` with `Kind = Utc` (Npgsql rejects other kinds for `timestamptz`).
- **Business day:** all rules that mention a day, a clock time or midnight are evaluated in the
  city time zone, loaded once with `TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo")`.
  The zone is configuration, not data (one city, see the glossary).
- **Opening hours** are stored as local `time` values (`opens_at`, `closes_at`), because they
  describe the wall clock, not an instant.
- **Conversion at the edge:** the API accepts a local date and local times, converts them to UTC
  in the application layer and returns instants as ISO 8601 with the local offset
  (for example `2026-11-15T10:00:00+01:00`).
- **DST edge cases:** a local time that does not exist (spring gap) is rejected as invalid. An
  ambiguous local time (autumn overlap) resolves to its first occurrence.
- **Duration and price** are computed from the UTC difference, so a reservation is billed for
  the time that really passed (R8).
- **"Now"** comes from `TimeProvider`, injected, so R4 and R5 can be tested with a fixed clock.

## Alternatives

### Store local time (`timestamp without time zone`)

**Rejected because** a local time is ambiguous during the autumn change, durations across a DST
change come out wrong, and the stored value only makes sense together with knowledge of the zone.

### Store `DateTimeOffset` with the local offset

**Rejected because** PostgreSQL `timestamptz` does not keep the offset (it normalizes to UTC),
so the offset would be lost anyway, and Npgsql accepts only offset zero for `timestamptz`.

### Use the server's local time zone

**Rejected because** containers and CI runners run in UTC; the rules would behave differently on
a developer machine and in production.

### NodaTime

Separate types for instants, local dates and zoned times make mistakes harder to write.
**Rejected for now because** it adds a dependency, Npgsql and EF Core plugins, and a learning
curve for a project with a single time zone. It is the first candidate if time handling grows
(several cities, weekly schedules).

## Consequences

### Positive

- Stored instants are unambiguous and sort and compare correctly.
- The overlap constraints in [ADR-002](0002-overlap-prevention-with-exclusion-constraint.md)
  work on UTC ranges and are not affected by DST.
- Tests can pin "now" to any instant, including the DST change days.

### Negative and trade-offs

- Every boundary that receives or shows a time needs a conversion; forgetting one shows times
  shifted by one or two hours. Conversions are kept in one place in the application layer.
- `DateTime` does not express in the type whether a value is UTC or local; discipline and tests
  must cover what NodaTime would enforce.
- The time zone database must be present at runtime. Linux container images without `tzdata`
  (some minimal images) fail to find `Europe/Sarajevo`; the image choice must include it.
- On DST change days a local day is 23 or 25 hours long, and some local times are rejected or
  resolved by convention. With typical opening hours (for example 07:00–22:00) this does not
  affect reservations, but a lot open at night would see it.

## Update (CP-055)

Added in CP-055 (2026-11-26). The text above is kept as written; this section records how the
API was built.

- **The API takes ISO 8601 instants with an offset**, for example `2026-12-01T10:00:00+01:00`, not
  a local date and local times as [Decision](#decision) describes. The instant is converted to UTC
  as soon as it is read.
- **Local times are derived only to check the rules.** The domain converts the UTC instant to
  Europe/Sarajevo to check the grid, the single day, the opening hours, the horizon and the start
  tolerance (R1, R3–R5). Storage and the overlap constraints stay in UTC.
- **The DST rules for local input do not apply to the API.** An instant with an offset always
  names exactly one moment, so there is no non-existent or ambiguous local time to reject or
  resolve. The DST notes above apply only where a client builds such an instant from a local
  wall-clock time.
