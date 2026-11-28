using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fluids;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

// ASRS prices for industry exports. everything here reads the cmuIndustryPrice registry and nothing else, so a
// thing pays only if it's listed: machine-made, mined or farmed goods (they carry ASRSSellable) and listed liquids
public sealed partial class CMUIndustryExportSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private PopupSystem _popup = default!;

    private readonly Dictionary<EntProtoId, double> _entityPrices = new();
    private readonly Dictionary<ProtoId<StackPrototype>, double> _stackPrices = new();
    private readonly Dictionary<string, double> _fluidPrices = new();
    private readonly HashSet<EntProtoId> _nonSellableEntities = new();
    private readonly HashSet<ProtoId<StackPrototype>> _nonSellableStacks = new();

    private readonly Dictionary<string, double> _roundSales = new();
    private const string MinedOrder = "mined:";

    public override void Initialize()
    {
        BuildPriceCaches();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _roundSales.Clear());
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<ASRSSellableDebugToolComponent, AfterInteractEvent>(OnDebugToolAfterInteract);
        SubscribeLocalEvent<ASRSSellableComponent, PriceCalculationEvent>(OnSellablePrice);
        SubscribeLocalEvent<FluidTankComponent, PriceCalculationEvent>(OnTankPrice);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUIndustryPricePrototype>())
            BuildPriceCaches();
    }

    private void BuildPriceCaches()
    {
        _entityPrices.Clear();
        _stackPrices.Clear();
        _fluidPrices.Clear();
        _nonSellableEntities.Clear();
        _nonSellableStacks.Clear();

        foreach (var price in ProtoMan.EnumeratePrototypes<CMUIndustryPricePrototype>())
        {
            if (!price.Sellable)
            {
                if (price.Entity is { } excludedEntity)
                    _nonSellableEntities.Add(excludedEntity);
                if (price.Stack is { } excludedStack)
                    _nonSellableStacks.Add(excludedStack);
                continue;
            }
            if (price.Stack is { } stack)
            {
                _stackPrices.Add(stack, price.Price);
            }
            if (price.Entity is { } entity)
                _entityPrices.Add(entity, price.Price);
            if (price.Reagent is { } reagent && price.Sellable)
                _fluidPrices.Add(reagent.Id, price.Price);
        }
    }

    public bool TryGetCanonicalUnitPrice(EntityUid uid, out double price)
    {
        if (TryComp<StackComponent>(uid, out var stack) && _stackPrices.TryGetValue(stack.StackTypeId, out price))
            return true;

        price = 0;
        return MetaData(uid).EntityPrototype is { } prototype && _entityPrices.TryGetValue(prototype.ID, out price);
    }

    public double GetFluidUnitPrice(string reagent) => _fluidPrices.GetValueOrDefault(reagent);

    public bool IsNonSellableIndustryProduct(EntityUid uid)
        => TryComp<StackComponent>(uid, out var stack) && _nonSellableStacks.Contains(stack.StackTypeId)
           || MetaData(uid).EntityPrototype is { } prototype &&
              (_nonSellableEntities.Contains(prototype.ID) ||
               prototype.ID.StartsWith("RUMCMachine", StringComparison.Ordinal) && !TryGetCanonicalUnitPrice(uid, out _));

    private void OnSellablePrice(Entity<ASRSSellableComponent> sellable, ref PriceCalculationEvent args)
    {
        if (args.Handled || !TryGetCanonicalUnitPrice(sellable, out var price))
            return;

        if (TryComp<StackComponent>(sellable, out var stack))
            price *= stack.Count;

        if (TryComp<FluidTankComponent>(sellable, out var tank) && HasComp<FluidFillableComponent>(sellable))
        {
            foreach (var (kind, amount) in tank.Contents)
                price += amount * GetFluidUnitPrice(kind);
        }
        if (HasComp<CMUIndustryExportComponent>(sellable))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((sellable.Owner, (SolutionManagerComponent?) null)))
            foreach (var reagent in solution.Comp.Solution.Contents)
                price += reagent.Quantity.Float() * GetFluidUnitPrice(reagent.Reagent.Prototype.Id);
        }

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
        if (!HasComp<ASRSSellableComponent>(uid) || IsNonSellableIndustryProduct(uid))
            return false;

        if (TryComp<FluidTankComponent>(uid, out var tank) && HasComp<FluidFillableComponent>(uid))
            return tank.Contents.Keys.All(kind => _fluidPrices.ContainsKey(kind));

        if (HasComp<CMUIndustryExportComponent>(uid))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((uid, (SolutionManagerComponent?) null)))
            foreach (var reagent in solution.Comp.Solution.Contents)
            {
                if (!_fluidPrices.ContainsKey(reagent.Reagent.Prototype.Id))
                    return false;
            }
        }

        return TryGetCanonicalUnitPrice(uid, out _);
    }

    // adds what this one entity exports by itself, not counting anything inside it
    public void CollectExports(EntityUid uid, List<IndustryExport> into)
    {
        if (!CanExport(uid))
            return;

        if (TryComp<ASRSSellableComponent>(uid, out var origin) && TryGetCanonicalUnitPrice(uid, out var unit))
        {
            if (TryComp<StackComponent>(uid, out var stack))
                into.Add(new IndustryExport((origin.FromOreDeposit ? MinedOrder : "stack:") + stack.StackTypeId, unit * stack.Count));
            else if (MetaData(uid).EntityPrototype is { } prototype)
                into.Add(new IndustryExport("entity:" + prototype.ID, unit));
        }

        if (TryComp<FluidTankComponent>(uid, out var tank) && HasComp<FluidFillableComponent>(uid))
        {
            foreach (var (kind, amount) in tank.Contents)
                AddFluid(kind, amount, into);
        }

        // a canister keeps its liquid in a solution on itself, it doesn't always have a solution manager
        if (HasComp<CMUIndustryExportComponent>(uid))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((uid, (SolutionManagerComponent?) null)))
            {
                foreach (var reagent in solution.Comp.Solution.Contents)
                    AddFluid(reagent.Reagent.Prototype, reagent.Quantity.Float(), into);
            }
        }
    }

    // Apply the common buyer's remaining order once per product, before the faction market quotes the load.
    public List<IndustryExport> FillRoundOrders(List<IndustryExport> exports, bool settle)
    {
        var totals = new Dictionary<string, double>();
        foreach (var export in exports)
        {
            if (double.IsFinite(export.Value) && export.Value > 0)
                totals[export.Key] = totals.GetValueOrDefault(export.Key) + export.Value;
        }

        var accepted = new List<IndustryExport>(totals.Count);
        foreach (var (key, value) in totals)
        {
            var tranche = key.StartsWith(MinedOrder, StringComparison.Ordinal) ? 10_000 : 2_000;
            var sold = _roundSales.GetValueOrDefault(key);
            var filled = Math.Min(value, 2 * tranche - sold);
            if (filled <= 0)
                continue;

            var full = Math.Min(filled, Math.Max(0, tranche - sold));
            accepted.Add(new IndustryExport(key, full + (filled - full) / 2));
            if (settle)
                _roundSales[key] = sold + filled;
        }

        return accepted;
    }

    private void AddFluid(string reagent, float amount, List<IndustryExport> into)
    {
        if (amount > 0 && _fluidPrices.TryGetValue(reagent, out var price) && price > 0)
            into.Add(new IndustryExport("fluid:" + reagent, price * amount));
    }

    // generic price (price guns, other pricing) of a tank follows the same registry as the ASRS
    private void OnTankPrice(Entity<FluidTankComponent> tank, ref PriceCalculationEvent args)
    {
        if (args.Handled)
            return;

        foreach (var (kind, amount) in tank.Comp.Contents)
            args.Price += amount * _fluidPrices.GetValueOrDefault(kind);
    }
}
