using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Dropship;

namespace Content.Server._RMC14.Dropship;

/// <summary>
///     Third parties a faction calls in while that faction is aboard its ship arrive at the ship's summoned third party
///     pad, and nowhere else. Once the faction is planetside, or when its ship has no such pad, they land as normal.
/// </summary>
public sealed partial class DropshipSystem
{
    [Dependency] private AuRoundSystem _auRound = default!;

    /// <summary>
    ///     The faction a summoned shuttle is held to its ship for, or null when it may land as normal.
    /// </summary>
    public string? GetSummonedThirdPartyShipFaction(EntityUid grid)
    {
        if (!TryComp(grid, out CMUSummonedThirdPartyShuttleComponent? summoned) ||
            string.IsNullOrEmpty(summoned.Faction) ||
            !IsFactionShipside(summoned.Faction) ||
            !HasSummonedThirdPartyPad(summoned.Faction))
        {
            return null;
        }

        return summoned.Faction;
    }

    /// <summary>
    ///     Whether the summoned third party rules allow (true) or forbid (false) a shuttle landing at a destination, or
    ///     null when they have no say and the usual checks apply.
    /// </summary>
    private bool? SummonedThirdPartyDestinationRule(EntityUid grid, EntityUid destination)
    {
        var isPad = HasComp<CMUSummonedThirdPartyDestinationComponent>(destination);
        if (GetSummonedThirdPartyShipFaction(grid) is { } faction)
        {
            if (isPad)
                return string.Equals(GetCarrierFaction(destination), faction, StringComparison.OrdinalIgnoreCase);

            // Its own way back to deep space stays open.
            return TryComp(destination, out ThirdPartyDropshipReturnDestinationComponent? returnDestination) &&
                   returnDestination.Shuttle == grid;
        }

        if (!isPad)
            return null;

        // The pad only takes parties called in by its own ship's faction.
        return TryComp(grid, out CMUSummonedThirdPartyShuttleComponent? summoned) &&
               string.Equals(summoned.Faction, GetCarrierFaction(destination), StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFactionShipside(string faction)
    {
        if (_auRound.GetSelectedPlanet() is not { } planet)
            return false;

        if (string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase))
            return planet.GovforInShip;

        if (string.Equals(faction, "opfor", StringComparison.OrdinalIgnoreCase))
            return planet.OpforInShip;

        return false;
    }

    private bool HasSummonedThirdPartyPad(string faction)
    {
        var pads = EntityQueryEnumerator<CMUSummonedThirdPartyDestinationComponent>();
        while (pads.MoveNext(out var uid, out _))
        {
            if (string.Equals(GetCarrierFaction(uid), faction, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
