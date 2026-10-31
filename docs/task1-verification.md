# Task 1 account and database verification

Date: 31 October 2026 (Australia/Sydney)
Scope: local Development environment; invented test accounts only.
Result: planned functional checks passed on the completed verification sequence.
One earlier inconsistent browser observation is retained below for transparency.

## Results

| Check | Result | Evidence |
|---|---|---|
| Register two accounts | Pass | Two fresh invented example.invalid accounts reached the protected home page after registration. |
| Sign in and sign out | Pass | Account A signed in with its password; both accounts were signed out using the navigation form. |
| Incorrect password | Pass | One deliberately incorrect password showed “Error: Invalid login attempt.” and did not grant access. |
| Protected home | Pass | Signed-out browser navigation to / returned to /Account/Login?ReturnUrl=%2F. An independent anonymous HTTP request returned 302 to the same route. |
| Process restart | Pass | Stopped the identified SpendWise web process, restarted the same project and database, then performed fresh password logins for both accounts successfully. |
| Existing integration test | Pass | dotnet test --configuration Release: 1 passed, 0 failed. Real SQLite migrations and persistence across two DbContexts are covered. |
| Fresh file database | Pass | Applied committed migrations to a separate previously absent SQLite file under /private/tmp; CreateIdentitySchema was recorded, integrity_check returned ok and user count was zero. |
| Data excluded from source control | Pass | git check-ignore confirms Data/app.db is ignored; no credential or database files added to this change. |

## Verification commands

Run from the solution root. Use a new unique temporary file path for each fresh-database check.

```sh
dotnet test --configuration Release
dotnet ef database update --project src/SpendWise.Web --startup-project src/SpendWise.Web --connection 'Data Source=/private/tmp/spendwise-task1-fresh-20261031.db'
```

The normal local database was not replaced. Three invented accounts were created in total: one during the interrupted preliminary run and two in the completed sequence. They remain local. Passwords were generated in memory and are not recorded here. No real email was sent. The final browser state was signed out; the local app was left running.

## Observations and limits

- During the preliminary in-app browser sequence, logout briefly displayed the login page, then the browser showed the signed-in home page again. A second logout and a later full verification sequence worked. The cause was not established. Do not describe this as a diagnosed or fixed application defect. Repeat logout in an independent browser before public release; if reproducible, investigate cookies and navigation before changing code.
- An attempted independent Chrome check timed out. The completed browser sequence used the in-app browser.
- The navigation still exposes a Weather link although the demo page was removed. This is a separate UI-cleanup follow-up; no change was made without discussing it with the user.
- The first attempted new server process found the port already occupied by SpendWise. The running process was identified by its executable and project directory before the planned restart.
- Email confirmation is intentionally disabled in local Development. Production confirmation, recovery email, lockout thresholds, passkeys and multi-browser session behaviour were not certified by this task.
- Authentication identifies the user. These checks do not prove expense-data isolation: that requires the owner-scoped service tests planned for task 3.

## Changes in this task

Documentation only. No application or test source was changed, so no new code comments were necessary. Existing code was exercised rather than rewritten.

## Engineering explanation

Registration checks the UI, validation, Identity and database together. A valid login checks password verification and cookie issuance. A failed login checks rejection. Logout followed by a direct protected-page request checks the observable access boundary. A fresh login after restart proves the account is in persistent storage rather than only server memory. Creating a separate database proves the committed schema can be reconstructed without relying on the developer's existing database.

The automated test is narrower: it inserts an Identity user entity and reads it through another context. It does not exercise password hashing or browser cookies. The browser checks complement it rather than duplicate it.
