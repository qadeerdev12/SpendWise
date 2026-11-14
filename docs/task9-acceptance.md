# Task 9: local MVP acceptance and performance

Verified 14 November 2026. Local technical checks passed; **Task 9 remains open
until the first-time-user exercise is recorded**. This is not a production release
sign-off. Backup restore, retention enforcement, hosted smoke checks and hosted
performance remain Task 10.

## Results

| Check | Result and evidence |
| --- | --- |
| Automated regression | 89 passed, 0 failed. Includes existing identity, ownership, money, category, transaction rollback and navigation tests. |
| Exact October fixture | A: groceries 82.40 on Oct 3, transport 12.00 on Oct 4, dining 24.50 on Oct 5. September A 9.00 and October B 100.00 excluded. Browser and SQLite tests show 118.90; editing dining to 20.00 gives 114.40; approved browser deletion of transport gives 102.40 across two rows. |
| Filtering | Dining filter shows only dining; dashboard includes all categories. Automated checks verify first/middle/last pages and deterministic date/ID ordering with 2,500 October rows. |
| Date boundaries | Added leap-February 2024, December 2025 and January 2026 tests, including both neighbouring dates. Only the selected month's two records total 58 cents. |
| Two-user browser isolation | A cannot open B's edit/delete details; B cannot open A's. Both show Expense unavailable. B sees 100.00 in October, not A's total. B's newly created category and November expense do not appear for A. |
| Backend isolation | Existing service tests reject foreign expense/category mutations and signed-out calls. Category IDs have no edit URL; service tests provide the mutation evidence. |
| Logout | Logout followed by protected /categories navigates to Login. Both existing test accounts subsequently sign in. Fresh registration was covered in Task 1, not repeated here. |
| New category and expense | B created Acceptance coffee, selected it on Add expense, corrected 4.505 to 4.50 and saved using Enter. Keyboard-entered merchant persisted after edit. |
| Mobile/keyboard | At 360 px, expense form labels/validation remain readable; ledger uses a contained horizontal scroll region. Tab reaches Save and Enter submits. Category focus/loading/error/reconnect checks are documented in Task 8. This is a basic workflow check, not a complete WCAG audit. |
| All categories archived | Injected in the isolated fixture. Add expense shows recovery link; it opens Categories; restoring one category brings back the expense form with only that active choice. |
| Restart persistence | Stopped and restarted isolated app. B's 4.50 expense and merchant survived. A still had the edited/deleted October total of 102.40. |
| Clean setup | Copied tracked and new source files into a new directory without bin/obj, .git or databases. Tool restore, package restore, Release build (0 warnings/errors), 89 tests and migrations to an empty DB passed. Started app and confirmed anonymous Home returns HTTP 302 to Login. Existing machine SDK/NuGet cache was available; this is not a virgin-machine/network-download certification. |
| First-time tester | Pending. See [tester script](task9-first-time-tester.md); the assistant and project owner do not substitute for an unfamiliar tester. |

Browser mutations used an isolated copy with invented expenses, never the normal
local data. Benchmark tests create and remove their own file-backed databases.

## Performance

Target from the original specification: 5,000 expenses for one user, 20 warm
samples, local list/summary calls within 500 ms, recording p95. The separate
2-second visible-operation target on the pilot host remains unmeasured until T10.

Environment: Apple M6, 32 GiB RAM, 12 logical CPUs; macOS 27.0.1 Arm64;
.NET runtime 10.0.13, SDK 10.0.402, Release build, local file-backed SQLite,
Pooling=False. Alice has 5,000 expenses across 8 categories: October 2,500,
September 1,500, August 1,000. Bob has one additional exclusion-control expense.
October's independent source-data oracle is 115,581,250 cents across 2,500 expenses.

| Service scenario | Median ms | p95 ms | Maximum ms | 500 ms target |
| --- | ---: | ---: | ---: | --- |
| Ledger page 1 | 0.944 | 1.050 | 1.812 | Pass |
| Ledger page 63 | 0.902 | 0.967 | 0.976 | Pass |
| Ledger page 125 | 0.932 | 1.186 | 1.847 | Pass |
| Category-filtered ledger | 0.923 | 1.146 | 1.642 | Pass |
| Dashboard | 1.193 | 1.512 | 1.984 | Pass |

Each scenario warms once, then records 20 sequential calls. Nearest-rank p95 is
sorted sample 19; median averages samples 10 and 11. Timings include a new DI scope,
authenticated-user resolution and service/context work. Fixture setup and assertions
are excluded. Assertions independently check the rows, counts and category sums
against the generated source data. [Raw samples](verification/task9-performance.txt)
are retained. Stopwatch timings use floating point for elapsed milliseconds, not money.

These are local, warm, single-caller service timings. They exclude HTTP/SignalR,
rendering and network time and do not establish multi-user production capacity.
Timing target misses are printed for acceptance review, not made flaky CI assertions.

Reproduce the performance check:

```sh
dotnet test --configuration Release --filter Category=Performance --logger "console;verbosity=detailed"
```

Run all checks:

```sh
dotnet test --configuration Release --logger "console;verbosity=detailed"
```

## Change found by acceptance

The Add expense empty state still claimed category management was unavailable.
It now links to Categories. The browser confirmed the recovery path after all
categories were archived. No schema changes, new packages or performance tuning
were necessary. The test class is partial so the new acceptance cases can share
its existing temporary-database/authentication setup without duplicating it.

## Remaining work and limits

- First-time tester: under-30-second typical expense entry after sign-in; record
  actual time, coaching and confusion. Do not tick Task 9 complete without this.
- T10: backup restoration, 30-day retention enforcement, restore deletion evidence,
  hosted performance/smoke checks and production email/security gate.
- Cosmetic polish remains deferred. Minor specification differences: new-expense
  exit is labelled View expenses rather than Cancel; clearing a category uses
  All categories rather than a separate clear-filter link. Neither blocks the tested workflow.
- Existing last-write-wins and loss of unsaved input after full reload remain MVP limits.
- Automation's text fill did not commit the optional merchant on the first create;
  explicit keyboard typing plus Tab committed it on edit. No product defect was
  established from that automation-specific observation; persistence was verified.

## Release-criteria mapping

AC01: existing registration evidence + current login/logout/isolation checks.
AC02–AC05: automated and browser evidence above plus Tasks 4–8.
AC06: core narrow-screen/keyboard/feedback evidence recorded; first-time usability
check pending. AC07: local restart passed, backup restore pending T10.
AC08: local performance/setup passed, deployment evidence pending T10.
All eight criteria are **not** signed off for release yet.
