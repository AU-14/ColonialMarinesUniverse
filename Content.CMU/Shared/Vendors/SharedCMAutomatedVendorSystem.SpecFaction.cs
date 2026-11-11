using Content.Shared.CMU14;

namespace Content.Shared._RMC14.Vendors;

public abstract partial class SharedCMAutomatedVendorSystem
{
    // both sides run the same AU14SpecVend, so counting every rack let govfor eat opfor's kits.
    // racks with no side at all still share one pool, same as before
    private string CMUGetSpecVendorFaction(EntityUid vendor)
    {
        if (TryComp<CMAutomatedVendorComponent>(vendor, out var comp) && !string.IsNullOrEmpty(comp.Faction))
            return comp.Faction.ToLowerInvariant();

        // map-placed racks never get stamped by the platoon spawner, so fall back to the ship they're on
        if (Transform(vendor).GridUid is { } grid &&
            TryComp<ShipFactionComponent>(grid, out var ship) &&
            !string.IsNullOrEmpty(ship.Faction))
        {
            return ship.Faction.ToLowerInvariant();
        }

        return string.Empty;
    }
}
