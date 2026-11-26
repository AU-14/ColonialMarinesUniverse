using Content.Server.Cargo.Components;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    [Dependency] private readonly CMUIndustryExportSystem _industryExports = default!;

    /// <summary>Returns the value this cargo would contribute to an ASRS shipment, before whole-credit settlement.</summary>
    public bool TryAppraiseAsrsCargo(EntityUid cargo, out double value)
    {
        var goods = new Dictionary<string, double>();
        var accepted = TryCollectAsrsCargo(cargo, goods, out var refund);
        value = refund + _industryExports.PriceShipment(goods, settle: false);
        return accepted;
    }

    private bool TryCollectAsrsCargo(EntityUid cargo, Dictionary<string, double> goods, out double refund)
    {
        refund = 0;
        var industrial = _industryExports.CanExport(cargo);
        if (!industrial && !HasComp<CargoSellWhitelistComponent>(cargo) && !HasComp<RequisitionsInvoiceComponent>(cargo))
            return false;
        if (!_sellCargoRewards)
            return true;

        if (TryComp<RequisitionsCrateComponent>(cargo, out var crate))
        {
            refund = crate.Reward;
            _industryExports.CollectContents(cargo, goods, includeOrdinaryContents: false);
        }
        else if (!HasComp<RequisitionsInvoiceComponent>(cargo) &&
                 !_industryExports.CollectCargo(cargo, goods, includeOrdinaryContents: !industrial))
            return false;

        refund += SubmitInvoices(cargo);
        return true;
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
