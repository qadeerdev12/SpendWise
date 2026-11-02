# Task 5: filtered expense ledger

Implemented and verified on 2 November 2026 (Australia/Sydney).

## Behavior

`/expenses` requires sign-in and defaults to the current month in Sydney. It lists
owned expenses by date descending, then ID descending for deterministic ordering.
Columns show date, merchant (or Not provided), category and exact AUD formatting.
The month selector supports January 2000 through the current Sydney month. The
optional category selector includes archived categories, marked explicitly, so
historical expenses remain searchable. Either filter resets to page 1.

Pages contain up to 20 rows with a filtered count, page number and Previous/Next
controls. If another operation removes the last page, the next load clamps to the
last available page and queries it again. Controls are disabled while loading to
avoid overlapping requests. Loading and error states hide stale results. Retry
preserves filters. Empty results explain how to change the filters or add an expense.
Filters are component state: refresh or leaving/reopening the page resets them to
the current month, all categories and page 1. URL-persisted filters are not part of
this MVP task.

Home, signed-in navigation and the entry page link to Expenses. The ledger links
to Add expense. Editing/deletion buttons remain Task 6.

## Engineering walkthrough

`Components/Pages/ExpenseLedger.razor` owns presentation state and delegates reads
to ExpenseService.ListAsync. It never accepts an owner ID. The existing service
applies authenticated ownership first, then month/category predicates, then count,
ordering and Skip/Take. Filtering in SQLite means pagination represents the whole
matching dataset rather than filtering only the 20 displayed rows.

For page 2, Skip((2 - 1) * 20) skips 20 matching rows and Take(20) requests the next
20. TotalCount is computed before Skip/Take so the UI knows whether another page
exists. The secondary ID ordering gives rows sharing a date a stable order.
The month predicate is inclusive at the start and exclusive at the next month's
start. AmountCents is divided by decimal 100m for display; storage is unchanged.

The category query deliberately includes archived rows. Direct navigation also
runs the existing idempotent initializer. Comments explain these decisions in the
page. The table keeps native headers/caption and has a labelled, focusable scroll
region so wide columns do not force the whole mobile page to scroll horizontally.
No schema migration, new dependency or HTTP API was needed.

## Verification

- Release test suite: **60 passed, 0 failed**, no build warnings reported.
- New SQLite tests cover the October/November boundary, same-date ID ordering,
  archived-category filtering/counts, exact cents and invalid month/page inputs.
  Existing tests cover 20-row pagination and cross-user isolation.
- Browser fixture in an isolated copy: 24 November rows produced pages of 20 and 4.
  An archived category produced 22 rows; choosing it from page 2 reset to page 1.
- Changing the month from page 2 reset to page 1 and displayed only the 31 October
  fixture at A$0.29. A category with no matches displayed the empty state.
- A temporary unavailable-table fault in that copy displayed a generic error,
  cleared old rows and preserved October/all-category filters. Restoring the table
  and clicking Try again recovered the correct result.
- Removing six fixture rows between page loads caused the UI to recover page 1 of 1
  with 18 remaining November rows, rather than display a nonexistent second page.
- Logout redirected to Login. A different account saw no fixture rows.
- At 360px, document width matched content width; the table scrolled inside its own
  focusable region. This is a layout/keyboard smoke check, not a complete accessibility audit.
- The normal local app was restarted at port 5037 and displayed existing saved expenses.
  Fixture mutations and injected faults did not touch its database.

The screenshot uses invented fixture data. The test copy is temporary and removed
after verification. Deployment, performance acceptance and dashboard totals remain
later tasks. Read counts and rows use separate queries, so this is not a frozen
snapshot across concurrent writes; the UI reloads on navigation/filter changes.
