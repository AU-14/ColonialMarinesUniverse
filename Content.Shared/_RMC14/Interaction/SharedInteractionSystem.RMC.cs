using Content.Shared._RMC14.CombatMode;
using Content.Shared._RMC14.Ghost;
using Content.Shared.Ghost;

namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    private bool TryRMCCombatModeInteractOverride(EntityUid user, EntityUid? target, out bool canInteract)
    {
        var ev = new RMCCombatModeInteractOverrideUserEvent(target);
        RaiseLocalEvent(user, ref ev);
        canInteract = ev.CanInteract;
        return ev.Handled;
    }

    private bool IgnoresRMCGhostInteractionLimits(EntityUid user, EntityUid target)
    {
        return HasComp<GhostComponent>(user) &&
               HasComp<RMCIgnoreGhostInteractionLimitsComponent>(target);
    }
}
