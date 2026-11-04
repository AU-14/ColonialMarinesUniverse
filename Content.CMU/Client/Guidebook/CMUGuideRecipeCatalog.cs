using System.Linq;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Assembly;
using Content.Shared.CMU14.Crafting;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Forge;
using Content.Shared.CMU14.Grinding;
using Content.Shared.CMU14.Mining;
using Content.Shared.CMU14.Smelting;
using Content.Shared.CMU14.Steam;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Guidebook;

public enum CMUGuideMaterialKind : byte
{
    Item,
    Stack,
    Fluid,
}

/// <summary>A lookup key, preserving the distinction between an exact item and any stack of a material.</summary>
public readonly record struct CMUGuideMaterial(CMUGuideMaterialKind Kind, string Id)
{
    public static CMUGuideMaterial Item(EntProtoId id) => new(CMUGuideMaterialKind.Item, id.Id);
    public static CMUGuideMaterial Stack(ProtoId<StackPrototype> id) => new(CMUGuideMaterialKind.Stack, id.Id);
    public static CMUGuideMaterial Fluid(string id) => new(CMUGuideMaterialKind.Fluid, id);
}

public sealed record CMUGuideIngredient(CMUGuideMaterial Material, double? Amount = 1, bool Kept = false);

public sealed class CMUGuideRecipe
{
    public required string Id;
    public required string Method;
    public required string Instructions;
    public List<CMUGuideIngredient> Outputs = new();
    public List<CMUGuideIngredient> Inputs = new();
    public List<EntProtoId> Equipment = new();
    public CraftingRecipePrototype? HandRecipe;
    public double? Seconds;
    public bool CanPlan = true;
    public string Name = string.Empty;
    public string Search = string.Empty;
}

/// <summary>
/// Read-only acquisition index built from loaded prototypes, never from live entities or sampled loot rolls.
/// Each guide widget builds it once; textures are resolved only when the selected entry is rendered.
/// </summary>
public sealed partial class CMUGuideRecipeCatalog
{
    private static readonly ProtoId<ToolQualityPrototype> HammerQuality = "Hammering";
    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototypes;
    private readonly EntityPrototype[] _equipment;
    private readonly Dictionary<string, CMUGuideRecipe> _entries = new();
    private readonly Dictionary<CMUGuideMaterial, HashSet<string>> _sources = new();
    private readonly HashSet<CMUGuideMaterial> _materials = new();
    private readonly Dictionary<Type, List<(EntProtoId Id, Component Component)>> _components = new();

    public IEnumerable<CMUGuideRecipe> Entries => _entries.Values;

    public CMUGuideRecipeCatalog(IEntityManager entities, IPrototypeManager prototypes)
    {
        _entities = entities;
        _prototypes = prototypes;
        _equipment = prototypes.EnumeratePrototypes<EntityPrototype>().Where(p => !p.Abstract).ToArray();
        AddProduction();
        AddGathering();
    }

    public HashSet<string>? Sources(CMUGuideMaterial material) => _sources.GetValueOrDefault(material);

    public string Name(CMUGuideMaterial material) => material.Kind switch
    {
        CMUGuideMaterialKind.Stack => _prototypes.Index(_prototypes.Index<StackPrototype>(material.Id).Spawn).Name,
        CMUGuideMaterialKind.Fluid => _entities.System<RMCReagentSystem>().Index(material.Id).LocalizedName,
        _ => _prototypes.Index<EntityPrototype>(material.Id).Name,
    };

    public SpriteSpecifier? Icon(CMUGuideMaterial material) => material.Kind switch
    {
        CMUGuideMaterialKind.Stack => _prototypes.Index<StackPrototype>(material.Id).Icon ??
            new SpriteSpecifier.EntityPrototype(_prototypes.Index<StackPrototype>(material.Id).Spawn),
        CMUGuideMaterialKind.Fluid => null,
        _ => new SpriteSpecifier.EntityPrototype(material.Id),
    };

    private List<EntProtoId> With<T>(Func<T, bool>? predicate = null) where T : Component, new()
    {
        if (!_components.TryGetValue(typeof(T), out var candidates))
        {
            candidates = new();
            foreach (var entity in _equipment)
            {
                if (entity.TryComp<T>(out var component, _entities.ComponentFactory))
                    candidates.Add((entity.ID, component));
            }
            _components.Add(typeof(T), candidates);
        }
        return candidates.Where(p => predicate == null || predicate((T) p.Component)).Select(p => p.Id).ToList();
    }

