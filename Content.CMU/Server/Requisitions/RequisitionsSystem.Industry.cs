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
        value = 0;
        if (!HasComp<CargoSellWhitelistComponent>(cargo) && !HasComp<RequisitionsInvoiceComponent>(cargo)
            && !_industryExports.CanExport(cargo))
            return false;

        if (_sellCargoRewards)
        {
            value = SubmitInvoices(cargo);
            value += TryComp<RequisitionsCrateComponent>(cargo, out var crate)
                ? crate.Reward + _industryExports.GetContainedExportPrice(cargo)
                : _pricing.GetPrice(cargo);
            if (!double.IsFinite(value) || value < 0)
            {
                value = 0;
                return false;
            }
        }

        return true;
    }
}
