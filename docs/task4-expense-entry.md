# Task 4: validated expense entry

Implemented and verified on 2 November 2026 (Australia/Sydney).

## User flow

Sign in and choose Add expense from Home or navigation, or visit `/expenses/new`.
Enter an AUD amount, date and active category; merchant and notes are optional.
The date defaults to today in Sydney. Save displays the saved AUD amount, clears
amount/merchant/notes and retains date/category for another expense. Validation
or service failures preserve input. Unsaved input is not retained across refresh.

The page initializes defaults even on a direct visit, loads owned active categories,
and provides loading, retry and empty-category states. It never accepts a user ID.
No public API or schema migration is introduced. The ledger remains Task 5.

## Code walkthrough

- `Components/Pages/AddExpense.razor` owns category loading and calls the existing
  ExpenseService. Authorization protects the route and navigation is signed-in only.
- `Components/Expenses/ExpenseForm.razor` is the reusable input/feedback component.
  Its Save delegate lets the page choose persistence without embedding database code
  in the form. A future edit workflow can reuse the fields and extend initialization.
- `ExpenseFormModel` holds decimal dollars and nullable required input. Data annotations
  and IValidatableObject provide required, amount, Sydney-date and trimmed-length checks.
  The service independently enforces the domain rules and category ownership/archive state.
- EditContext tracks input and validation messages. The submit handler validates before
  conversion or persistence. A new context after success clears old validation state.

The money boundary is deliberate: A$24.50 is decimal `24.50m` in the form, multiplied
by `100m` and converted to long `2450` only after validation. Values containing a
fraction of a cent are rejected, not rounded or silently truncated. ToInput validates
again so callers cannot bypass the conversion's guard. Trailing zeroes do not change
the monetary value. Stored values remain integer cents.

` saving ` is set before the first await and guarded at handler entry. The fieldset
is disabled while saving, and the button displays Saving. The prerendered form is
also disabled until the interactive circuit attaches. This protects ordinary repeated
submissions on one form; it is not cross-tab or network-retry idempotency. A connection
failure can leave the save outcome uncertain, so the message does not claim no row
was written. Global idempotency is outside this task's scope.

See Microsoft's [Blazor validation guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-10.0).

## Verification evidence

- Release test run: **54 passed, 0 failed**; no build warnings reported.
- Unit tests cover exact cents (including A$0.01, A$0.29 and A$999,999.99), zero/negative/
  excessive/over-precision values, missing fields, trimmed text limits, Sydney's date
  boundary and date validation after midnight.
- A form-to-service integration test verifies fresh-context SQLite reads, text trimming,
  date/category values and another user's inability to read the saved expense.
- Browser keyboard input of 24.505 was rejected with values/category/merchant preserved.
- Browser save of A$24.50 with a double-click produced one row; success reset amount and
  optional text while retaining category/date. A second keyboard-entered A$12.30 expense
  preserved its merchant and notes. Read-only SQLite checks verified both after refresh.
- Browser logout redirected the expense route to Login; signing in returned to the form.
- The form was inspected at a 360-pixel viewport: fields stack and document width equals
  content width, with no horizontal overflow. Labels and keyboard input were exercised.
- No pending EF model changes. No existing local accounts or expenses were deleted.

Browser fixtures use invented data and remain in the local test account. Automated
fixtures use disposable databases. Browser checks do not constitute full accessibility
or production reconnect/retry testing; those remain part of later acceptance work.
