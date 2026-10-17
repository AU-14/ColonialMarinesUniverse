// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Upload;

namespace Content.Server.CMU14.Construction.CustomConstruction;

/// <summary>Mirrors generated construction YAML into the database as a backup. Startup does not restore it.</summary>
public sealed partial class CustomConstructionMenuSystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IGamePrototypeLoadManager _protoLoad = default!;

    // DB "kind" = generated subdirectory ("" is the root entries dir). Mirrors the file layout.
    private const string DbKindEntries = "";
    private const string DbKindTiles = "Tiles";
    private const string DbKindLathe = "Lathe";
    private const string DbKindOverrides = "Overrides";

    // Startup-generated YAML waits until the upload manager is initialized.
    private string? _pendingPrototypePublish;

    /// <summary>False until the first Update tick; while false, PublishYaml queues the client broadcast
    /// into <see cref="_pendingPrototypePublish"/> instead of calling the (not yet initialized) upload manager.</summary>
    private bool _publishReady;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _publishReady = true;
        if (_pendingPrototypePublish is not { } pending)
            return;

        _pendingPrototypePublish = null;
        try
        {
            _protoLoad.SendGamePrototype(pending);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to broadcast generated custom construction prototypes to clients: {e}");
        }
    }

    /// <summary>
    /// Hot-loads generated prototype YAML on the server AND every connected client, and queues it for
    /// late joiners (same channel the admin <c>loadprototype</c> command uses). This is what makes edits
    /// apply without a full rebuild and keeps late-joining clients in sync.
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
            _pendingPrototypePublish = (_pendingPrototypePublish ?? string.Empty) + yaml + "\n";
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

    /// <summary>
    /// Mirrors a file write into the DB. Fire-and-forget: the file already succeeded, so a DB
    /// hiccup only costs the backup, never the in-game action - but it is always logged.
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
