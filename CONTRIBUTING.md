# Contributing

## Workflow

Every change starts from a card on the board and reaches `main` only through a pull request.

1. Take a card (e.g. `CP-004`) and create a branch from an up-to-date `main`.
2. Commit in small, logical steps.
3. Push and open a pull request. The template asks for the summary, card ID, how it was tested and a checklist.
4. CI (`.github/workflows/ci.yml`) restores, builds and tests the solution. The PR can be merged only when the **Build and test** check is green.
5. Merge with **Rebase and merge**. Merge commits and squash merges are disabled, so `main` has a linear history.
6. Move the card to Done.

`main` is protected: direct pushes are rejected, and a PR needs a passing CI check and a branch that is up to date with `main`.

## Branch names

```
<type>/cp-<number>-<card-title-in-kebab-case>
```

Example: `chore/cp-002-add-docker-compose-with-postgresql-17`

## Commit messages

```
<type>(<scope>): <short imperative summary> (CP-<number>)
```

Example: `chore(db): add docker compose service for PostgreSQL 17 (CP-002)`

| Type       | Use for                                          |
| ---------- | ------------------------------------------------ |
| `feat`     | New functionality                                |
| `fix`      | Bug fix                                          |
| `refactor` | Code change that does not change behaviour       |
| `test`     | Adding or changing tests                         |
| `docs`     | Documentation only                               |
| `ci`       | CI workflows and repository automation           |
| `chore`    | Tooling, configuration, dependencies             |
| `style`    | Formatting only                                  |

The scope names the area, e.g. `db`, `api`, `ci`, `deps`. Dependabot commits use `chore(deps)` and `ci(deps)` without a card ID.

## Pull request titles

```
CP-<number>: <Short summary in sentence case>
```

Example: `CP-002: Add Docker Compose with PostgreSQL 17`

## Before opening a PR

```bash
dotnet build
dotnet test
```

The build treats warnings as errors, so it must be warning-free locally as well.
