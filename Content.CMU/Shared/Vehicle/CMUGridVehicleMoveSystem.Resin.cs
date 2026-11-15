using Content.Shared.CMU14.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    [Dependency] private CMUVehicleResinSystem _cmuResin = default!;
    private readonly VehicleCollisionContactTracker _cmuResinContacts = new();

    private void CMUTryStickResin(EntityUid vehicle, GridVehicleMoverComponent mover, EntityUid target)
    {
        if (_net.IsClient || MathF.Abs(mover.CurrentSpeed) < 0.01f ||
            !TryComp(target, out CMUVehicleStickyResinComponent? resin) ||
            !fixtureQ.TryComp(vehicle, out var vehicleFixtures) ||
            !fixtureQ.TryComp(target, out var targetFixtures) ||
            !TryGetFixtureAabb(vehicleFixtures, physics.GetPhysicsTransform(vehicle), out var vehicleBounds) ||
            !TryGetFixtureAabb(targetFixtures, physics.GetPhysicsTransform(target), out var targetBounds) ||
            !_cmuResinContacts.TryStart(vehicle, target, vehicleBounds, targetBounds))
            return;

        // Roll once per contact, including light vehicles blocked by resin. Holding the throttle
        // against the same wall must not reroll every simulation step.
        _cmuResin.TryStick(vehicle, resin);
    }

    private void CMUUpdateResinContacts(EntityUid vehicle, bool parentChanged)
    {
        if (parentChanged)
            _cmuResinContacts.RemoveVehicle(vehicle);
        else if (_cmuResinContacts.HasContacts(vehicle) && fixtureQ.TryComp(vehicle, out var fixtures) &&
                 TryGetFixtureAabb(fixtures, physics.GetPhysicsTransform(vehicle), out var bounds))
            _cmuResinContacts.Update(vehicle, bounds);
    }

    private float CMUResinSpeed(EntityUid vehicle)
    {
        return TryComp(vehicle, out CMUVehicleResinComponent? resin) && resin.Fouled
            ? resin.SpeedMultiplier
            : 1f;
    }
}
