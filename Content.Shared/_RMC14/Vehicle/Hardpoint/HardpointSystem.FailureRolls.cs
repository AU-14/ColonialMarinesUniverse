using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    [Dependency] private IGameTiming _timing = default!;

    private bool TryRollFailure(EntityUid vehicle, HardpointIntegrityComponent damagedPart, float amount)
    {
        var chance = VehicleFailureRules.GetChance(damagedPart, amount);
        if (chance <= 0f || !TryComp(vehicle, out HardpointIntegrityComponent? frame) ||
            _timing.CurTime < frame.NextFailureRoll)
            return false;

        var active = CountFailures(vehicle);
        if (TryComp(vehicle, out HardpointSlotsComponent? slots) &&
            TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            var visited = new HashSet<EntityUid> { vehicle };
            foreach (var mounted in _topology.GetMountedSlots(vehicle, slots, itemSlots))
            {
                if (mounted.Item is { } item && visited.Add(item))
                    active += CountFailures(item);
            }
        }

        if (active >= frame.MaxVehicleFailures)
            return false;

        // Consume the interval on a failed roll too. A single impact that damages
        // several modules must not get a separate chance for every module.
        frame.NextFailureRoll = _timing.CurTime + frame.FailureRollCooldown;
        return _random.Prob(chance);
    }

    private int CountFailures(EntityUid uid)
    {
        return TryComp(uid, out VehicleHardpointFailureComponent? failures) ? failures.ActiveFailures.Count : 0;
    }
}
