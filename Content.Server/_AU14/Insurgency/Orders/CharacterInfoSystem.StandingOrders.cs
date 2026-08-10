using Content.Server._AU14.Insurgency.Orders;
using Content.Shared._CMU14.Threats.Mobs.CLF;

namespace Content.Server.CharacterInfo;

// the cell leader's standing orders ride in the character brief rather than on paper.
// see CLFStandingOrdersSystem for why
public sealed partial class CharacterInfoSystem
{
    [Dependency] private CLFStandingOrdersSystem _clfOrders = default!;

    private void AddCLFStandingOrders(List<string> lines, EntityUid entity)
    {
        if (!HasComp<CLFMemberComponent>(entity))
            return;

        if (_clfOrders.Orders is not { } orders || string.IsNullOrWhiteSpace(orders))
            return;

        lines.Add(string.IsNullOrWhiteSpace(_clfOrders.IssuedBy)
            ? Loc.GetString("clf-standing-orders-primer", ("orders", orders))
            : Loc.GetString("clf-standing-orders-primer-signed", ("orders", orders), ("leader", _clfOrders.IssuedBy)));
    }
}
