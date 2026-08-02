# Database persistence for admin-generated construction entries

## Status: IMPLEMENTED

The in-game construction editors (Construction Items Editor, Tiles Editor, Lathe Editor, the right-click
Add / Change / Remove verbs, and the in-menu "Remove Item" overrides) originally persisted only by writing
self-contained prototype YAML files under `Resources/Prototypes/_AU14/CustomConstruction/Generated/`. The
production server wipes the Docker filesystem on every patch, so anything written at runtime was lost;
only the database survives. Every generated entry is now ALSO stored in the database, using the same
pattern the INSFOR faction definitions use (verbatim YAML blob + small indexed key columns).

## How it works

- Table: `au14_custom_construction_entries` (`Content.Server.Database/AU14ConstructionModel.cs`).
  One row per generated file: `Kind` (the generated subdirectory: `""` root entries, `Tiles`, `Lathe`,
  `Overrides`), `EntryKey` (the file stem, e.g. `AU14Custom_<entity>__<spawnlist>__<category>`), `Yaml`
  (the exact file contents), timestamps. Unique index on (Kind, EntryKey) gives upsert semantics.
  Migrations exist for both providers (`Migrations/Sqlite` + `Migrations/Postgres`,
  `AU14CustomConstructionEntries`), applied automatically on server start like every other migration.
- DB access: `IServerDbManager.GetCustomConstructionEntries / UpsertCustomConstructionEntry /
  DeleteCustomConstructionEntry`, in partial files (`ServerDbManager.AU14Construction.cs`,
  `ServerDbBase.AU14Construction.cs`) so the shared DB code is not churned. EFCore only, no raw SQL.
- Content wiring: `CustomConstructionMenuSystem.Database.cs`. Every file write also upserts its row;
  every file delete (including the bulk group remove, the lathe recipe remove, and the startup
  invalid-entry cleanup) also deletes its row.
- Startup restore: on system init, rows whose file is missing (fresh container after a redeploy) are
  written back to disk BEFORE the client content pack is built, and their prototypes are hot-loaded via
  `IPrototypeManager.LoadString` + `ResolveResults`, so the server has them the same boot - there is no
  "one restart behind" gap. Restored lathe recipes trigger a lathe pack regen (the pack files are derived
  from the recipe files and are not stored in the DB).

The files remain the working store (git-committable, hand-editable on a dev checkout); the database is
the durable backup that survives redeploys. Only YAML this system generated itself is ever stored or
loaded - never player uploads, never executable content.

## Notes for the maintainer

- Nothing to add server-side: the table + migrations ship with this branch. Postgres and Sqlite both work.
- If a row's file is hand-deleted from a dev checkout without using the in-game editor, the next in-game
  remove/save of that entry reconciles the DB; alternatively delete the row from
  `au14_custom_construction_entries` directly.
- Saved player builds (`/saved_builds` in user data) are a separate feature and are still file-only.
