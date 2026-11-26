# AI usage

CityPark was built with the help of an AI coding assistant. This page says where it was used
and how its output was checked. The use of AI tools was allowed for this project.

## Tool

| Tool | Used as |
| --- | --- |
| [Claude Code](https://claude.com/claude-code) (Anthropic) | Pair programmer in the editor: proposes code, tests and text that I review, change or reject |

The assistant did not have commit or push rights. Every command that builds, tests, migrates or
changes the repository was run by me, and every commit was made by hand.

## Where AI assistance was used

| Area | How |
| --- | --- |
| Backend code | Drafts of entities, use cases, endpoints, EF Core configuration and migrations, one card at a time |
| Tests | Drafts of unit, integration, concurrency and HTTP tests, and of test scenarios to cover |
| Frontend | Drafts of React pages, forms and the API client |
| Docker and CI | Drafts of the Dockerfiles, Compose services and the GitHub Actions workflow |
| Documentation | Drafts of the README, the docs in `docs/` and the ADRs |
| Commit messages and PR descriptions | Drafts in the format from [CONTRIBUTING.md](CONTRIBUTING.md) |
| Learning | Explanations of concepts and alternatives, and practice in defending the design decisions |

## What stayed with me

- **Scope and decisions.** The cards, their order and what is out of scope; the architecture and
  the choices recorded in the [ADRs](docs/adr/README.md) and [decisions](docs/decisions.md),
  such as the exclusion constraint for overlap prevention and cookie authentication.
- **Accepting a change.** A proposal became part of the project only after I read it, understood
  it and agreed with it. Proposals I could not explain were changed or dropped.
- **Responsibility.** I am responsible for all code and text in this repository, whoever drafted
  it.

## How the output was verified

Every change, whether written by me or drafted with AI, went through the same steps before it
reached `main`:

1. **Small units.** Work was done one card at a time, in small commits that can each be reviewed
   on their own.
2. **Review.** I read every diff before committing it and again in the pull request.
3. **Local build and tests.** `dotnet build` with warnings as errors, `dotnet test` against real
   PostgreSQL in Testcontainers, and the frontend lint, type-check, tests and build.
4. **CI.** Every pull request passed the [CI workflow](.github/workflows/ci.yml) (build and test,
   frontend, Docker smoke test) before it could be merged; `main` is protected.
5. **Checks against the real system.** Behaviour that tests alone do not prove, such as query
   plans and the Docker stack, was checked by running it ([docs/performance.md](docs/performance.md),
   `scripts/smoke.sh`).

Prompts and conversation logs are not part of the repository.
