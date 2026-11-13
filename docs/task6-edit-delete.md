# Task 6: editing and confirmed deletion

Implemented 14 November 2026 (Australia/Sydney).

## What changed for the user

Every ledger row has Edit and Delete links. Edit opens a separate page with the
existing amount, date, category, merchant and notes. Save changes returns to the
same ledger filters; Cancel discards unsaved edits. Invalid input stays in the form.
The existing archived category may be retained; other archived categories are not
choices. The service checks that rule again when saving.

Delete opens a separate page showing amount, date, merchant and category with an
irreversible-deletion warning. Merely opening it, refreshing it or cancelling does
not delete anything. Only the explicit Permanently delete expense button invokes
the deletion service. A missing, already-deleted or foreign-owned record shows
Expense unavailable. No password is required for this individual expense action.

Month/category/page are now URL query parameters. Refresh, edit/save and cancel
preserve the selected view. If a change removes the last row on a page, the ledger
reloads the last valid page and updates the URL. Moving an expense outside the
selected month/category can make it disappear; a success message explains this.
The ledger refreshes its filtered count. Dashboard spending totals belong to Task 7.

## Junior-engineer walkthrough

### 1. Reuse the form instead of copying it

`ExpenseForm.razor` still owns fields, validation and busy/error feedback. Its new
optional InitialExpense parameter tells it whether we are adding or editing.
`ExpenseFormModel.FromExpense` converts stored cents back to decimal dollars and
copies the remaining fields. For example, 2450 / 100m becomes exactly 24.50 dollars.
Both modes still call ToInput, so validation and the conversion back to cents have
one implementation. No floats or silent rounding are introduced.

The form accepts a Save delegate: a function supplied by its parent page. AddExpense
continues to supply CreateAsync. EditExpense supplies an adapter around UpdateAsync.
This is dependency separation in practice: the form knows how to collect input,
while the page chooses which application operation to perform.

OnSaved and OnCancel are EventCallbacks. They let the form notify its parent page
without knowing a URL. After an edit, the parent navigates to the ledger; after a
create, the existing repeat-entry reset remains unchanged. The parent keys the form
by its loaded record so navigating between record IDs creates fresh input state.

### 2. Read and write authorization are separate checks

`EditExpense.razor` loads through GetAsync, which checks the signed-in owner. That
makes the URL ID a record selector, not permission to access the record. Save calls
UpdateAsync, which checks ownership and category rules again. The same principle
applies to DeleteExpense: showing a confirmation does not replace the ownership
predicate in DeleteAsync. These service protections existed before Task 6.

### 3. Confirmation controls when a destructive command runs

`DeleteExpense.LoadAsync` reads the record. `ConfirmAsync` performs deletion. This
separation prevents a GET, refresh or Cancel from causing a write. Busy controls
block ordinary repeated clicks while a request is pending. If the record has gone
away since loading, the service returns NotFound and the page shows unavailable.
A connection failure is reported as an uncertain outcome; it does not promise the
operation never reached the database.

### 4. Treat navigation state as input

`LedgerLocation` parses month/category/page and normalizes invalid values to safe
defaults. It constructs URLs from fixed application paths and encoded query values;
it never accepts a caller-supplied return URL. `ExpenseNavigationPage` shares the
query-binding properties between the three pages without sharing business logic.

`OnParametersSetAsync` reloads the ledger when query parameters change, including
browser back/forward navigation. `OnInitializedAsync` alone would miss changes to
an already-mounted page. Each load has a version number so an older, slower query
cannot overwrite a newer result. Canonical URLs reflect normalized/clamped state.
See [Microsoft's navigation guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0).

### 5. Keep success feedback separate from persistence

The circuit-scoped ExpenseFeedback service passes a one-use message to the ledger.
It is not stored in a query parameter, so a refresh cannot recreate success feedback
from the URL. Navigation occurs after the service call succeeds. A navigation error
after commit cannot roll back the database; the form's persistence error handling
therefore does not wrap the navigation callback.

## Automated verification

73 tests pass. The added tests cover:

- Exact edit-prefill round trips at one cent, 29 cents and the maximum amount.
- Query-state URLs and malformed/out-of-range input normalization using Sydney time.
- One-use feedback.
- Fresh-context reads of edited fields, preserved ownership/creation time and movement
  between monthly/category results.
- Invalid edits leaving stored values unchanged and already-deleted records returning
  unavailable for read, update and delete.
- Deleting the only row on page 2 leaving 20 rows on page 1.

Existing tests continue to cover cross-user writes/deletes, archive rules and creation.
No schema migration or third-party dependency is added.

## Browser verification

Performed on an isolated copy with invented expenses; the normal local database
is not used for edit/delete test mutations. Confirmed prefilled fields, fraction-of-a-cent
rejection with input retained, A$24.50 saved while retaining the archived current
category, refresh preserving page 2 and filters, and Cancel discarding a merchant edit.
Changing the category removed that expense from the original filter with success
feedback. Delete Cancel returned to the same view and retained the record.

Final deletion and last-page recovery browser evidence is recorded after the explicit
test-action confirmation. Automated successful deletion tests already pass.

## Deliberate MVP limits

There is no optimistic concurrency token: a later edit can overwrite an earlier
edit, and the confirmation details can become stale if another tab changes the same
record. Unsaved edits are discarded on Cancel/navigation/refresh. Busy guards are
per circuit, not global request-idempotency. Browser back navigation does not restore
unsaved edits. Backup retention/export safeguards remain the deployment task; this
feature does not add an audit record for individual expense deletion.
