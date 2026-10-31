# SpendWise

A personal expense tracker built while learning C#, ASP.NET Core and Blazor.

## Current milestone

Project foundation only: .NET 10 Blazor Web App, Interactive Server rendering,
Individual Accounts, SQLite, a test project and a build/test workflow.
Expense and Category entities, relational constraints and migrations are implemented.
Category seeding, user-scoped services, CRUD screens and dashboard totals are next.
See [the data-model walkthrough](docs/data-model.md) for design decisions and test coverage.
See `docs/SpendWise_Project_Specification.pdf` for the full plan.

## Requirements

.NET SDK 10.0.402 (or a later patch in the same feature band), Git and an editor.
Run all commands below from the SpendWise folder.

## First setup

```sh
dotnet tool restore
dotnet restore
dotnet ef database update --project src/SpendWise.Web --startup-project src/SpendWise.Web
```

The database is local to `src/SpendWise.Web/Data/app.db` and is excluded from Git.
If running EF commands with a different working directory, provide an explicit
connection path to avoid creating a second database.

## Run locally

```sh
dotnet run --project src/SpendWise.Web --launch-profile http
```

Open the local address printed in the terminal. Register a test account with a
password of at least 12 characters, including uppercase, lowercase, a number and
a symbol. Local Development skips email confirmation; no email is sent.
The home page requires sign-in. Use invented data only.
For HTTPS, trust the development certificate with `dotnet dev-certs https --trust`
then run with `--launch-profile https`.

## Verify

```sh
dotnet build --configuration Release
dotnet test --configuration Release
```

The integration suite uses real SQLite migrations to verify account-preserving upgrades,
exact cents, ownership relationships, category uniqueness and restrictive deletion.
Browser login/logout checks are recorded in docs/task1-verification.md.

## Layout

- `src/SpendWise.Web`: UI, generated Identity account flows and application code.
- `tests/SpendWise.Tests`: unit and integration tests.
- `docs`: specification and development notes.
- `.github/workflows/ci.yml`: build and test when a remote repository is connected.

## Delivery plan

See [the dated roadmap](docs/ROADMAP.md) and [GitHub issues](https://github.com/qadeerdev12/SpendWise/issues).
Target: 13 November 2026, working 3–4 hours per weekday.

## Next milestone

1. Add a per-operation database context factory and user-scoped services.
2. Initialise eight default categories once per user.
3. Complete one slice: create an expense and display it in the ledger.

## Release boundary

This setup is not production-ready. Production requires confirmed accounts but
the generated email sender is still a no-op. Configure real email delivery,
remove development confirmation-link exposure, complete the security requirements
and test the deployed authentication flow before public use.
Never commit secrets, database files or backups.
