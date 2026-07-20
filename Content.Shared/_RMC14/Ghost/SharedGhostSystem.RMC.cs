using Content.Shared._RMC14.Ghost;

namespace Content.Shared.Ghost;

public abstract partial class SharedGhostSystem
{
    private bool IgnoresRMCGhostInteractionLimits(EntityUid? target)
    {
        return HasComp<RMCIgnoreGhostInteractionLimitsComponent>(target);
    }
}