    private static CMUGuideIngredient Item(EntProtoId id, double? amount = 1, bool kept = false) =>
        new(CMUGuideMaterial.Item(id), amount, kept);

    private static CMUGuideIngredient Stack(ProtoId<StackPrototype> id, double? amount = 1, bool kept = false) =>
        new(CMUGuideMaterial.Stack(id), amount, kept);

    private static CMUGuideIngredient Fluid(string id, double? amount = 1) =>
        new(CMUGuideMaterial.Fluid(id), amount);

    private static List<CMUGuideIngredient> Stacks(Dictionary<ProtoId<StackPrototype>, int> stacks) =>
        stacks.Select(p => Stack(p.Key, p.Value)).ToList();

    private void Add(CMUGuideRecipe entry)
    {
        if (entry.Outputs.Count == 0 || !_entries.TryAdd(entry.Id, entry))
            return;

        var output = entry.HandRecipe is { } hand ? Loc.GetString(hand.Name) :
            string.Join(" + ", entry.Outputs.Select(p => Name(p.Material)).Distinct());
        entry.Name = Loc.GetString("cmu-industry-source-title", ("name", output), ("method", entry.Method));
        entry.Search = string.Join(" ", entry.Inputs.Select(p => Name(p.Material))
            .Concat(entry.Equipment.Select(p => _prototypes.Index(p).Name)).Prepend(entry.Name));

        foreach (var material in entry.Inputs.Concat(entry.Outputs).Select(p => p.Material))
        {
            _materials.Add(material);
            if (material.Kind == CMUGuideMaterialKind.Item &&
                _prototypes.Index<EntityPrototype>(material.Id).TryComp<StackComponent>(out var stack, _entities.ComponentFactory))
                _materials.Add(CMUGuideMaterial.Stack(stack.StackTypeId));
        }

        foreach (var product in entry.Outputs)
        {
            Index(product.Material, entry.Id);
            // A recipe that spawns a stack also satisfies ingredients accepting any stack of that type.
            // A stack recipe only satisfies exact-item ingredients for the prototype it actually spawns.
            if (product.Material.Kind == CMUGuideMaterialKind.Item &&
                _prototypes.Index<EntityPrototype>(product.Material.Id).TryComp<StackComponent>(out var stack, _entities.ComponentFactory))
                Index(CMUGuideMaterial.Stack(stack.StackTypeId), entry.Id);
            else if (product.Material.Kind == CMUGuideMaterialKind.Stack)
                Index(CMUGuideMaterial.Item(_prototypes.Index<StackPrototype>(product.Material.Id).Spawn), entry.Id);
        }
    }

    private void Index(CMUGuideMaterial material, string id)
    {
        if (!_sources.TryGetValue(material, out var entries))
            _sources.Add(material, entries = new HashSet<string>());
        entries.Add(id);
    }

    private CMUGuideRecipe Recipe(string id, string method, string instructions,
        List<CMUGuideIngredient> outputs, List<CMUGuideIngredient>? inputs = null,
        List<EntProtoId>? equipment = null, double? seconds = null) => new()
    {
        Id = id,
        Method = Loc.GetString("cmu-industry-source-" + method),
        Instructions = Loc.GetString("cmu-industry-source-" + instructions),
        Outputs = outputs,
        Inputs = inputs ?? new(),
        Equipment = equipment ?? new(),
        Seconds = seconds,
    };

