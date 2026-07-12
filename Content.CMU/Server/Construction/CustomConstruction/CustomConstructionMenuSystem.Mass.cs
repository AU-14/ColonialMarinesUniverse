// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Linq;
using System.Text;
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
    }

    private void OnRequestOpenMass(RequestOpenMassConstructionEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanEditConstructionMenu(session) || _generatedDir == null)
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
        if (!CanEditConstructionMenu(session) || _generatedDir == null)
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

        foreach (var id in msg.ProtoIds.Take(MaxMassEntities))
        {
            if (!seen.Add(id) || !_prototype.TryIndex<EntityPrototype>(id, out var proto) || proto.Abstract)
                continue;

            // Item vs structure differs per entity, so the recipe is validated against each one.
            var isItemRecipe = proto.TryGetComponent<ItemComponent>(out _, _componentFactory);
            if (!ValidateSteps(steps, isItemRecipe, out var invalidReason))
            {
                failed++;
                firstFailReason ??= invalidReason;
                continue;
            }

            var deconstructSteps = (isItemRecipe ? null : msg.DeconstructSteps) ?? new List<CustomConstructionStepData>();
            if (!ValidateDeconstructSteps(deconstructSteps, out var deconstructReason))
            {
                failed++;
                firstFailReason ??= deconstructReason;
                continue;
            }

            var key = MakeEntryKey(proto.ID, spawnlist, category);
            try
            {
                var yaml = BuildGeneratedYaml(proto, key, spawnlist, category, steps, deconstructSteps, msg.Health);
                File.WriteAllText(FilePathForKey(key), yaml, Encoding.UTF8);
                DbUpsert(DbKindEntries, $"{FilePrefix}{key}", yaml);
                PublishYaml(yaml, $"entry {key}");
                UnhideRecipeId($"{FilePrefix}{key}");
                added++;
            }
            catch (Exception e)
            {
                Log.Error($"Mass add failed for {proto.ID} (key {key}): {e}");
                failed++;
            }
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
