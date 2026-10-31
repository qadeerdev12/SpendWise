# SpendWise delivery roadmap

## Goal and working agreement

Build the specification’s manual AUD expense tracker MVP by **Friday 13 November 2026**. Work **3–4 focused hours per weekday**, starting Monday 2 November. Dates use Australia/Sydney. This is a target, not a guarantee; move the release if security, accuracy or persistence checks fail.

The remaining plan budgets **39 hours**, near the top of the available 30–40 hours. Reserve the last 30–45 minutes of each session for tests and notes. If learning/debugging takes longer, replan dates rather than skipping release checks. No weekend work assumed.

## Already complete on 31 October

- Desktop solution and .NET 10 SDK pin.
- Blazor Interactive Server with generated Identity account flows and local SQLite.
- Clean build and one passing SQLite migration/persistence integration test.
- Starter home page, README, Git ignore rules and CI workflow.
- Login/register HTTP smoke checks. Full two-account browser flow remains task 1.

## Schedule

| Task | Target date | Hours | Deliverable | Milestone |
|---|---|---:|---|---|
| T01 | 2026-11-02 | 3 | Verify accounts and database setup | 1 |
| T02 | 2026-11-03 | 4 | Add Expense and Category entities and migrations | 1 |
| T03 | 2026-11-04 | 4 | Add current user services and category initialisation | 1 |
| T04 | 2026-11-05 | 4 | Build validated expense entry | 2 |
| T05 | 2026-11-06 | 4 | Build filtered and paged expense ledger | 2 |
| T06 | 2026-11-09 | 4 | Implement expense editing and confirmed deletion | 2 |
| T07 | 2026-11-10 | 4 | Build monthly spending dashboard | 3 |
| T08 | 2026-11-11 | 4 | Complete category management and responsive states | 3 |
| T09 | 2026-11-12 | 4 | Run MVP acceptance and performance checks | 3 |
| T10 | 2026-11-13 | 4 | Deploy pilot and demonstrate recovery | 3 |

## Daily workflow

1. Select the next issue whose dependencies are complete.
2. Explain the concept being learned and implement one small working slice.
3. Run relevant tests and check the feature in the browser.
4. Commit on a feature branch, open a pull request, and merge when CI passes.
5. Close the issue only when its checklist passes; note what to learn next.

The user and assistant work together on implementation. The user performs learning walkthroughs and usability acceptance. No unattended application development, reminders or scheduled automation is implied by these target dates.

## Milestone gates

### 1 Foundation and data
Target: 2026-11-04. Identity checks, relational schema and owner-scoped persistence.

### 2 Expense workflow
Target: 2026-11-09. Create, browse, edit and delete expenses with validated AUD amounts.

### 3 Insights and release
Target: 2026-11-13. Monthly summaries, category management, verification and pilot deployment.

## Task checklists

### T01 Verify accounts and database setup
Target: 2026-11-02 • Estimate: 3 hours • Depends on: existing foundation

- [ ] Register two local test accounts and verify login/logout.
- [ ] Check unauthenticated routes redirect to login.
- [ ] Verify a clean database can be created with committed migrations.
- [ ] Record results and keep credentials/database files out of Git.

### T02 Add Expense and Category entities and migrations
Target: 2026-11-03 • Estimate: 4 hours • Depends on: T01

- [ ] Implement schema, limits, keys and indexes from specification page 6.
- [ ] Use integer AUD cents and DateOnly expense dates.
- [ ] Enforce owner-matching category relationships and restrictive deletion.
- [ ] Apply migrations to a clean SQLite database and test relational constraints.

### T03 Add current user services and category initialisation
Target: 2026-11-04 • Estimate: 4 hours • Depends on: T02

- [ ] Create server-side current-user abstraction and a fresh DbContext per operation.
- [ ] Seed eight categories once per user with idempotent initialisation.
- [ ] Add owner-scoped expense and category operations without browser-supplied owner IDs.
- [ ] Add two-user integration tests proving reads and writes cannot cross accounts.

### T04 Build validated expense entry
Target: 2026-11-05 • Estimate: 4 hours • Depends on: T03

- [ ] Build reusable form for amount, date, category, merchant and notes.
- [ ] Validate amount/date/length rules in the service and UI.
- [ ] Prevent repeated saves while busy and preserve input on validation failures.
- [ ] Save one expense and verify it survives refresh; test boundary amounts and Sydney dates.

### T05 Build filtered and paged expense ledger
Target: 2026-11-06 • Estimate: 4 hours • Depends on: T04

- [ ] Display owned expenses ordered by date descending then ID descending.
- [ ] Implement month selector, optional category filter and 20-row paging.
- [ ] Reset paging on filter changes and show useful empty/loading/error states.
- [ ] Test month boundaries and exclusion of the other user’s data.

### T06 Implement expense editing and confirmed deletion
Target: 2026-11-09 • Estimate: 4 hours • Depends on: T05

- [ ] Reuse the form for editing and require confirmation before deletion.
- [ ] Treat missing and foreign-owned IDs as unavailable.
- [ ] Refresh ledger and totals after successful writes and handle deleting a page’s last row.
- [ ] Test persistence, invalid changes and already-deleted records.

### T07 Build monthly spending dashboard
Target: 2026-11-10 • Estimate: 4 hours • Depends on: T06

- [ ] Show selected month’s AUD total, count and category totals.
- [ ] Keep dashboard independent of ledger category filtering.
- [ ] Verify A$118.90 fixture and A$114.40/A$102.40 edit/delete outcomes.
- [ ] Show A$0.00 and a useful action for an empty month.

### T08 Complete category management and responsive states
Target: 2026-11-11 • Estimate: 4 hours • Depends on: T07

- [ ] Implement add, rename, archive and restore with case-insensitive name uniqueness.
- [ ] Retain archived category history; prevent new assignments while allowing existing records to retain it.
- [ ] Check 360-pixel layout, keyboard navigation, labels and focus.
- [ ] Verify error, loading and reconnect states.

### T09 Run MVP acceptance and performance checks
Target: 2026-11-12 • Estimate: 4 hours • Depends on: T08

- [ ] Run automated tests and the two-user manual acceptance checklist.
- [ ] Measure warm queries against 5,000 expenses and record environment and timings.
- [ ] Have a first-time tester attempt the expense-entry workflow using invented data.
- [ ] Fix accuracy, isolation and persistence defects before release; record outstanding non-blockers.

### T10 Deploy pilot and demonstrate recovery
Target: 2026-11-13 • Estimate: 4 hours • Depends on: T09

- [ ] Select an agreed host with HTTPS, WebSockets and persistent SQLite storage; confirm cost before provisioning paid resources.
- [ ] Deploy one instance, apply migrations safely and run deployed smoke tests.
- [ ] Verify restart persistence and restore a backup into a separate database.
- [ ] Complete README/demo and tag v0.1.0 only after AC01–AC08 pass. No real-data public registration until the specification’s email/security gate is met.

## Stretch backlog after the MVP

Monthly budgets, CSV export, charts, concurrency conflict handling, public email confirmation/recovery and a REST API learning phase. None belongs to the dated MVP workload.

## Release evidence

Map results to AC01–AC08 in the specification. Record test commands/results, two-user isolation, AUD totals, mobile/keyboard checks, performance results, restart persistence, backup restoration and deployed smoke checks. Keep secrets and personal test data out of issue descriptions, screenshots and commits.

## Scope and hosting decisions

The repository is public; that does not make the running app ready for public personal data. The initial hosted app is a test-data pilot. Choose a host and budget before deployment day. If hosting is not ready, record the local acceptance result and leave the deployment issue open rather than claiming the release is complete.
