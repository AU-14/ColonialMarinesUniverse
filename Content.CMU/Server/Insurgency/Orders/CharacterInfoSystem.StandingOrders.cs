using Content.Server._AU14.Insurgency.Orders;
using Content.Shared._CMU14.Threats.Mobs.CLF;

namespace Content.Server.CharacterInfo;

// The cell leader's standing orders ride in the character brief rather than on paper.
// See CLFStandingOrdersSystem for why.
public sealed partial class CharacterInfoSystem
{
    [Dependency] private CLFStandingOrdersSystem _clfOrders = default!;

    private string? AddCLFStandingOrders(string? briefing, EntityUid entity)
    {
        if (!HasComp<CLFMemberComponent>(entity))
            return briefing;

        if (_clfOrders.Orders is not { } orders || string.IsNullOrWhiteSpace(orders))
            return briefing;

        var standingOrders = string.IsNullOrWhiteSpace(_clfOrders.IssuedBy)
            ? Loc.GetString("clf-standing-orders-primer", ("orders", orders))
            : Loc.GetString("clf-standing-orders-primer-signed", ("orders", orders), ("leader", _clfOrders.IssuedBy));

        return string.IsNullOrWhiteSpace(briefing)
            ? standingOrders
            : $"{briefing}\n{standingOrders}";
    }
}
