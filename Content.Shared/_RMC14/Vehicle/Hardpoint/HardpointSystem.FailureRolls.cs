// using Content.Shared.Containers.ItemSlots; // CMU14: no vehicle-wide fault count.
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    [Dependency] private IGameTiming _timing = default!;

    // CMU14 method: retain the shared roll cooldown; each part enforces its own fault limit.
    private bool TryRollFailure(EntityUid vehicle, Entity<HardpointIntegrityComponent> damagedPart, float amount)
    {
        // A saturated part must not consume the shared interval and block another part.
        if (TryComp(damagedPart, out VehicleHardpointFailureComponent? failures) &&
            failures.ActiveFailures.Count >= failures.MaxActiveFailures)
            return false;

        var chance = VehicleFailureRules.GetChance(damagedPart.Comp, amount);
        if (chance <= 0f || !TryComp(vehicle, out HardpointIntegrityComponent? frame) ||
            _timing.CurTime < frame.NextFailureRoll)
            return false;

        // Consume the interval on a failed roll too. A single impact that damages
        // several modules must not get a separate chance for every module.
        frame.NextFailureRoll = _timing.CurTime + frame.FailureRollCooldown;
        return _random.Prob(chance);
    }

    // CMU14: CountFailures was only used by the removed vehicle-wide cap.
    // private int CountFailures(EntityUid uid)
    // {
    //     return TryComp(uid, out VehicleHardpointFailureComponent? failures) ? failures.ActiveFailures.Count : 0;
    // }
}