    private void AddProduction()
    {
        foreach (var recipe in _prototypes.EnumeratePrototypes<CraftingRecipePrototype>())
        {
            var entry = Recipe(recipe.ID, "hand", "hand-help", new() { Item(recipe.Output, recipe.OutputCount) },
                recipe.Consumables.Select(p => p.Stack is { } s ? Stack(s, p.Amount) : Item(p.Item!.Value, p.Amount)).ToList(),
                seconds: recipe.Time.TotalSeconds);
            entry.HandRecipe = recipe;
            Add(entry);
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<MachineRecipePrototype>())
        {
            var machines = With<RecipeMachineComponent>(m => m.Kind == recipe.Machine &&
                (recipe.Circuit == null || m.MaxCircuit >= recipe.Circuit));
            if (machines.Count == 0)
                continue;
            var inputs = Stacks(recipe.Inputs);
            inputs.AddRange(recipe.ItemInputs.Select(p => Item(p.Key, p.Value)));
            inputs.AddRange(recipe.FluidInputs.Select(p => Fluid(p.Key, p.Value)));
            inputs.AddRange(recipe.Catalysts.Select(p => Stack(p.Key, p.Value, true)));
            var outputs = Stacks(recipe.Outputs);
            outputs.AddRange(recipe.ItemOutputs.Select(p => Item(p.Key, p.Value)));
            outputs.AddRange(recipe.FluidOutputs.Select(p => Fluid(p.Key, p.Value)));
            var entry = Recipe("machine:" + recipe.ID, "machine", "machine-help", outputs, inputs, machines, recipe.Duration);
            entry.Method = string.Join(" / ", machines.Select(m => _prototypes.Index(m).Name).Distinct());
            if (recipe.Circuit is { } circuit)
                entry.Instructions += " " + Loc.GetString("cmu-industry-source-circuit", ("circuit", circuit));
            Add(entry);
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<GrinderRecipePrototype>())
        {
            var outputs = new List<CMUGuideIngredient> { Stack(recipe.Result, recipe.ResultAmount) };
            outputs.AddRange(recipe.Extras.Select(p => Stack(p.Result, p.Amount)));
            var inputs = new List<CMUGuideIngredient> { Stack(recipe.Input, recipe.InputAmount) };
            Add(Recipe("grind:" + recipe.ID, "grinder", "grinder-help", outputs, inputs, With<GrinderComponent>()));

            var ore = _prototypes.Index(_prototypes.Index(recipe.Input).Spawn);
            if (ore.TryComp<GroundStackComponent>(out _, _entities.ComponentFactory))
            {
                // HammerCrushSystem uses two thirds of each grinder output, rounded down, with a minimum of one.
                var crushed = outputs.Select(p => p with { Amount = Math.Max(1, (int) p.Amount!.Value * 2 / 3) }).ToList();
                Add(Recipe("crush:" + recipe.ID, "crush", "crush-help", crushed, inputs,
                    new() { _prototypes.Index(HammerQuality).Spawn }));
            }
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<AlloyRecipePrototype>())
        {
            var entry = Recipe("alloy:" + recipe.ID, "alloy", "alloy-help",
                new() { Stack(recipe.Result, recipe.ResultAmount) }, Stacks(recipe.Materials),
                With<ElectricSmelterComponent>(m => m.MaxTemperature >= recipe.MinTemperature), recipe.Duration.TotalSeconds);
            entry.Instructions += " " + Loc.GetString("cmu-industry-source-temperature", ("temperature", recipe.MinTemperature));
            Add(entry);
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<PressRecipePrototype>())
        {
            var inputs = Stacks(recipe.Inputs);
            inputs.AddRange(recipe.Fluids.Select(p => Fluid(p.Key, p.Value)));
            AddMold(inputs, recipe.Shape);
            Add(Recipe("press:" + recipe.ID, "press", "press-help", new() { Stack(recipe.Result, recipe.ResultAmount) },
                inputs, With<SolidFuelPressComponent>(), recipe.Duration));
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<SteamSmeltRecipePrototype>())
        {
            var inputs = Stacks(recipe.Inputs);
            AddMold(inputs, recipe.Shape);
            foreach (var producesSlag in new[] { true, false })
            {
                var smelters = With<SteamSmelterComponent>(m => m.ProducesSlag == producesSlag);
                if (smelters.Count == 0)
                    continue;
                var outputs = new List<CMUGuideIngredient> { Stack(recipe.Result, recipe.ResultAmount) };
                if (producesSlag && recipe.Slag > 0)
                    outputs.Add(Fluid("RUMCSlag", recipe.Slag));
                Add(Recipe($"steam:{recipe.ID}:{producesSlag}", "steam", "steam-help", outputs, inputs, smelters, recipe.Duration));
            }
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<CondenserRecipePrototype>())
        {
            var inputs = new List<CMUGuideIngredient>
            {
                Fluid(recipe.Liquid, recipe.LiquidAmount),
                Fluid(recipe.Coolant, recipe.CoolantAmount),
            };
            if (recipe.Material is { } material)
                inputs.Add(Stack(material, recipe.MaterialAmount));
            Add(Recipe("condense:" + recipe.ID, "condenser", "condenser-help",
                new() { Stack(recipe.Result, recipe.ResultAmount) }, inputs, With<CondenserComponent>(), recipe.Duration));
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<AnvilRecipePrototype>())
        {
            // Unrestricted recipes accept any forgeable workpiece, not every arbitrary metal stack.
            var ingots = With<AnvilWorkpieceComponent>().Where(id => recipe.Ingot == null ||
                (_prototypes.Index(id).TryComp<StackComponent>(out var s, _entities.ComponentFactory) && s.StackTypeId == recipe.Ingot));
            foreach (var ingot in ingots)
            {
                var entry = Recipe($"anvil:{recipe.ID}:{ingot}", "anvil", "anvil-help",
                    new() { Item(recipe.Output, recipe.OutputCount) }, new() { Item(ingot) }, With<AnvilComponent>());
                entry.Method += " · " + _prototypes.Index(ingot).Name;
                entry.Instructions += " " + Loc.GetString("cmu-industry-source-strikes",
                    ("tools", string.Join(" → ", recipe.Steps.Select(s => Loc.GetString(_prototypes.Index(s).ToolName)))));
                Add(entry);
            }
        }

        foreach (var recipe in _prototypes.EnumeratePrototypes<AssemblyRecipePrototype>())
        {
            var inputs = recipe.Layout.Values.GroupBy(p => p.Material).Select(g => Stack(g.Key, g.Sum(p => p.Amount))).ToList();
            var blueprints = With<AssemblyBlueprintComponent>();
            if (blueprints.Count > 0)
                inputs.Add(Item(blueprints[0], kept: true));
            var entry = Recipe("assembly:" + recipe.ID, "assembly", "assembly-help",
                new() { Item(recipe.Result, recipe.ResultAmount) }, inputs, With<AssemblyMachineComponent>(), recipe.AssembleTime.TotalSeconds);
            entry.Instructions += " " + string.Join("; ", recipe.Layout.OrderBy(p => p.Key).Select(p =>
                Loc.GetString("cmu-industry-source-assembly-slot", ("slot", p.Key + 1),
                    ("count", p.Value.Amount), ("name", Name(CMUGuideMaterial.Stack(p.Value.Material))))));
            Add(entry);
        }

        foreach (var entity in _equipment)
        {
            if (entity.TryComp<MoldComponent>(out var mold, _entities.ComponentFactory))
            {
                for (var i = 0; i < mold.Recipes.Count; i++)
                {
                    var recipe = mold.Recipes[i];
                    Add(Recipe($"cast:{entity.ID}:{i}", "cast", "cast-help", new() { Item(recipe.Output, recipe.OutputCount) },
                        recipe.Ingredients.Select(p => Fluid(p.Key.Id, p.Value.Float())).ToList(), new() { entity.ID }, recipe.CoolTime.TotalSeconds));
                }
            }
            if (entity.TryComp<ForgeComponent>(out var forge, _entities.ComponentFactory))
            {
                foreach (var ingredient in forge.Ingredients)
                {
                    var entry = Recipe($"melt:{entity.ID}:{ingredient.Input}", "melt", "melt-help",
                        new() { Fluid(ingredient.Reagent.Id, ingredient.ReagentPerUnit.Float()) },
                        new() { Stack(ingredient.Input) }, new() { entity.ID });
                    if (_entities.System<RMCReagentSystem>().Index(ingredient.Reagent).MeltingPoint is { } temperature)
                        entry.Instructions += " " + Loc.GetString("cmu-industry-source-temperature", ("temperature", temperature));
                    Add(entry);
                }
            }
            if (entity.TryComp<SmeltingPotComponent>(out var pot, _entities.ComponentFactory))
            {
                for (var i = 0; i < pot.Recipes.Count; i++)
                {
                    var recipe = pot.Recipes[i];
                    var inputs = new List<CMUGuideIngredient>();
                    if (recipe.Input is { } input)
                        inputs.Add(Stack(input, recipe.InputAmount));
                    var entry = Recipe($"pot:{entity.ID}:{i}", "pot", "pot-help", new() { Stack(recipe.Output, recipe.OutputAmount) },
                        inputs, new() { entity.ID }, recipe.Duration.TotalSeconds);
                    if (recipe.InputAnyElectronics)
                        entry.Instructions += " " + Loc.GetString("cmu-industry-source-electronics", ("count", recipe.InputAmount));
                    Add(entry);
                }
            }
        }
    }

    private void AddMold(List<CMUGuideIngredient> inputs, string shape)
    {
        if (string.IsNullOrEmpty(shape))
            return;
        // Molds of a shape are interchangeable; show one clickable example rather than requiring all variants.
        var molds = With<PressMoldComponent>(m => m.Shape == shape);
        if (molds.Count > 0)
            inputs.Add(Item(molds.OrderBy(m => m.Id, StringComparer.Ordinal).First(), kept: true));
    }
}
