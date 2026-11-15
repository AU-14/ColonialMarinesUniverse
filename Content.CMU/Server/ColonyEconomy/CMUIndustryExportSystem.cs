using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Cargo;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Government;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>ASRS appraisal for industry reservoirs and otherwise unpriced world-trade stacks.</summary>
public sealed partial class CMUIndustryExportSystem : EntitySystem
{
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FluidTankComponent, PriceCalculationEvent>(OnTankPrice);
        SubscribeLocalEvent<StackComponent, PriceCalculationEvent>(OnStackPrice);
    }

    public bool CanExport(EntityUid uid)
        => HasComp<CMUIndustryExportComponent>(uid)
           || HasComp<FluidTankComponent>(uid) && HasComp<FluidFillableComponent>(uid)
           || TryComp<StackComponent>(uid, out var stack) && GetTradePrice(stack) > 0;

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
