using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Farming;
using Content.Shared.CMU14.Flora;
using Content.Shared.CMU14.Mining;
using Content.Shared.CMU14.Steam;
using Content.Shared.EntityTable;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mining;
using Content.Shared.Mining.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Guidebook;

public sealed partial class CMUGuideRecipeCatalog
{
    private void AddGathering()
    {
        // Only chemistry used by industry needs to be advertised here, not the full medical chemistry catalogue.
        var fluids = _materials.Where(m => m.Kind == CMUGuideMaterialKind.Fluid).ToHashSet();
        foreach (var entity in _equipment)
        {
            if (entity.TryComp<OreVeinComponent>(out var vein, _entities.ComponentFactory))
            {
                var ores = new HashSet<ProtoId<OrePrototype>>();
                if (vein.CurrentOre is { } ore)
                    ores.Add(ore);
                else if (vein.OreChance > 0 && vein.OreRarityPrototypeId is { } table)
                    ores.UnionWith(_prototypes.Index(table).Weights.Where(p => p.Value > 0).Select(p => p.Key));

                foreach (var oreId in ores)
                {
                    if (_prototypes.Index(oreId).OreEntity is not { } item)
                        continue;
                    var tree = entity.TryComp<TreeFellableComponent>(out _, _entities.ComponentFactory);
                    AddWorld($"vein:{entity.Name}:{oreId}", tree ? "chop" : "mine", tree ? "chop-help" : "mine-help",
                        Item(item, null), entity);
                }
            }

            if (entity.TryComp<OreDepositComponent>(out var deposit, _entities.ComponentFactory))
            {
                foreach (var ore in _prototypes.Index(deposit.Table).Ores.Where(p => p.Value > 0))
                {
                    AddWorld($"deposit:{entity.ID}:{ore.Key}", "drill", "drill-help", Stack(ore.Key, null), entity, With<OreDrillComponent>());
                }
            }

            if (entity.TryComp<TreeFellableComponent>(out var fellable, _entities.ComponentFactory))
                AddWorld($"firewood:{entity.Name}", "chop", "firewood-help", Item(fellable.Firewood, null), entity);

            if (entity.TryComp<TreeTappableComponent>(out var tappable, _entities.ComponentFactory))
            {
                AddWorld($"branch:{entity.Name}", "gather", "branch-help", Item(tappable.Branch, null), entity);
                AddWorld($"resin:{entity.Name}", "gather", "resin-help", Fluid(tappable.Resin.Id, null), entity);
            }

            if (entity.TryComp<LogSplittableComponent>(out var log, _entities.ComponentFactory))
                Add(Recipe("split:" + entity.ID, "chop", "split-help", new() { Item(log.Firewood, log.Pieces) }, new() { Item(entity.ID) }));

            if (entity.TryComp<ToolRefinableComponent>(out var refine, _entities.ComponentFactory))
            {
                var outputs = new List<CMUGuideIngredient>();
                foreach (var spawn in refine.RefineResult)
                {
                    if (spawn.PrototypeId is not { } item || spawn.SpawnProbability <= 0)
                        continue;
                    // Spawned stacks retain their prototype count (e.g. a log yields a ten-plank stack).
                    var count = _prototypes.Index(item).TryComp<StackComponent>(out var stack, _entities.ComponentFactory) ? stack.Count : 1;
                    outputs.Add(Item(item, spawn.SpawnProbability < 1 || spawn.MaxAmount > spawn.Amount || spawn.GroupId != null
                        ? null : spawn.Amount * count));
                }

                // Keep unrelated food and salvage recipes out of the industry finder.
                if (outputs.Any(p => IsIndustryMaterial(p.Material)))
                {
                    var entry = Recipe("refine:" + entity.ID, "refine", "refine-help", outputs, new() { Item(entity.ID) },
                        new() { _prototypes.Index(refine.QualityNeeded).Spawn }, refine.RefineTime.TotalSeconds);
                    if (entity.TryComp<ButcherableComponent>(out _, _entities.ComponentFactory))
                        entry.Instructions = Loc.GetString("cmu-industry-source-butcher-help");
                    Add(entry);
                }
            }

            if (entity.TryComp<FarmGatherableComponent>(out var plant, _entities.ComponentFactory) && plant.Loot != null)
            {
                // List possibilities without rolling loot or claiming that conditional/random drops are guaranteed.
                foreach (var item in plant.Loot.Values.SelectMany(t => t.ListSpawns(_entities, _prototypes, new EntityTableContext()))
                             .Where(p => p.prob > 0).Select(p => p.spawn).Distinct())
                    AddWorld($"gather:{entity.Name}:{item}", "gather", "gather-help", Item(item, null), entity);
            }

            if (entity.TryComp<OilPumpComponent>(out var oil, _entities.ComponentFactory))
                AddWorld("oil:" + entity.ID, "pump", "oil-help", Fluid(oil.Kind, null), entity);

            if (entity.TryComp<FluidPumpComponent>(out _, _entities.ComponentFactory))
                AddWorld("water:" + entity.ID, "pump", "water-help", Fluid("Water", null), entity);

            if (entity.TryComp<OreDrillComponent>(out var drill, _entities.ComponentFactory))
            {
                foreach (var rock in drill.RockTiles.Values.Distinct())
                    AddWorld($"floor:{entity.ID}:{rock}", "drill", "rock-help", Stack(rock, null), entity);
            }

            if (entity.TryComp<ExtractableComponent>(out var extractable, _entities.ComponentFactory))
            {
                if (extractable.JuiceSolution is { } juice)
                    AddExtraction(entity, juice, true, fluids);
                if (entity.TryComp<SolutionComponent>(out var solution, _entities.ComponentFactory) &&
                    solution.Id == extractable.GrindableSolutionName)
                    AddExtraction(entity, solution.Solution, false, fluids);
            }
        }
    }

    private void AddExtraction(EntityPrototype entity, Solution solution, bool juice, HashSet<CMUGuideMaterial> fluids)
    {
        var outputs = solution.Select(r => Fluid(r.Reagent.Prototype.Id, r.Quantity.Float())).ToList();
        if (!outputs.Any(p => fluids.Contains(p.Material)))
            return;
        Add(Recipe($"extract:{entity.ID}:{juice}", juice ? "juice" : "extract", juice ? "juice-help" : "extract-help",
            outputs, new() { Item(entity.ID) }, With<ReagentGrinderComponent>()));
    }

    private bool IsIndustryMaterial(CMUGuideMaterial material)
    {
        if (_materials.Contains(material))
            return true;
        return material.Kind == CMUGuideMaterialKind.Item &&
               _prototypes.Index<EntityPrototype>(material.Id).TryComp<StackComponent>(out var stack, _entities.ComponentFactory) &&
               _materials.Contains(CMUGuideMaterial.Stack(stack.StackTypeId));
    }

    private void AddWorld(string id, string method, string instructions, CMUGuideIngredient output, EntityPrototype source,
        List<EntProtoId>? tools = null)
    {
        var entry = Recipe(id, method, instructions, new() { output }, equipment: new() { source.ID });
        entry.Method += " · " + source.Name;
        entry.CanPlan = false;
        if (tools != null)
            entry.Equipment.AddRange(tools);
        Add(entry);
    }
}
