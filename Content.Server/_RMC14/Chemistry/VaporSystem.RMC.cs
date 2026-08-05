using Content.Server.Chemistry.Components;
using Content.Shared._RMC14.Chemistry;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Chemistry.EntitySystems;

internal sealed partial class VaporSystem
{
    private void RMCRaiseVaporHit(
        Entity<VaporComponent> vapor,
        Entity<SolutionComponent> solution,
        EntityUid target)
    {
        var power = TryComp(vapor, out RMCExtinguisherPowerComponent? extinguisher)
            ? extinguisher.Power
            : 7;
        var ev = new VaporHitEvent(solution, power);
        RaiseLocalEvent(target, ref ev);
    }
}
