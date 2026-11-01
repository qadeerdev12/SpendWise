# Task 3: services, initialization and account deletion

Implemented and verified locally on 1 November 2026.

## What a service does

A service is a C# class that performs one application responsibility. A Blazor page
collects input and shows results; a service enforces ownership and business rules;
EF Core persists the result to SQLite. There is no separate HTTP API in this MVP.

For example, a future expense form passes amount, date and category to
`ExpenseService.CreateAsync`. It cannot supply a user ID or audit timestamps.
`ICurrentUser` obtains the owner from the authenticated server-side principal.
Every record lookup and mutation includes that owner. A foreign ID gets the same
unavailable result as a missing ID.

## Context lifetime and transactions

Each ordinary service operation creates and disposes a fresh context through
`IDbContextFactory`. This avoids sharing a tracked context across a long-lived
Blazor circuit. The factory is scoped to remain compatible with Identity's scoped
configuration. DTOs carry values to the UI; tracked entities stay inside services.
See Microsoft's [Blazor EF Core guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core).

Account deletion creates a fresh DI scope so Identity's UserManager and explicit
dependent-data deletion share one context and transaction. If account deletion
fails, the expenses and categories are restored by rollback. Only a committed
success signs out the current browser through the normal HTTP account page.

## Default categories

The authenticated home page initializes Groceries, Dining, Transport, Housing,
Utilities, Health, Entertainment and Other. A durable `CategoriesInitialized`
flag prevents refreshes from duplicating categories or recreating a renamed or
archived default. A conditional database write claims initialization before reading
categories; the flag and inserted rows commit together. Concurrent visits are tested
using separate connections. A failed attempt rolls back and may be retried.

Migration `20261101105756_TrackCategoryInitialization` adds the flag with default
false, allowing existing accounts to initialize when they next visit home.

## Operations and rules

- Expenses: create, retrieve, update, delete, and list by month/category with 20-row paging.
- Categories: list, create, rename, archive and restore; archived names still reserve uniqueness.
- AUD stays in integer cents: 1 through 99,999,999. Decimal conversion belongs to the upcoming form.
- Dates run from 1 January 2000 through today in Australia/Sydney. Audit timestamps are UTC.
- New expense assignments require an active owned category. Existing expenses may retain their archived category.
- Expected validation failures return a structured result. Infrastructure exceptions remain distinguishable.

## Permanent Delete Account

The user explicitly expanded the MVP scope to include deletion. The account page
requires the current password and a separate confirmation checkbox. The service
checks these requirements again, rejects locked-out accounts, and deletes the
current user's expenses, then categories, then Identity account in one transaction.
Identity relationships remove its dependent authentication records. Other users
remain untouched. This MVP supports password accounts; passkey-only accounts would
need an alternative reauthentication design before deletion is supported for them.

There is no in-app recovery. Deletion removes live database records; it does not
purge previously created backup files. Backup retention and all-session immediate
revocation are separate production considerations. The current browser signs out;
other existing sessions follow Identity's normal revalidation behavior.

## Verification evidence

- `dotnet test --configuration Release`: **37 passed, 0 failed**, with no build warnings reported.
- `dotnet ef migrations has-pending-model-changes`: no model changes since the latest migration.
- Real SQLite tests cover signed-out rejection, two-user isolation, CRUD, paging,
  Sydney date boundaries, archived categories, concurrent initialization and retry after failure.
- Deletion tests cover missing confirmation, wrong password, successful owner-only
  deletion, and an injected failure at Identity deletion proving complete rollback.
- A consistent SQLite backup was migrated in a separate trial database first.
  Hashes of all original application-table columns matched before and after, in
  both the trial and local database. SQLite integrity and foreign-key checks passed.
- Browser: existing account login, home readiness and refresh passed. Exactly eight
  categories exist for the initialized account; all three existing accounts remain.
- Browser: submitting without confirmation and submitting an incorrect password
  with confirmation both displayed the expected error. Successful deletion is
  covered by integration tests; no existing local account was deleted in the browser.

## Next learning slice

Task 4 adds the expense-entry page. These services already enforce its core rules,
but UI validation, busy state, decimal conversion and user feedback remain to build.
