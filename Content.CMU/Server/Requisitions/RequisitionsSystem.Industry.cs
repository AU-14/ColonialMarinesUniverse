using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fluids;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private CMUIndustryExportSystem _industryExports = default!;
    [Dependency] private CMUIndustryMarketSystem _industryMarket = default!;
    [Dependency] private GasCanisterPricingSystem _gasCanisterPricing = default!;

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
    }

    // the console of this account that was used last speaks the payout, or any of its consoles if none was
    private void CMUSendSaleFeedback(Entity<RequisitionsAccountComponent> account, string text)
    {
        Entity<RequisitionsComputerComponent>? speaker = null;
        var query = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            if (computer.Account != account.Owner)
                continue;

            speaker ??= (uid, computer);
            if (computer.IsLastInteracted)
            {
                speaker = (uid, computer);
                break;
            }
        }

        if (speaker is { } found)
            SendUIFeedback(found, text);
    }

    // adds one piece of cargo to the shipment. false when the ASRS won't take it at all
    public bool TryCollectAsrsCargo(EntityUid cargo, AsrsShipment shipment)
    {
        var exportable = _industryExports.CanExport(cargo);
        // Ordinary whitelisted cargo is still returnable, but a registry-priced
        // industry item or industry reservoir cannot bypass export eligibility.
        if (!exportable && !HasComp<RequisitionsCrateComponent>(cargo) &&
            (HasComp<ASRSSellableComponent>(cargo) || _industryExports.TryGetCanonicalUnitPrice(cargo, out _) ||
             _industryExports.IsNonSellableIndustryProduct(cargo) ||
             HasComp<CMUIndustryExportComponent>(cargo) ||
             HasComp<FluidTankComponent>(cargo) && HasComp<FluidFillableComponent>(cargo)))
            return false;
        if (!HasComp<CargoSellWhitelistComponent>(cargo) && !HasComp<RequisitionsInvoiceComponent>(cargo) && !exportable)
            return false;

        if (!_sellCargoRewards)
            return true;

        var start = shipment.Exports.Count;
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
        else if (!HasComp<RequisitionsInvoiceComponent>(cargo))
        {
            regular += AppraiseRegular(cargo, shipment.Exports);
        }

        if (!double.IsFinite(regular) || regular < 0)
        {
            shipment.Exports.RemoveRange(start, shipment.Exports.Count - start);
            return false;
        }

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
        {
            // the cap is for selling the item back as bought; gas pumped into a canister afterwards still pays (BUG-1573)
            var added = TryComp<GasCanisterComponent>(uid, out var canister)
                ? _gasCanisterPricing.AddedGasValue((uid, canister), proto)
                : 0;
            price = Math.Min(price, cost * CatalogReturnShare + added);
        }

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
                else if (!HasComp<ASRSSellableComponent>(item) &&
                         !_industryExports.TryGetCanonicalUnitPrice(item, out _) &&
                         !_industryExports.IsNonSellableIndustryProduct(item) &&
                         !HasComp<CMUIndustryExportComponent>(item) &&
                         !(HasComp<FluidTankComponent>(item) && HasComp<FluidFillableComponent>(item)))
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
        var accepted = _industryExports.FillRoundOrders(shipment.Exports, settle: false);
        value = shipment.Regular + _industryMarket.Quote(faction ?? "unassigned", accepted);
        return true;
    }

    // pays out a whole elevator load at once, so the load saturates its own market as it sells
    private double SettleAsrsShipment(string faction, AsrsShipment shipment)
    {
        return shipment.Regular + _industryMarket.Sell(faction, _industryExports.FillRoundOrders(shipment.Exports, settle: true));
    }

    // Accounts are keyed lowercase; overwatch consoles say "GOVFOR", which opened an empty account so supply drops never had funds (BUG-1518).
    private static string AsrsFaction(string? faction)
        => string.IsNullOrEmpty(faction) || faction.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? "unassigned"
            : faction.ToLowerInvariant();

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
            best = AsrsFaction(elevator.Faction);
        }

        return best;
    }

    // Set the amounts on the actual crate and invoice, so returning them separately pays the same.
    private void LimitAsrsReturnRewards(EntityUid shipment, EntityUid paper, int paid)
    {
        var remaining = Math.Max(0, paid) / 4;
        if (TryComp<RequisitionsCrateComponent>(shipment, out var crate))
        {
            crate.Reward = Math.Min(crate.Reward, remaining);
            remaining -= crate.Reward;
            Dirty(shipment, crate);
        }

        if (TryComp<RequisitionsInvoiceComponent>(paper, out var invoice))
        {
            invoice.Reward = Math.Min(invoice.Reward, remaining);
            Dirty(paper, invoice);
        }
    }
}
