using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Cargo;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Government;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>ASRS appraisal for industry reservoirs and otherwise unpriced world-trade stacks.</summary>
public sealed partial class CMUIndustryExportSystem : EntitySystem
{
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    // Each off-world order pays full price for one tranche, then half price for one more.
    private const double DemandTranche = 2000;
    private readonly Dictionary<string, double> _roundSales = new();
    private readonly Dictionary<ProtoId<StackPrototype>, double> _unmarkedCaps = new();
    private readonly Dictionary<EntProtoId, double> _entityPrices = new();
    private readonly Dictionary<ProtoId<StackPrototype>, double> _stackPrices = new();
    private readonly Dictionary<ProtoId<StackPrototype>, int> _tradePrices = new();

    public override void Initialize()
    {
        BuildPriceCaches();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _roundSales.Clear());
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<ASRSSellableDebugToolComponent, AfterInteractEvent>(OnDebugToolAfterInteract);
        SubscribeLocalEvent<ASRSSellableComponent, PriceCalculationEvent>(OnSellablePrice);
        SubscribeLocalEvent<FluidTankComponent, PriceCalculationEvent>(OnTankPrice);
        SubscribeLocalEvent<StackComponent, PriceCalculationEvent>(OnStackPrice);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUIndustryPricePrototype>() || args.WasModified<ColonyWorldGoodPrototype>())
            BuildPriceCaches();
    }

    private void BuildPriceCaches()
    {
        _entityPrices.Clear();
        _stackPrices.Clear();
        _unmarkedCaps.Clear();
        _tradePrices.Clear();

        foreach (var price in ProtoMan.EnumeratePrototypes<CMUIndustryPricePrototype>())
        {
            if (price.Stack is { } stack)
            {
                _stackPrices.Add(stack, price.Price);
                if (price.UnmarkedPriceCap is { } cap)
                    _unmarkedCaps.Add(stack, cap);
            }
            if (price.Entity is { } entity)
                _entityPrices.Add(entity, price.Price);
        }

        foreach (var good in ProtoMan.EnumeratePrototypes<ColonyWorldGoodPrototype>())
        {
            if (good.Stack is { } stack &&
                (!_tradePrices.TryGetValue(stack, out var existing) || good.SellPrice < existing))
                _tradePrices[stack] = good.SellPrice;
        }
    }

    public bool TryGetCanonicalUnitPrice(EntityUid uid, out double price)
    {
        if (TryComp<StackComponent>(uid, out var stack) && _stackPrices.TryGetValue(stack.StackTypeId, out price))
            return true;

        price = 0;
        return MetaData(uid).EntityPrototype is { } prototype && _entityPrices.TryGetValue(prototype.ID, out price);
    }

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
        => HasComp<ASRSSellableComponent>(uid)
           || HasComp<CMUIndustryExportComponent>(uid)
           || HasComp<FluidTankComponent>(uid) && HasComp<FluidFillableComponent>(uid)
           || TryComp<StackComponent>(uid, out var stack) && GetTradePrice(stack) > 0;

    /// <summary>Collect individual goods so crates, stack splits and different vessels share the same demand.</summary>
    internal bool CollectCargo(EntityUid uid, Dictionary<string, double> goods, bool includeOrdinaryContents)
    {
        var price = _pricing.GetPrice(uid, includeContents: false);
        if (!double.IsFinite(price) || price < 0)
            return false;

        var stack = CompOrNull<StackComponent>(uid);
        var marked = HasComp<ASRSSellableComponent>(uid);
        var fixedPrice = marked && TryGetCanonicalUnitPrice(uid, out _);
        if (!marked && stack != null && _unmarkedCaps.TryGetValue(stack.StackTypeId, out var cap))
            price = Math.Min(price, cap * stack.Count);

        // The catalog price already covers the contents of finished products such as prepared food.
        // For ordinary vessels, put the liquid value on the reagent's order, regardless of the vessel.
        if (!fixedPrice)
            price = Math.Max(0, price - CollectFluidValue(uid, goods));
        else if (HasComp<CMUIndustryExportComponent>(uid) || HasComp<FluidFillableComponent>(uid))
            CollectFluidValue(uid, goods);

        if (stack != null)
            AddGoods(goods, $"stack:{stack.StackTypeId}", price);
        else if (MetaData(uid).EntityPrototype is { } prototype)
            AddGoods(goods, $"entity:{prototype.ID}", price);

        CollectContents(uid, goods, includeOrdinaryContents && !fixedPrice);
        return true;
    }

    private double CollectFluidValue(EntityUid uid, Dictionary<string, double> goods)
    {
        var liquid = 0.0;
        if (TryComp<FluidTankComponent>(uid, out var tank))
        {
            foreach (var (kind, amount) in tank.Contents)
            {
                if (_reagents.TryIndex(kind, out var reagent))
                    liquid += AddGoods(goods, $"fluid:{kind}", amount * reagent.PricePerUnit);
            }
        }

        foreach (var (_, solution) in _solutions.EnumerateSolutions(uid))
        {
            foreach (var (kind, quantity) in solution.Comp.Solution.Contents)
            {
                if (_reagents.TryIndex(kind, out var reagent))
                    liquid += AddGoods(goods, $"fluid:{kind.Prototype}", (float) quantity * reagent.PricePerUnit);
            }
        }

        return liquid;
    }

    internal void CollectContents(EntityUid uid, Dictionary<string, double> goods, bool includeOrdinaryContents)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                // A crate shell or invoice is only redeemable under the requisitions return rules.
                if (HasComp<RequisitionsInvoiceComponent>(item))
                    continue;

                if (HasComp<RequisitionsCrateComponent>(item))
                    CollectContents(item, goods, false);
                else if (CanExport(item) || includeOrdinaryContents)
                    CollectCargo(item, goods, includeOrdinaryContents);
                else
                    CollectContents(item, goods, false);
            }
        }
    }

    private static double AddGoods(Dictionary<string, double> goods, string product, double value)
    {
        if (!(value > 0) || !double.IsFinite(value))
            return 0;

        goods[product] = goods.GetValueOrDefault(product) + value;
        return value;
    }

    internal double PriceShipment(Dictionary<string, double> goods, bool settle)
    {
        var payout = 0.0;
        foreach (var (product, value) in goods)
        {
            var sold = _roundSales.GetValueOrDefault(product);
            var full = Math.Min(value, Math.Max(0, DemandTranche - sold));
            var accepted = Math.Min(value, 2 * DemandTranche - sold);
            payout += full + (accepted - full) * 0.5;
            if (settle)
                _roundSales[product] = sold + accepted;
        }

        return payout;
    }

    private void OnTankPrice(Entity<FluidTankComponent> tank, ref PriceCalculationEvent args)
    {
        if (args.Handled)
            return;
        foreach (var (kind, amount) in tank.Comp.Contents)
        {
            if (_reagents.TryIndex(kind, out var reagent))
                args.Price += amount * reagent.PricePerUnit;
        }
    }

    private void OnStackPrice(Entity<StackComponent> stack, ref PriceCalculationEvent args)
    {
        // Existing material/static prices remain authoritative; the trade table fills missing valuations.
        if (args.Handled || HasComp<MaterialComponent>(stack) || HasComp<StackPriceComponent>(stack)
            || HasComp<StaticPriceComponent>(stack))
            return;
        args.Price += (double) stack.Comp.Count * GetTradePrice(stack.Comp);
    }

    private int GetTradePrice(StackComponent stack) => _tradePrices.GetValueOrDefault(stack.StackTypeId);
}
