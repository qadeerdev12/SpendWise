# Backup retention and deleted accounts

Decision agreed on 1 November 2026 (Australia/Sydney).
Status: approved policy. Task 3B implements transactionally saved deletion records in the application database. Independent export, expiry and restore controls remain planned.

## Retention policy

Retain each database backup for no more than 30 days from its creation, then
expire it automatically. This applies to operational backups and manual migration
or recovery copies, including local copies. Account deletion removes live records
immediately upon successful commit; older backups may contain those records until
they expire. Deletion does not reset the age of an existing backup.

This is a product policy, not a claim about legal requirements. Do not promise
operational compliance until expiry and restoration controls have been tested.
Existing local migration backups currently have no automatic expiry.

## Deletion evidence and recovery

Task 3B saves a minimal AccountDeletionRecord with event ID, internal account ID,
UTC deletion timestamp and a nullable export timestamp. Only successful transactions
leave a record, so no separate success flag is needed. There are no email, password
or expense fields, and no foreign key to the deleted Identity account.
Internal identifiers still require access controls. There is no public query endpoint.
Independent storage, delivery reliability and retention still need implementation.

The log must survive restoration of the application database and remain available
while any retained backup could contain the deleted account. Restoring a database
must not restore access to deleted accounts: apply all relevant recorded deletions
to an isolated restore before allowing application access. If the deletion history
is unavailable or incomplete, do not release that restore for use.

## Deployment acceptance checks

- Inventory all backup locations, including manual copies, and record creation time.
- Implement expiry at 30 days and surface failures; verify expired copies are removed.
- Set deletion-log retention to cover the entire recoverable backup window and any
  active restore; define eventual expiry rather than retaining it indefinitely.
- Demonstrate that a backup taken before account deletion can be restored in isolation,
  have the deletion reapplied, and retain unrelated users and data correctly.
- Verify restored deleted accounts cannot sign in and have no expenses/categories.
- Document operational ownership, failure handling and user-facing retention wording.

Independent export and these operational checks belong to Task 10. Replan its estimate if needed rather than marking
recovery complete before the checks pass. No backup purge or scheduled job was
created by this policy decision.
