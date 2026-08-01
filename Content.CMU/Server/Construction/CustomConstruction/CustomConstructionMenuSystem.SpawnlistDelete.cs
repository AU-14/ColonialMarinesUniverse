// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.IO;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._AU14.Construction.CustomConstruction;

/// <summary>
/// "Spawnlist Delete" tool (Admin Tools > Delete Spawnlist): removes a whole spawnlist - every generated
/// recipe filed under it, both construction ENTRIES and TILES - from disk, the database, the server's
/// loaded prototypes, and (via the hide-tombstone mechanism) from every open menu this round. Gated by its
/// own tool permission (<see cref="AU14ToolPermissions.SpawnlistDelete"/>) so it can be granted separately
/// from the editors that create recipes.
/// </summary>
public sealed partial class CustomConstructionMenuSystem
{
    private void InitializeSpawnlistDelete()
    {
        SubscribeNetworkEvent<RequestOpenSpawnlistDeleteEvent>(OnRequestOpenSpawnlistDelete);
        SubscribeNetworkEvent<DeleteSpawnlistEvent>(OnDeleteSpawnlist);
    }

    private void OnRequestOpenSpawnlistDelete(RequestOpenSpawnlistDeleteEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.SpawnlistDelete))
            return;

        var ev = new OpenSpawnlistDeleteEvent();
        foreach (var (_, info) in EnumerateAllGeneratedRecipes())
        {
            if (string.IsNullOrWhiteSpace(info.Spawnlist))
                continue;

            ev.SpawnlistCounts.TryGetValue(info.Spawnlist, out var count);
            ev.SpawnlistCounts[info.Spawnlist] = count + 1;

            // Per-category tally so the window can scope a delete down to one category.
            var category = string.IsNullOrWhiteSpace(info.Category) ? DefaultCategory : info.Category;
            if (!ev.CategoryCounts.TryGetValue(info.Spawnlist, out var byCategory))
                ev.CategoryCounts[info.Spawnlist] = byCategory = new Dictionary<string, int>();

            byCategory.TryGetValue(category, out var categoryCount);
            byCategory[category] = categoryCount + 1;
        }

        RaiseNetworkEvent(ev, session);
    }

    private void OnDeleteSpawnlist(DeleteSpawnlistEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.SpawnlistDelete))
            return;

        var spawnlist = (msg.Spawnlist ?? string.Empty).Trim();
        if (spawnlist.Length == 0)
            return;

        // Empty category = the whole spawnlist (original behaviour); otherwise scope to that one category
        // and delete everything filed under it.
        var category = (msg.Category ?? string.Empty).Trim();
        var wholeSpawnlist = category.Length == 0;

        var removed = 0;
        var failed = 0;
        foreach (var (file, info) in EnumerateAllGeneratedRecipes())
        {
            if (!string.Equals(info.Spawnlist, spawnlist, StringComparison.Ordinal))
                continue;

            if (!wholeSpawnlist)
            {
                var entryCategory = string.IsNullOrWhiteSpace(info.Category) ? DefaultCategory : info.Category;
                if (!string.Equals(entryCategory, category, StringComparison.Ordinal))
                    continue;
            }

            try
            {
                RetireGeneratedFile(file);
                removed++;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to delete generated recipe {file} while deleting spawnlist '{spawnlist}'"
                          + (wholeSpawnlist ? "" : $" category '{category}'") + $": {e}");
                failed++;
            }
        }

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{session.Name} DELETED {(wholeSpawnlist ? $"spawnlist '{spawnlist}'" : $"category '{category}' of spawnlist '{spawnlist}'")} ({removed} generated recipes removed, {failed} failed)");

        PopupTo(session,
            wholeSpawnlist
                ? Loc.GetString("construction-menu-spawnlist-deleted", ("spawnlist", spawnlist), ("count", removed))
                : Loc.GetString("construction-menu-spawnlist-category-deleted", ("spawnlist", spawnlist), ("category", category), ("count", removed)),
            PopupType.LargeCaution);
    }

    /// <summary>Every generated recipe file that carries a spawnlist: the construction entries in the
    /// Generated/ root and the tile recipes under Generated/Tiles/.</summary>
    private IEnumerable<(string File, EntryInfo Info)> EnumerateAllGeneratedRecipes()
    {
        if (_generatedDir == null)
            yield break;

        if (Directory.Exists(_generatedDir))
        {
            foreach (var file in Directory.EnumerateFiles(_generatedDir, "*.yml"))
            {
                if (ReadHeaders(file) is { } info)
                    yield return (file, info);
            }
        }

        if (TilesDir is { } tilesDir && Directory.Exists(tilesDir))
        {
            foreach (var file in Directory.EnumerateFiles(tilesDir, "*.yml"))
            {
                if (ReadHeaders(file) is { } info)
                    yield return (file, info);
            }
        }
    }

    /// <summary>
    /// Retires ONE generated recipe file of either kind (entry or tile): deletes the file, drops its DB
    /// mirror row (the kind is derived from the file's directory), unloads its prototypes server-side and
    /// hides its recipe id so it disappears from open menus this round.
    /// </summary>
    private void RetireGeneratedFile(string path)
    {
        var isTile = string.Equals(Path.GetDirectoryName(path), TilesDir, StringComparison.OrdinalIgnoreCase);
        if (!isTile)
        {
            RetireEntryFile(path);
            return;
        }

        var stem = Path.GetFileNameWithoutExtension(path);
        string? yaml = null;
        if (File.Exists(path))
        {
            yaml = File.ReadAllText(path);
            File.Delete(path);
        }

        DbDelete(DbKindTiles, stem);

        if (yaml != null)
            UnloadYaml(yaml, stem);

        HideRecipeId(stem);
    }
}
