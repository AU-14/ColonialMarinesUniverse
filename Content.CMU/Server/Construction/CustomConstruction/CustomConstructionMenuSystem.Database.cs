// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Upload;

namespace Content.Server._AU14.Construction.CustomConstruction;

/// <summary>
/// Database mirror for every generated construction file, so admin-added entries survive Docker
/// redeploys (the container filesystem - including Resources - is wiped on every patch; only the
/// database persists). Same storage pattern as the INSFOR faction definitions: the generated YAML
/// is kept verbatim as a blob, keyed by (kind, file stem).
///
/// <para>
/// Flow: every file write/delete also upserts/deletes its DB row. On startup, any row whose file is
/// missing (fresh container) is restored to disk BEFORE the client content pack is built, and its
/// prototypes are hot-loaded via <c>LoadString</c> + <c>ResolveResults</c> so the server has them
/// this boot too - no "one restart behind" gap after a redeploy.
/// </para>
/// </summary>
public sealed partial class CustomConstructionMenuSystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IGamePrototypeLoadManager _protoLoad = default!;

    // DB "kind" = generated subdirectory ("" is the root entries dir). Mirrors the file layout.
    private const string DbKindEntries = "";
    private const string DbKindTiles = "Tiles";
    private const string DbKindLathe = "Lathe";
    private const string DbKindOverrides = "Overrides";

    /// <summary>
    /// YAML restored from the DB during Initialize that still needs its client broadcast. During system init
    /// the upload manager is not initialized yet (SendGamePrototype would NRE) and, worse, it reloads ALL
    /// localizations per call - doing that once per restored entry made a large mass-editor batch stall
    /// startup for minutes, long enough for connecting clients to hit an engine crash. So restore batches
    /// everything into ONE document and publishes it on the first Update tick instead.
    /// </summary>
    private string? _pendingRestorePublish;

    /// <summary>False until the first Update tick; while false, PublishYaml queues the client broadcast
    /// into <see cref="_pendingRestorePublish"/> instead of calling the (not yet initialized) upload manager.</summary>
    private bool _publishReady;

    /// <summary>
    /// Restores DB-stored entries whose files are missing and hot-loads their prototypes. Runs once
    /// at system init, before <see cref="ValidateExistingEntries"/>. The DB read blocks the (one-time)
    /// startup path deliberately: restored files must exist before clients connect and before the
    /// rest of init reads the generated directory.
    /// </summary>
    private void RestoreFromDatabase()
    {
        if (_generatedDir == null)
            return;

        List<AU14CustomConstructionEntry> rows;
        try
        {
            // Task.Run is REQUIRED, not just politeness: the game thread has a sync-context, so awaiting
            // the manager's async wrapper directly would queue its continuation back onto this (blocked)
            // thread - a guaranteed startup deadlock. Inside Task.Run every continuation stays on the
            // thread pool, so the one-time blocking wait below completes normally.
            rows = Task.Run(() => _db.GetCustomConstructionEntries()).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read custom construction entries from the database: {e}");
            return;
        }

        var restored = 0;
        var anyLathe = false;
        var loaded = 0;
        var combined = new StringBuilder();
        foreach (var row in rows)
        {
            var path = DbEntryFilePath(row.Kind, row.EntryKey);
            if (path == null)
                continue;

            // Bad-data quarantine, applied to EVERY kind: oversized rows (or, for entries, unsafe ones)
            // are skipped AND deleted so a corrupt/hostile row can never crash or stall startup again.
            if (IsOversizedYaml(row.Yaml, out var sizeReason))
            {
                Log.Error($"Skipping oversized custom construction entry {row.Kind}/{row.EntryKey}: {sizeReason}. The DB row will be removed.");
                DbDelete(row.Kind, row.EntryKey);
                continue;
            }

            if (row.Kind == DbKindEntries && IsUnsafeGeneratedEntryYaml(row.Yaml, out var reason))
            {
                Log.Error($"Skipping unsafe custom construction entry {row.Kind}/{row.EntryKey}: {reason}. The DB row will be removed so startup cannot crash on it again.");
                DbDelete(row.Kind, row.EntryKey);

                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception e)
                {
                    Log.Warning($"Failed to delete unsafe restored custom construction file {path}: {e}");
                }

                continue;
            }

            try
            {
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, row.Yaml, Encoding.UTF8);
                    restored++;
                }

                // ALWAYS load, even when the file already exists: the DB is the source of truth. On
                // servers whose resource VFS doesn't pick the written files up (packaged/Docker), the
                // disk copy alone never loads - and plain LoadString would leave every connecting CLIENT
                // without the prototypes (=> "Unknown LatheRecipePrototype" in the lathe UI). Each row is
                // loaded server-side here; the CLIENT replay goes through one combined publish on the
                // first Update tick (see _pendingRestorePublish) - per-row publishing reloaded all
                // localizations once per entry, which stalled startup for minutes on big batches.
                _prototype.LoadString(row.Yaml, overwrite: true);
                loaded++;
                combined.AppendLine(row.Yaml);
                combined.AppendLine();
                anyLathe |= row.Kind == DbKindLathe;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to restore custom construction entry {row.Kind}/{row.EntryKey}: {e}");
            }
        }

        // One resolve pass for the whole batch (ResolveResults walks every loaded prototype, so doing it
        // per row would be O(rows * prototypes)).
        if (loaded > 0)
        {
            try
            {
                _prototype.ResolveResults();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to resolve restored custom construction prototypes: {e}");
            }

            _pendingRestorePublish = combined.ToString();
        }

        // The lathe pack files are derived from the recipe files, so rebuild and publish them (the packs
        // themselves are not stored in the DB).
        if (anyLathe)
            RegenerateLathePacks();

        if (restored > 0)
            Log.Info($"Restored {restored} custom construction entries from the database.");
    }

    /// <summary>
    /// Flushes the one combined restore document to the prototype-upload channel once the engine is fully
    /// initialized (first Update tick). This is what replays DB-restored prototypes to every current and
    /// late-joining client; calling it during Initialize is both broken (upload manager not initialized)
    /// and pathologically slow per entry.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _publishReady = true;
        if (_pendingRestorePublish is not { } pending)
            return;

        _pendingRestorePublish = null;
        try
        {
            _protoLoad.SendGamePrototype(pending);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to broadcast restored custom construction prototypes to clients: {e}");
        }
    }

    /// <summary>
    /// Hot-loads generated prototype YAML on the server AND every connected client, and queues it for
    /// late joiners (same channel the admin <c>loadprototype</c> command uses). This is what makes edits
    /// apply without a full rebuild and what keeps clients in sync with DB-restored entries.
    /// </summary>
    private bool PublishYaml(string yaml, string what)
    {
        try
        {
            _prototype.LoadString(yaml, overwrite: true);
            _prototype.ResolveResults();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load generated prototypes ({what}): {e}");
            return false;
        }

        if (!_publishReady)
        {
            // Startup: the upload manager isn't initialized yet, so queue for the first-tick flush.
            _pendingRestorePublish = (_pendingRestorePublish ?? string.Empty) + yaml + "\n";
            return true;
        }

        try
        {
            _protoLoad.SendGamePrototype(yaml);
        }
        catch (Exception e)
        {
            Log.Debug($"Generated prototypes loaded server-side but could not be queued for live client broadcast yet ({what}): {e}");
        }

        return true;
    }

    /// <summary>
    /// Server-side unload of the prototypes defined in generated YAML (used when an entry is deleted, so
    /// the removal applies this round instead of "after the next full restart"). Clients cannot unload
    /// prototypes at runtime; menu-visible leftovers are handled by hiding the recipe id instead.
    /// </summary>
    private void UnloadYaml(string yaml, string what)
    {
        try
        {
            _prototype.RemoveString(yaml);
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to unload generated prototypes ({what}): {e}");
        }
    }

    /// <summary>Maps a DB row back to its file path: &lt;generated&gt;/&lt;kind&gt;/&lt;stem&gt;.yml.</summary>
    private string? DbEntryFilePath(string kind, string stem)
    {
        if (_generatedDir == null)
            return null;

        // Defence in depth: the stem is one of our sanitized keys, never a path. Reject anything else.
        if (string.IsNullOrWhiteSpace(stem) || stem != Path.GetFileName(stem))
            return null;

        var dir = string.IsNullOrEmpty(kind) ? _generatedDir : Path.Combine(_generatedDir, kind);
        return Path.Combine(dir, $"{stem}.yml");
    }

    /// <summary>
    /// Mirrors a file write into the DB. Fire-and-forget: the file already succeeded, so a DB
    /// hiccup only costs redeploy-durability, never the in-game action - but it is always logged.
    /// </summary>
    private void DbUpsert(string kind, string stem, string yaml)
    {
        LogDbFailure(_db.UpsertCustomConstructionEntry(kind, stem, yaml), "save", kind, stem);
    }

    /// <summary>Mirrors a file delete into the DB (missing rows are fine, e.g. pre-DB entries).</summary>
    private void DbDelete(string kind, string stem)
    {
        LogDbFailure(_db.DeleteCustomConstructionEntry(kind, stem), "delete", kind, stem);
    }

    private void LogDbFailure(Task task, string action, string kind, string stem)
    {
        task.ContinueWith(
            t => Log.Error($"Failed to {action} custom construction entry {kind}/{stem} in the database: {t.Exception}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
