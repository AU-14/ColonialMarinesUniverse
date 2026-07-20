// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Database;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Construction.CustomConstruction;

/// <summary>
/// "Mass Entity Editor" half of the construction-menu editor: one recipe form applied to a whole batch of
/// entities at once. Every entity in the batch still gets its OWN independent generated entry file (identical
/// to adding them one-by-one), so any single one can be re-recipe'd or removed individually afterwards.
/// </summary>
public sealed partial class CustomConstructionMenuSystem
{
    // 🔧 TUNABLE: hard cap on entities per mass request. Guards against a malicious/buggy client submitting
    // the entire prototype set in one go (each entry is a file write + DB row + live prototype publish).
    // High enough that any single parent family (e.g. everything under BaseWall) fits in one batch.
    private const int MaxMassEntities = 4000;

    private void InitializeMass()
    {
        SubscribeNetworkEvent<RequestOpenMassConstructionEditorEvent>(OnRequestOpenMass);
        SubscribeNetworkEvent<SubmitMassConstructionEditorEvent>(OnSubmitMass);
        SubscribeNetworkEvent<SubmitMassTileEditorEvent>(OnSubmitMassTiles);
    }

    /// <summary>Tiles mode of the Mass Entity Editor: one cost/category fanned out to every selected tile,
    /// each as its own independent generated tile recipe (same output as the single Tiles Editor).</summary>
    private void OnSubmitMassTiles(SubmitMassTileEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Mass) || TilesDir == null)
            return;

        var material = Fallback(msg.Material, "CMSteel");
        if (!_prototype.HasIndex<Content.Shared.Stacks.StackPrototype>(material))
        {
            PopupTo(session, Loc.GetString("construction-menu-invalid-material", ("material", material)), PopupType.MediumCaution);
            return;
        }

        var amount = Math.Clamp(msg.Amount, 1, MaxStepAmount);
        var spawnlist = msg.ZLevelPage ? TilesSpawnlist : SanitizeName(msg.Spawnlist, DefaultSpawnlist);
        var category = SanitizeName(msg.Category, DefaultTileCategory);

        var added = 0;
        var failed = 0;
        var seen = new HashSet<string>();
        var preview = msg.Preview ? new OpenDbSavePreviewEvent { Kind = "construction-db-preview-kind-mass-tiles" } : null;
        // Batched publish: every generated document is folded into ONE combined publish at the end.
        // Publishing per tile reloads every localization each time, which hung the server mid-round.
        var combined = new StringBuilder();
        var recipeIds = new List<string>();

        if (preview == null)
        {
            try
            {
                Directory.CreateDirectory(TilesDir);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to create tiles dir for mass add: {e}");
                PopupTo(session, Loc.GetString("construction-menu-verb-add-failed"), PopupType.MediumCaution);
                return;
            }
        }

        foreach (var tileId in msg.TileIds.Take(MaxMassEntities))
        {
            if (!seen.Add(tileId) ||
                string.IsNullOrWhiteSpace(tileId) ||
                !_prototype.HasIndex<Content.Shared.Maps.ContentTileDefinition>(tileId))
            {
                failed++;
                preview?.Lines.Add($"REJECT '{tileId}' - unknown or duplicate tile");
                continue;
            }

            var key = $"{Sanitize(tileId)}__{Sanitize(spawnlist)}__{Sanitize(category)}";
            try
            {
                var yaml = BuildTileYaml(key, tileId, material, amount, spawnlist, category, msg.ZLevelPage);
                if (IsOversizedYaml(yaml, out var sizeReason))
                {
                    failed++;
                    preview?.Lines.Add($"REJECT '{tileId}' - {sizeReason}");
                    continue;
                }

                if (preview != null)
                {
                    preview.Lines.Add($"WRITE file Tiles/{TileFilePrefix}{key}.yml ({Encoding.UTF8.GetByteCount(yaml)} bytes) + UPSERT DB row (Tiles/{TileFilePrefix}{key})");
                    added++;
                    continue;
                }

                File.WriteAllText(Path.Combine(TilesDir, $"{TileFilePrefix}{key}.yml"), yaml, Encoding.UTF8);
                DbUpsert(DbKindTiles, $"{TileFilePrefix}{key}", yaml);
                combined.AppendLine(yaml);
                recipeIds.Add($"{TileFilePrefix}{key}");
                added++;
            }
            catch (Exception e)
            {
                Log.Error($"Mass tile add failed for {tileId} (key {key}): {e}");
                failed++;
            }
        }

        if (preview != null)
        {
            preview.Planned = added;
            preview.Rejected = failed;
            RaiseNetworkEvent(preview, session);
            return;
        }

        // ONE publish for the whole batch (recipes + the overrides unhide in the same document).
        if (recipeIds.Count > 0)
        {
            if (UnhideRecipeIdsPersist(recipeIds) is { } overridesYaml)
                combined.AppendLine(overridesYaml);
            PublishYaml(combined.ToString(), $"mass tiles ({added})");
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} mass-added {added} construction menu TILES ({failed} failed; spawnlist: {spawnlist}, category: {category}, cost: {amount} {material})");

        PopupTo(session,
            failed > 0
                ? Loc.GetString("construction-menu-mass-partial", ("added", added), ("failed", failed), ("reason", "invalid tiles"))
                : Loc.GetString("construction-menu-mass-tiles-added", ("added", added), ("category", category)),
            failed > 0 && added == 0 ? PopupType.MediumCaution : PopupType.Medium);
    }

    private void OnRequestOpenMass(RequestOpenMassConstructionEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Mass) || _generatedDir == null)
            return;

        // Server-side validation of the batch: drop unknown/abstract ids, dedupe, cap.
        var ids = new List<string>();
        var seen = new HashSet<string>();
        foreach (var id in msg.ProtoIds)
        {
            if (ids.Count >= MaxMassEntities)
                break;

            if (!seen.Add(id) || !_prototype.TryIndex<EntityPrototype>(id, out var proto) || proto.Abstract)
                continue;

            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            PopupTo(session, Loc.GetString("construction-menu-mass-none"), PopupType.MediumCaution);
            return;
        }

        var ev = new OpenMassConstructionEditorEvent
        {
            ProtoIds = ids,
            Editor = new OpenCustomConstructionEditorEvent
            {
                ProtoId = ids[0],
                ItemName = Loc.GetString("construction-menu-mass-item-name", ("count", ids.Count)),
                IsEdit = false,
                Spawnlist = DefaultSpawnlist,
                Category = DefaultCategory,
                Steps = DefaultSteps(),
                AvailableSpawnlists = EnumerateSpawnlists(),
                AvailableCategoriesBySpawnlist = EnumerateCategoriesBySpawnlist(),
            },
        };

        RaiseNetworkEvent(ev, session);
    }

    private void OnSubmitMass(SubmitMassConstructionEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanUseTool(session, AU14ToolPermissions.Mass) || _generatedDir == null)
            return;

        var steps = msg.Steps ?? new();
        if (steps.Count == 0)
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-bad-recipe"), PopupType.MediumCaution);
            return;
        }

        var spawnlist = SanitizeName(msg.Spawnlist, DefaultSpawnlist);
        var category = SanitizeName(msg.Category, DefaultCategory);

        var added = 0;
        var failed = 0;
        string? firstFailReason = null;
        var seen = new HashSet<string>();
        var preview = msg.Preview ? new OpenDbSavePreviewEvent { Kind = "construction-db-preview-kind-mass" } : null;
        // Batched publish: every generated document is folded into ONE combined publish at the end.
        // Publishing per entity reloads every localization each time, which hung the server mid-round.
        var combined = new StringBuilder();
        var recipeIds = new List<string>();

        if (preview == null)
        {
            try
            {
                Directory.CreateDirectory(_generatedDir);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to create generated dir for mass add: {e}");
                PopupTo(session, Loc.GetString("construction-menu-verb-add-failed"), PopupType.MediumCaution);
                return;
            }
        }

        foreach (var id in msg.ProtoIds.Take(MaxMassEntities))
        {
            if (!seen.Add(id) || !_prototype.TryIndex<EntityPrototype>(id, out var proto) || proto.Abstract)
                continue;

            if (IsGeneratedCustomEntityId(proto.ID))
            {
                failed++;
                firstFailReason ??= "generated custom entities cannot be added again";
                preview?.Lines.Add($"REJECT '{proto.ID}' - already a generated custom entity");
                continue;
            }

            // Item vs structure differs per entity, so the recipe is validated against each one.
            var isItemRecipe = proto.TryGetComponent<ItemComponent>(out _, _componentFactory);
            if (!ValidateSteps(steps, isItemRecipe, out var invalidReason))
            {
                failed++;
                firstFailReason ??= invalidReason;
                preview?.Lines.Add($"REJECT '{proto.ID}' - {invalidReason}");
                continue;
            }

            var deconstructSteps = (isItemRecipe ? null : msg.DeconstructSteps) ?? new List<CustomConstructionStepData>();
            if (!ValidateDeconstructSteps(deconstructSteps, out var deconstructReason))
            {
                failed++;
                firstFailReason ??= deconstructReason;
                preview?.Lines.Add($"REJECT '{proto.ID}' - {deconstructReason}");
                continue;
            }

            var key = MakeEntryKey(proto.ID, spawnlist, category);
            try
            {
                var yaml = BuildGeneratedYaml(proto, key, spawnlist, category, steps, deconstructSteps, msg.Health);
                if (IsUnsafeGeneratedEntryYaml(yaml, out var reason) || IsOversizedYaml(yaml, out reason))
                {
                    failed++;
                    firstFailReason ??= reason;
                    preview?.Lines.Add($"REJECT '{proto.ID}' - {reason}");
                    continue;
                }

                if (preview != null)
                {
                    preview.Lines.Add($"WRITE file {FilePrefix}{key}.yml ({Encoding.UTF8.GetByteCount(yaml)} bytes) + UPSERT DB row (entries/{FilePrefix}{key})");
                    added++;
                    continue;
                }

                File.WriteAllText(FilePathForKey(key), yaml, Encoding.UTF8);
                DbUpsert(DbKindEntries, $"{FilePrefix}{key}", yaml);
                combined.AppendLine(yaml);
                recipeIds.Add($"{FilePrefix}{key}");
                added++;
            }
            catch (Exception e)
            {
                Log.Error($"Mass add failed for {proto.ID} (key {key}): {e}");
                failed++;
            }
        }

        if (preview != null)
        {
            preview.Planned = added;
            preview.Rejected = failed;
            RaiseNetworkEvent(preview, session);
            return;
        }

        // ONE publish for the whole batch (recipes + the overrides unhide in the same document).
        if (recipeIds.Count > 0)
        {
            if (UnhideRecipeIdsPersist(recipeIds) is { } overridesYaml)
                combined.AppendLine(overridesYaml);
            PublishYaml(combined.ToString(), $"mass entries ({added})");
        }

        var recipeText = DescribeRecipe(steps);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} mass-added {added} construction menu items ({failed} failed; spawnlist: {spawnlist}, category: {category}, recipe: {recipeText})");

        if (failed > 0)
        {
            PopupTo(session,
                Loc.GetString("construction-menu-mass-partial",
                    ("added", added), ("failed", failed), ("reason", firstFailReason ?? string.Empty)),
                added > 0 ? PopupType.Medium : PopupType.MediumCaution);
        }
        else
        {
            PopupTo(session,
                Loc.GetString("construction-menu-mass-added",
                    ("added", added), ("category", category), ("recipe", recipeText)),
                PopupType.Medium);
        }
    }
}
