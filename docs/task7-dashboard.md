# Task 7: monthly spending dashboard

Implemented 14 November 2026. Home now displays a selected month's AUD total,
expense count and category breakdown, sorted by spending descending. Archived
categories remain in history. Empty months display A$0.00, zero expenses and an
Add expense action. Existing first-visit category setup is preserved.

## Walkthrough for a junior engineer

1. `Home.razor` owns the presentation: month input, cards, table, loading and Retry.
   It asks for data rather than querying the database itself. The month travels in
   the URL so refresh/back/forward can reconstruct the view. View expenses carries
   the selected month to the ledger, with all categories and page one.
2. `DashboardService` is an ordinary C# class with one reporting responsibility.
   Dependency injection supplies the context factory, authenticated current user
   and clock. `ServiceRegistration` registers it as scoped so Home can inject it.
3. `GetMonthlySummaryAsync` gets the authenticated owner and validates the month
   against Sydney's current date. It creates a short-lived database context.
   `Where` restricts records to that owner and the interval from the month's first
   day (inclusive) to next month's first day (exclusive). This handles month lengths
   without inventing a last-day time or mixing purchase dates with timestamps.
4. `GroupBy` collects expenses by category; `Sum` and `Count` are translated by EF
   into SQLite aggregation. No Skip/Take is applied: all matching expenses count,
   even when the ledger needs multiple pages. The database returns one row per
   category, rather than transferring every expense to the page.
5. `DashboardSummary` and `CategorySpending` are records carrying read-only reporting
   values. Overall total and count come from the same category results, avoiding
   inconsistent cards/table if another request writes between separate queries.
6. Amounts stay as long integer cents during storage and arithmetic. Only Home's
   Money helper divides by 100m and formats with en-AU. Thus 11890 becomes A$118.90
   without using float/double for money.
7. Each load has a version number. An older asynchronous request cannot publish its
   result after newer navigation. Old results are cleared during loading/errors,
   so stale totals are not labelled with the newly selected month.

## Verification

`dotnet test --configuration Release --no-restore`: 80 passed, zero failed.
New real SQLite tests verify A$118.90 initially, A$114.40 after editing and A$102.40
following deletion; archived history; category ordering/counts; owner and month
isolation; both month boundaries; 21 expenses all included; empty months and
invalid month rejection. The existing signed-out test now covers DashboardService.

Browser verification used an isolated database with invented expenses:
- November: A$118.90, 3 expenses, Dining archived A$82.40 and Groceries A$36.50.
- Month selector changed to October: A$0.00, zero expenses and an Add expense link.
- Refresh retained October; View expenses opened October's unfiltered ledger.
- Invalid month normalised to the current Sydney month; a categoryId query did not
  restrict the dashboard and was removed from the canonical URL.
- Desktop layout visually inspected. Full mobile/keyboard acceptance remains T08/T09.

No schema changes, migrations, API endpoints or third-party packages were needed.
The report reloads on page/month navigation and refresh; live cross-tab updates,
charts, budgets and comparison trends remain outside this task. A loading failure
shows Retry; failure injection was not part of the browser checks.
