using Content.Server.Cargo.Components;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    [Dependency] private CMUIndustryExportSystem _industryExports = default!;
    [Dependency] private CMUIndustryMarketSystem _industryMarket = default!;

    // anything a catalog sells loose pays back at most this much of its cheapest price, or buying and returning it
    // prints money (liquid gas canisters cost 400 and appraised at 527)
    private const double CatalogReturnShare = 0.5;
    private Dictionary<EntProtoId, int>? _catalogCosts;

    // what a shipment is worth, split in two: regular ASRS value (crate refunds, invoices, whitelisted cargo) pays as
    // it always has, industry exports go through the faction's market and lose value when flooded
    public sealed class AsrsShipment
    {
        public double Regular;
        public readonly List<IndustryExport> Exports = new();

        public double ExportBase
        {
            get
            {
                var sum = 0.0;
                foreach (var export in Exports)
                    sum += export.Value;
                return sum;
            }
        }
    }

    // adds one piece of cargo to the shipment. false when the ASRS won't take it at all
    public bool TryCollectAsrsCargo(EntityUid cargo, AsrsShipment shipment)
    {
        var exportable = _industryExports.CanExport(cargo);
        if (!HasComp<CargoSellWhitelistComponent>(cargo) && !HasComp<RequisitionsInvoiceComponent>(cargo) && !exportable)
            return false;

        if (!_sellCargoRewards)
            return true;

        var regular = (double) SubmitInvoices(cargo);
        if (TryComp<RequisitionsCrateComponent>(cargo, out var crate))
        {
            // a crate pays its refund, and only industry exports inside it count on top
            regular += crate.Reward;
            CollectContainedExports(cargo, shipment.Exports);
        }
        else if (exportable)
        {
            _industryExports.CollectExports(cargo, shipment.Exports);
            CollectContainedExports(cargo, shipment.Exports);
        }
        else
        {
            regular += AppraiseRegular(cargo, shipment.Exports);
        }

        if (!double.IsFinite(regular) || regular < 0)
            return false;

        shipment.Regular += regular;
        return true;
    }

    // whitelisted cargo keeps its normal price, but industry exports tucked inside it still go through the market
    private double AppraiseRegular(EntityUid uid, List<IndustryExport> exports)
    {
        var price = _pricing.GetPrice(uid, includeContents: false);
        if (TryComp<ContainerManagerComponent>(uid, out var manager))
            price += AppraiseRegularContents(manager, exports);

        if (MetaData(uid).EntityPrototype is { } proto && CatalogCosts().TryGetValue(proto.ID, out var cost))
            price = Math.Min(price, cost * CatalogReturnShare);

        return price;
    }

    private double AppraiseRegularContents(ContainerManagerComponent manager, List<IndustryExport> exports)
    {
        var price = 0.0;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (_industryExports.CanExport(item))
                {
                    _industryExports.CollectExports(item, exports);
                    CollectContainedExports(item, exports);
                }
                else
                {
                    price += AppraiseRegular(item, exports);
                }
            }
        }

        return price;
    }

    // cheapest price of everything any faction's catalog delivers, by the prototype that arrives
    private Dictionary<EntProtoId, int> CatalogCosts()
    {
        if (_catalogCosts != null)
            return _catalogCosts;

        _catalogCosts = new Dictionary<EntProtoId, int>();
        foreach (var proto in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<RequisitionsComputerComponent>(out var computer, EntityManager.ComponentFactory))
                continue;

            foreach (var category in computer.Categories)
            {
                foreach (var entry in category.Entries)
                {
                    if (entry.Cost > 0 && (!_catalogCosts.TryGetValue(entry.Crate, out var known) || entry.Cost < known))
                        _catalogCosts[entry.Crate] = entry.Cost;
                }
            }
        }

        return _catalogCosts;
    }

    private void CollectContainedExports(EntityUid uid, List<IndustryExport> exports)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (_industryExports.CanExport(item))
                    _industryExports.CollectExports(item, exports);
                CollectContainedExports(item, exports);
            }
        }
    }

    // what this cargo would pay right now, market included. without a faction it reads the market of the nearest ASRS
    // platform, so an appraisal tool shows what the elevator will actually pay
    public bool TryAppraiseAsrsCargo(EntityUid cargo, out double value, string? faction = null)
    {
        value = 0;
        var shipment = new AsrsShipment();
        if (!TryCollectAsrsCargo(cargo, shipment))
            return false;

        faction ??= NearestAsrsFaction(cargo);
        value = shipment.Regular + (faction == null ? shipment.ExportBase : _industryMarket.Quote(faction, shipment.Exports));
        return true;
    }

    // pays out a whole elevator load at once, so the load saturates its own market as it sells
    private double SettleAsrsShipment(string faction, AsrsShipment shipment)
    {
        return shipment.Regular + _industryMarket.Sell(faction, shipment.Exports);
    }

    private string? NearestAsrsFaction(EntityUid cargo)
    {
        var xform = Transform(cargo);
        var position = _transform.GetWorldPosition(xform);
        string? best = null;
        var bestDistance = float.MaxValue;
        var elevators = EntityQueryEnumerator<RequisitionsElevatorComponent, TransformComponent>();
        while (elevators.MoveNext(out var uid, out var elevator, out var elevatorXform))
        {
            if (elevatorXform.MapID != xform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(elevatorXform) - position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = GetAccount(elevator.Faction).Comp.Faction;
        }

        return best;
    }
}
