# Tooling decisions

Short decisions about tools and settings. Architecture decisions are recorded as ADRs in
[docs/adr/](adr/README.md); this file is for choices that are smaller, easy to revisit and mostly
about which package or setting to use.

Each entry has a decision, the rationale, the evidence it is based on and a fallback.

| # | Question | Decision | Date |
| --- | --- | --- | --- |
| D1 | snake_case naming in EF Core | `EFCore.NamingConventions` 10.0.x with EF Core 10 | 2026-11-15 |
| D2 | xUnit v3 runner mode on the .NET 10 SDK | Microsoft.Testing.Platform (MTP) | 2026-11-15 |
| D3 | Board and repository visibility | Public repository, private board until the project is finished | 2026-11-15 |
| D4 | TypeScript version of the frontend | TypeScript 5.9, not 6.0 | 2026-11-24 |
| D5 | Register reveals that an email is taken | Accepted: 409 on register, rate limited | 2026-11-24 |
| D6 | Language of the user interface | English for now; i18n is the next step for local users | 2026-11-26 |

## D1: snake_case naming in EF Core

**Decision.** Use `EFCore.NamingConventions` 10.0.1 (`UseSnakeCaseNamingConvention()`) together
with EF Core 10 and `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x. The project stays on EF Core
10 (LTS, matching `net10.0`) and does not move to EF Core 11.

**Rationale.** PostgreSQL folds unquoted identifiers to lower case, so PascalCase names from EF
Core would have to be quoted in every hand-written SQL statement, including the exclusion
constraints in [ADR-002](adr/0002-overlap-prevention-with-exclusion-constraint.md) and the
column names in the ERD of the [product brief](product-brief.md). The package applies snake_case
to tables, columns, keys, foreign keys and indexes in one line, instead of configuring every
name by hand.

**Evidence.** A throwaway console project with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 and
`EFCore.NamingConventions` 10.0.1 (EF Core runtime 10.0.4) generated the create script with
`parking_lots`, `parking_spaces`, `reservations`, `hourly_price`, `start_time`,
`pk_reservations`, `fk_reservations_parking_spaces_space_id` and `ix_reservations_space_id`.
`DateTime` mapped to `timestamp with time zone` and `TimeOnly` to `time without time zone`, as
[ADR-004](adr/0004-utc-storage-and-sarajevo-business-day.md) expects. The package declares its
EF Core dependency as `[10.0.1, 11.0.0)`, and on 2026-11-15 the Npgsql provider had no stable
11.x release either, so EF Core 11 is not an option yet.

**Fallback.** If the package falls behind a required EF Core version, set the names explicitly in
the entity configurations (`ToTable("reservations")`, `HasColumnName("start_time")`). Moving to
EF Core 11 waits until both the Npgsql provider and this package have stable 11.x releases.

## D2: xUnit v3 runner mode on the .NET 10 SDK

**Decision.** Test projects use xUnit v3 (`xunit.v3` 4.0.3) and run on Microsoft.Testing.Platform.
`global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`, each test project sets
`<OutputType>Exe</OutputType>`, and `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` and
`coverlet.collector` are removed. CI runs
`dotnet test --no-build --configuration Release --report-xunit-trx --results-directory TestResults`.

**Rationale.** xUnit v2 is no longer developed, so a new project starts on v3. On the .NET 10 SDK,
xUnit v3 together with the classic VSTest mode of `dotnet test` is blocked by the
Microsoft.Testing.Platform build targets, so MTP is the only mode that works without workarounds.
MTP also fails a run that discovers no tests (exit code 8). Under VSTest such a run is only a
warning, so a broken test adapter could leave CI green while no test runs.

**Evidence.** Two throwaway test projects with `xunit.v3` 4.0.3 on SDK 10.0.404:

| Check | VSTest (`xunit.runner.visualstudio` 4.0.1) | MTP (`global.json` runner) |
| --- | --- | --- |
| `dotnet test` | build error: *Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later*, also with `UseMicrosoftTestingPlatformRunner=false` | 3 of 3 tests passed |
| TRX report | – | `--report-xunit-trx` wrote one `.trx` file |
| Filter that matches no test | – | run failed with exit code 8 |
| Without `<OutputType>Exe</OutputType>` | – | build error: *xUnit.net v3 test projects must be executable* |

The same commands then built and ran the four CityPark test projects locally, and CI runs them
on every pull request.

**Consequences.**

- `dotnet test` arguments are MTP options: `--logger trx` becomes `--report-xunit-trx`, and a VSTest
  `--filter` becomes xUnit's `--filter-method`, `--filter-class` or `--filter-trait`.
- All test projects in the repository must be MTP projects; the `global.json` setting applies to
  the whole solution.
- Code coverage needs an MTP extension (for example `coverlet.MTP`) when it is added later;
  `coverlet.collector` is a VSTest data collector and was removed.

**Fallback.** Stay on xUnit v2 (`xunit` 2.9.3 with `xunit.runner.visualstudio` and VSTest), which
was the configuration before this decision and ran green in CI. This means reverting the
`global.json` test runner setting and the test project files.

## D3: Board and repository visibility

**Decision.** The GitHub repository is **public**. The Trello board stays **private** (visible
only to the author) until the project is finished.

**Rationale.** The repository is the deliverable of the technical interview and has to be easy to
open without an invitation. Being public also gives free branch protection, required status
checks and CodeQL default setup, which the pull request workflow in
[CONTRIBUTING.md](../CONTRIBUTING.md) relies on. The board holds
working notes, unfinished cards and the context of the assignment, which are not meant for
readers while the work is in progress.

What is kept out of the public repository (and out of the board before it is ever shared):

- the original assignment text and the name of the company it comes from;
- personal data: commits use the GitHub `noreply` e-mail address;
- real credentials: only local-development placeholders are committed (`.env.example`), `.env`
  is git-ignored;
- private working notes that are not documentation.

**Fallback.** If the company asks for a private repository, switch it to private and invite the
reviewers as collaborators. On a free account this removes branch protection and CodeQL, so the
pull request workflow then depends on discipline instead of enforced rules. If the board is
shared at the end, review it first for the items listed above, or export the relevant cards into
`docs/` instead.

## D4: TypeScript version of the frontend

**Decision.** `web/` uses TypeScript `~5.9.3`, although `create-vite` scaffolds the app with
TypeScript 6.0.

**Rationale.** The API types are generated with `openapi-typescript`, which declares
`typescript ^5.x` as a peer dependency, so `npm install` refuses it next to TypeScript 6. Moving
down one version keeps the dependency tree valid without `--legacy-peer-deps`, which would hide
every other peer conflict as well. The project uses no TypeScript 6 feature; `strict` is set
explicitly in both tsconfig files, because TypeScript 5 does not turn it on by default.

**Evidence.** On 2026-11-24 `npm view openapi-typescript` showed 7.13.0 as `latest` (and
7.0.0-rc.1 as `next`), both with the peer range `^5.x`. `typescript-eslint` 8.72 accepts
`>=4.8.4 <6.1.0`, so it works with 5.9. Typecheck, lint, tests and build pass on 5.9.3.

**Fallback.** When `openapi-typescript` releases support for TypeScript 6, remove the Dependabot
ignore rule for `typescript` major updates and upgrade.

## D5: Register reveals that an email is taken

**Decision.** `POST /api/v1/auth/register` answers **409** "An account with this email already
exists." for a taken email. This is a known account-enumeration leak and is accepted for now.

**Rationale.** Login hides whether an account exists: an unknown email, a wrong password and a
locked account all get the same 401. Register cannot hide it without email verification. The
usual fix is to answer every registration the same way ("check your inbox") and to email the
owner of an existing account instead. CityPark sends no email, so that fix is out of scope, and a
customer who already has an account gets a clear answer instead of a silent one.

**Mitigation.** Register shares the `credentials` rate-limit policy with login: 10 requests per
minute per client address (`AuthRateLimiting`). This does not hide the answer for a single email;
it slows down checking a long list of them.

**Fallback.** When email sending is added, register returns the same response for a new and a
taken email, and the owner of a taken email gets a notification instead of the 409.

## D6: Language of the user interface

**Decision.** The web UI stays in English. All texts are written directly in the components; there
is no translation layer and no i18n library.

**Rationale.** CityPark is a portfolio project, and the people who review it read English. The
code, the API errors, the documentation and the board are in English too, so one language keeps
them consistent. Adding an i18n library now would mean extracting every text into keys before
there is a second language to translate them into.

**Evidence.** On 2026-11-26 every page, form message and API problem detail shown in the UI is in
English. The lots are in Sarajevo, so lot names such as Baščaršija stay in their local spelling;
they are data from the API, not UI texts.

**Fallback.** For local users the next step is i18n: move the UI texts into translation files with
an i18n library (for example `react-i18next`), add Bosnian, Croatian and Serbian next to English,
and map the API error codes to translated messages in the frontend instead of showing the English
detail text.
