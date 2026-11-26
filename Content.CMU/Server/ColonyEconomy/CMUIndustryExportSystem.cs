using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Cargo;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Government;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>ASRS appraisal for industry reservoirs and otherwise unpriced world-trade stacks.</summary>
public sealed partial class CMUIndustryExportSystem : EntitySystem
{
    private static readonly HashSet<string> ExportableFluids = new(StringComparer.Ordinal)
    {
        "RUMCCrudeOil",
        "RUMCNaphtha",
        "RUMCLightFuel",
        "RUMCHeavyFuel",
        "RUMCDiesel",
    };

    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private readonly Dictionary<EntProtoId, double> _entityPrices = new();
    private readonly Dictionary<ProtoId<StackPrototype>, double> _stackPrices = new();
    private readonly Dictionary<string, double> _namePrices = new(StringComparer.OrdinalIgnoreCase);

    public override void Initialize()
    {
        BuildCanonicalPriceCache();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<ASRSSellableDebugToolComponent, AfterInteractEvent>(OnDebugToolAfterInteract);
        SubscribeLocalEvent<ASRSSellableComponent, PriceCalculationEvent>(OnSellablePrice);
        SubscribeLocalEvent<FluidTankComponent, PriceCalculationEvent>(OnTankPrice);
        SubscribeLocalEvent<StackComponent, PriceCalculationEvent>(OnStackPrice);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUIndustryPricePrototype>() || args.WasModified<StackPrototype>() ||
            args.WasModified<EntityPrototype>())
            BuildCanonicalPriceCache();
    }

    private void BuildCanonicalPriceCache()
    {
        _entityPrices.Clear();
        _stackPrices.Clear();
        _namePrices.Clear();

        foreach (var price in ProtoMan.EnumeratePrototypes<CMUIndustryPricePrototype>())
            _namePrices[NormalizeProductName(price.Product)] = price.Price;

        foreach (var stack in ProtoMan.EnumeratePrototypes<StackPrototype>())
        {
            if (!_namePrices.TryGetValue(NormalizeProductName(stack.Name), out var price))
                continue;

            _stackPrices[stack.ID] = price;
            if (!string.IsNullOrWhiteSpace(stack.Spawn.Id))
                _entityPrices[stack.Spawn] = price;
        }

        foreach (var prototype in ProtoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (_namePrices.TryGetValue(NormalizeProductName(prototype.Name), out var price))
                _entityPrices[prototype.ID] = price;
        }

        // Generated explicit bindings override display-name matching. Some legacy
        // stacks use generic names such as "metal" while the industry catalog
        // intentionally exposes the material as "Steel".
        foreach (var price in ProtoMan.EnumeratePrototypes<CMUIndustryPricePrototype>())
        {
            foreach (var stackId in price.Stacks)
            {
                _stackPrices[stackId] = price.Price;
                var stack = ProtoMan.Index(stackId);
                if (!string.IsNullOrWhiteSpace(stack.Spawn.Id))
                    _entityPrices[stack.Spawn] = price.Price;
            }

            foreach (var entityId in price.Entities)
                _entityPrices[entityId] = price.Price;
        }

        Log.Info($"Loaded {_namePrices.Count} canonical RUMC prices; resolved {_stackPrices.Count} stacks and {_entityPrices.Count} entities.");
    }

    public bool TryGetCanonicalUnitPrice(EntityUid uid, out double price)
    {
        if (TryComp<StackComponent>(uid, out var stack) && _stackPrices.TryGetValue(stack.StackTypeId, out price))
            return true;

        if (MetaData(uid).EntityPrototype is { } prototype && _entityPrices.TryGetValue(prototype.ID, out price))
            return true;

        return _namePrices.TryGetValue(NormalizeProductName(Name(uid)), out price);
    }

    private static string NormalizeProductName(string name)
        => string.Join(' ', name.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));

    private void OnSellablePrice(Entity<ASRSSellableComponent> sellable, ref PriceCalculationEvent args)
    {
        if (args.Handled || !TryGetCanonicalUnitPrice(sellable, out var price))
            return;

        if (TryComp<StackComponent>(sellable, out var stack))
            price *= stack.Count;

        args.Price = price;
        args.Handled = true;
    }

    private void OnDebugToolAfterInteract(Entity<ASRSSellableDebugToolComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        var targetName = Name(target);
        if (HasComp<ASRSSellableComponent>(target))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-asrs-debug-tool-already-sellable", ("target", targetName)),
                target,
                args.User);
            return;
        }

        EnsureComp<ASRSSellableComponent>(target);
        _popup.PopupEntity(
            Loc.GetString("cmu-asrs-debug-tool-applied", ("target", targetName)),
            target,
            args.User);
    }

    public bool CanExport(EntityUid uid)
    {
        if (!HasComp<ASRSSellableComponent>(uid))
            return false;

        // A tank's marker only makes the container eligible for appraisal; its
        // contents must still be an allowed fuel/oil. This prevents refinery
        // gas, ethylene, water, and arbitrary reagents from becoming sellable
        // merely because they were poured into an industry tank.
        if (TryComp<FluidTankComponent>(uid, out var tank) && HasComp<FluidFillableComponent>(uid))
            return tank.Contents.Count > 0 && tank.Contents.Keys.All(ExportableFluids.Contains);

        if (TryComp<SolutionManagerComponent>(uid, out var manager))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((uid, manager)))
            {
                foreach (var reagent in solution.Comp.Solution.Contents)
                {
                    if (!ExportableFluids.Contains(reagent.Reagent.Prototype.Id))
                        return false;
                }
            }

            // Empty manufactured containers remain tangible exports. Once
            // filled, every reagent must be an eligible fuel or oil.
            return true;
        }

        return true;
    }

    /// <summary>Crates keep their fixed refund, with only industry exports paid on top.</summary>
    public double GetContainedExportPrice(EntityUid uid)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return 0;

        var price = 0.0;
        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (CanExport(item))
                    price += _pricing.GetPrice(item, includeContents: false);
                price += GetContainedExportPrice(item);
            }
        }
        return price;
    }

    private void OnTankPrice(Entity<FluidTankComponent> tank, ref PriceCalculationEvent args)
    {
        if (args.Handled)
            return;
        foreach (var (kind, amount) in tank.Comp.Contents)
        {
            if (ExportableFluids.Contains(kind) && _reagents.TryIndex(kind, out var reagent))
                args.Price += amount * reagent.PricePerUnit;
        }
    }

    private void OnStackPrice(Entity<StackComponent> stack, ref PriceCalculationEvent args)
    {
        // Existing material/static prices remain authoritative; the trade table fills missing valuations.
        if (args.Handled || HasComp<MaterialComponent>(stack) || HasComp<StackPriceComponent>(stack)
            || HasComp<StaticPriceComponent>(stack))
            return;
        args.Price += stack.Comp.Count * GetTradePrice(stack.Comp);
    }

    private int GetTradePrice(StackComponent stack)
    {
        var price = 0;
        foreach (var good in ProtoMan.EnumeratePrototypes<ColonyWorldGoodPrototype>())
        {
            if (good.Stack != stack.StackTypeId)
                continue;
            if (price == 0 || good.SellPrice < price)
                price = good.SellPrice;
        }
        return price;
    }
}
