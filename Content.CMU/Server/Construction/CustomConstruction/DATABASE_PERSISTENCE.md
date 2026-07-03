# Persisting admin-generated construction entries across Docker redeploys

## The problem

The in-game construction editors (Construction Items Editor, Tiles Editor, Lathe Editor, and the right-click
Add / Change / Remove verbs) currently persist by writing self-contained prototype YAML files into
`Resources/Prototypes/_AU14/CustomConstruction/Generated/`. That works on a long-lived host, but on this server
the Docker image is rebuilt and replaced on every patch, which wipes the container filesystem. Anything written
to `Resources/` (or to the user-data dir) at runtime is lost on the next deploy. Only the database survives,
because it lives in an external/volume-mounted Postgres.

So: file-based persistence cannot survive a redeploy here. The generated entries must be stored in the database.

## Can content code do this on its own? No.

Prototypes are not stored in the DB by the engine, and there is no generic key/value or blob table exposed to
content. Adding storage means adding a real table, which requires:

- a new entity + `DbSet<>` in `Content.Server.Database/Model.cs`, and
- EF Core migrations for BOTH providers (`Content.Server.Database/Migrations/Postgres` and `.../Sqlite`),
  generated with `dotnet ef migrations add` and committed.

Content code cannot create tables or generate migrations, so this part is a maintainer change. Everything below
is the exact, minimal set of pieces to add. The content side is already structured to drop straight onto it.

## Recommended shape: a dedicated table for admin-added entries

Per the maintainer's note ("have it have its own path for admintool-added entities, since they are actually
separate entities"), use a standalone table rather than overloading anything existing. The generated YAML is
already a complete, self-describing prototype document, so the simplest durable representation is: one row per
generated entry, storing the entry key and the YAML text. No need to model recipe internals in SQL.

### 1. Entity + DbSet (`Content.Server.Database/Model.cs`)

```csharp
public DbSet<CustomConstructionEntry> CustomConstructionEntries { get; set; } = null!;

// Admin-generated construction-menu entries (items, tiles, lathe recipes, menu overrides), persisted as the
// same self-contained prototype YAML the in-game editors already produce. Survives Docker redeploys.
public class CustomConstructionEntry
{
    public int Id { get; set; }

    // Unique logical key (matches the generated file stem, e.g. "AU14Custom_<entity>__<spawnlist>__<category>",
    // or a tile/lathe/override key). Used for upsert + delete.
    [Required]
    public string EntryKey { get; set; } = null!;

    // Which generated sub-directory the YAML belongs to: "" (root), "Tiles", "Lathe", or "Overrides".
    // Lets the loader keep the same grouping the file layout used, and the editors target the right kind.
    [Required]
    public string Kind { get; set; } = "";

    // The complete prototype YAML document (the same text written to the .yml today).
    [Required]
    public string Yaml { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
}
```

Add a unique index on `EntryKey` in `ModelDatabaseContext.OnModelCreating`:

```csharp
modelBuilder.Entity<CustomConstructionEntry>()
    .HasIndex(e => e.EntryKey)
    .IsUnique();
```

Then generate the migrations (run once per provider, from the repo root):

```
dotnet ef migrations add AddCustomConstructionEntries --context PostgresServerDbContext --project Content.Server.Database --startup-project Content.Server -- --provider Postgres
dotnet ef migrations add AddCustomConstructionEntries --context SqliteServerDbContext  --project Content.Server.Database --startup-project Content.Server -- --provider Sqlite
```

(Provider/context names follow whatever the existing `RMC*` migrations used; copy that invocation.)

### 2. DB manager methods (`IServerDbManager` + `ServerDbManager`)

```csharp
Task<List<(string Key, string Kind, string Yaml)>> GetCustomConstructionEntriesAsync();
Task UpsertCustomConstructionEntryAsync(string key, string kind, string yaml);
Task DeleteCustomConstructionEntryAsync(string key);
```

Implement them with the usual `using var db = await GetDb();` pattern the other methods use (find-by-EntryKey,
update-or-add, SaveChanges).

## Content-side integration (already mapped out; only 3 touch points)

`CustomConstructionMenuSystem` (and its `.Tiles` / `.Lathe` partials) already centralises persistence through a
handful of methods. To go DB-backed, those become DB calls instead of (or in addition to) `File.WriteAllText` /
`File.Delete`:

1. WRITE: every place that currently does `File.WriteAllText(FilePathForKey(key), yaml, ...)` also calls
   `_db.UpsertCustomConstructionEntryAsync(key, kind, yaml)`. (`BuildGeneratedYaml`, the tile builder, the lathe
   recipe/pack builders, and the overrides writer.)
2. DELETE: every `File.Delete(...)` (RemoveEntry, RemoveGroup, OnRemoveLatheRecipe) also calls
   `_db.DeleteCustomConstructionEntryAsync(key)`.
3. LOAD ON STARTUP: on round/server init, read all rows and feed them to the prototype manager at runtime:

   ```csharp
   foreach (var (key, kind, yaml) in await _db.GetCustomConstructionEntriesAsync())
       _prototypeManager.LoadString(yaml);   // IPrototypeManager.LoadString(string, bool overwrite = false)
   _prototypeManager.ResolveResults();        // finish loading the newly added prototypes
   ```

   `LoadString` + `ResolveResults` is the same path the engine uses for runtime-uploaded prototypes, so no
   restart is needed for them to appear; the DB simply replaces the file as the durable store. Lathe recipe
   PACKS (`AU14AutolatheRecipes` / `AU14ArmylatheRecipes`) must be loaded before the lathes index them, so load
   DB entries during init before the lathe pack regen runs (or have the regen read from the DB rows).

### Keeping files as a dev convenience (optional)

It is fine to keep writing the `.yml` files too: locally (non-Docker) they remain handy and git-committable, and
the DB load can be made idempotent (`LoadString(overwrite: true)` or skip keys already present as prototypes).
On the Docker host the files are wiped but the DB rows are authoritative, so nothing is lost.

## Summary for the maintainer

- Add the `CustomConstructionEntry` table (Model.cs + DbSet + unique index on `EntryKey`) and the two provider
  migrations.
- Add the three `IServerDbManager` methods.
- Ping back and the content side gets wired to call them (write on edit, delete on remove, load+`ResolveResults`
  on init). That is a small, self-contained change once the table exists.
