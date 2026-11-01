# Task 3B: a first database transaction

Implemented 1 November 2026. Start with `Services/AccountDeletionService.cs`.

## Why the transaction matters

Account deletion changes several tables. If expenses were deleted successfully but
saving the deletion record failed, we would otherwise risk losing data without the
required evidence. A transaction groups those database writes into one unit: all
commit, or none commit. It is not a backup and cannot undo a completed commit.

## Read the code in this order

1. The fresh dependency-injection scope supplies one ApplicationDbContext shared by
   the service and Identity's UserManager. This ensures all writes use one transaction.
2. `await using var transaction = await db.Database.BeginTransactionAsync(ct);`
   begins that unit of work. Password and account checks must pass before any deletion.
3. `ExecuteDeleteAsync` immediately sends SQL DELETE commands for expenses and
   categories. `UserManager.DeleteAsync` sends the account deletion through Identity.
   They have run in SQLite, but the outer transaction has not committed them yet.
4. `db.AccountDeletionRecords.Add(...)` tells EF to track a new record. It does not
   immediately insert it. `SaveChangesAsync(ct)` sends the INSERT to SQLite.
5. `await transaction.CommitAsync(ct);` commits all the writes together. Only then
   does the service return Success and the account page sign out the current browser.

With an explicit outer transaction, **SaveChanges is not Commit**. Multiple SQL
commands and save operations can participate in one transaction. We need this
because Identity and our direct deletes do not form a single SaveChanges call.

## Where is rollback?

`await using` guarantees asynchronous disposal when the method exits, including an
early return or an exception. Disposing an uncommitted SQLite transaction rolls it
back. We therefore do not need a redundant catch solely to call RollbackAsync.
The exception still reaches the account page, which logs it and shows a generic
error. The fresh context is disposed too; a retry starts with clean tracked state.

Example: expenses removed -> categories removed -> account removed -> record INSERT
fails. CommitAsync is never reached. Disposal rolls back all earlier deletes and
the account remains usable. Once a commit succeeds, later UI/sign-out failures do
not reverse the database changes. The transaction covers database work only.

See Microsoft's [EF Core transaction documentation](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

## Minimal record

`AccountDeletionRecord` stores a unique event ID, internal user ID, UTC timestamp
and nullable ExportedAtUtc. UserId has a unique index and deliberately no foreign
key to AspNetUsers. The record survives its account's removal; a repeated request
for an already-removed account returns unavailable without adding another record.
The injected TimeProvider makes timestamps deterministic in tests.

The timestamp is sampled just before the record is saved; it is not a database
commit-time measurement. A record's existence proves a successful commit.

## What the tests demonstrate

The real SQLite suite passes 39 tests. New and extended deletion tests verify:

- Correct record, fixed UTC timestamp and pending export after successful deletion.
- No record for incorrect password, missing confirmation or failed Identity deletion.
- A SQLite trigger rejects the record INSERT after all deletes have run; a new scope
  verifies the account, password, categories and expenses survived rollback.
- Removing the injected fault allows retry to succeed with exactly one record.
- Repeated deletion creates no duplicate, and unrelated users remain intact.

The migration was rehearsed on a consistent backup copy and then applied locally.
Hashes of all original application-table columns matched before and after both
upgrades; SQLite integrity and foreign-key checks passed. The new deletion table
was empty and had no foreign keys. EF reported no pending model changes.

No existing local account was deleted to run these tests; fixtures use disposable
SQLite files. The migration adds an empty table and unique index; it does not
invent historical deletion evidence.

## Limits and next steps

This is the local transactional outbox foundation, not a complete delivery system.
No exporter, deletion-record cleanup, backup purge or restore automation exists yet.
Restoring an old application database can lose these records. Task 10 must provide
independent durable delivery and verify complete deletion history before reopening
a restored database. Retention must cover retained backups and active restores;
records are not automatically removed on their 30th day.
