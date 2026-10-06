using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round;

namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    /// <summary>
    /// Normal navigation requires console access and FoF opponents cannot bypass the hijack process.
    /// Check this again when accepting a launch, since an already open UI is not authorization.
    /// </summary>
    public bool CanUseNavigation(EntityUid computer, EntityUid user)
    {
        // FoF hijack eligibility and runtime ownership are server-side. Other modes keep
        // their existing access-reader behavior, including access unlocked by a completed hijack.
        if (IsForceOnForceHuman(user) && TryComp<WhitelistedShuttleComponent>(computer, out var whitelist) &&
            ForceOnForceSystem.Opponent(whitelist.Faction) != null &&
            TryComp<MarineComponent>(user, out var marine) &&
            ForceOnForceSystem.Opponent(marine.Faction) != null &&
            !string.Equals(marine.Faction, whitelist.Faction, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _navigationAccess.IsAllowed(user, computer);
    }
}
