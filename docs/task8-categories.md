# Task 8: category management

Implemented 14 November 2026. The authenticated Categories navigation link opens
/categories. Users can add names, rename one row at a time, confirm archiving,
show archived rows and restore them. Archived rows may also be renamed.
Visual polish remains deferred; this task includes basic responsive/accessibility behaviour.

## Junior-engineer walkthrough

- `Categories.razor` owns input, selected edit/confirmation IDs, list visibility,
  busy/loading flags and feedback. `editing` holds one category ID, so only one
  row becomes a rename form. Cancel clears that selection without calling a service.
- The page calls the existing `CategoryService`. It does not query or write EF
  entities directly. That service still scopes every read/write to the authenticated
  owner and validates trimmed 1–40-character names. The unique index prevents
  duplicates, including archived names and concurrent requests.
- `@bind:event="oninput"` keeps the typed name in component state. A failed validation
  leaves it there so the user can correct it. Forms prevent the normal browser submit
  and invoke Blazor handlers; Enter submits, while Cancel uses type=button.
- `MutateAsync<T>` centralises the shared saving flow for service results containing
  either an integer ID or a boolean. It disables actions, performs the operation,
  handles validation, and reloads the list after success. The supplied function
  chooses create, rename, archive or restore. Comments explain the important boundaries.
- Archive sets IsArchived, never deletes category or expenses. Expense.CategoryId
  stays unchanged after rename, so reports show the new label and the same values.
- A failed list refresh after a successful write is reported accurately. An uncertain
  write outcome disables changes until Reload categories, reducing accidental retries.
  Reload discards any open rename editor; a full browser refresh also loses unsaved text.
- `Task.Yield` lets Blazor render loading/saving controls before SQLite's potentially
  synchronous I/O starts. It does not move database work to a background service.
- `OnAfterRenderAsync` focuses controls after they exist: rename input, archive
  confirmation, feedback, or heading after Cancel. CSS wraps actions at narrow widths.
  NavMenu adds the entry inside the authenticated section.

## Evidence

`dotnet test --configuration Release --no-restore`: 84 passed, zero failed.
Added SQLite lifecycle coverage for trimming, rename history, unchanged amounts/IDs,
archived-name reservation, failed duplicate rename preserving the old name, archived
rename, restoration allowing new expenses, and another user's identical category name.
Added empty/whitespace/overlong create-and-rename rejection tests. Existing tests cover
cross-user access and archived expense-assignment rules.
`dotnet ef migrations has-pending-model-changes`: no model drift. No dependencies or migrations added.

Browser tests used an isolated database copy with invented expenses only:
- Added Coffee with surrounding spaces; duplicate coffee rejected with input retained.
- Cancelled a rename, saved another, confirmed archive, showed archived rows, renamed
  an archived category and restored it.
- Dashboard showed the renamed archived category with the original A$4.50 expense total.
- 360-pixel screenshot verified wrapped controls; Tab reached Save then Cancel, Enter
  cancelled, and focus returned to the heading. Rename and archive focus were checked.
- Temporarily unavailable category table produced a safe error and disabled actions;
  restoring it and Reload categories recovered the list.
- An exclusive test-database lock exposed Loading categories and disabled controls;
  releasing it restored the list.
- Stopping the isolated server showed the existing reconnect dialog; restarting and
  browser reload recovered saved categories. This proves reload recovery, not automatic
  preservation of unsaved input across server restarts.

No normal expense data was modified. Full app-wide mobile/keyboard, two-user manual
acceptance and performance checks remain T09. No optimistic concurrency token is added;
a later successful rename/archive operation may overwrite an earlier operation.
