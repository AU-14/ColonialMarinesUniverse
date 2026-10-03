using System;
using Content.Shared.Damage;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    private void CMUApplyVehicleCollision(
        EntityUid vehicle,
        GridVehicleMoverComponent mover,
        EntityUid target,
        float wheelDamage,
        ref bool playedCollisionSound)
    {
        if (!IsSmashingCapable(mover))
        {
            ApplyCollisionSelfDamage(vehicle, mover, target, wheelDamage, 0f);
            return;
        }

        var impactSpeed = MathF.Abs(mover.CurrentSpeed);
        PlayCollisionSound(vehicle, ref playedCollisionSound);

        var multiplier = HasPlowInstalled(vehicle) ? mover.WallSmashPlowDamageMultiplier : 1f;
        // Reuse the impact contact guard for both sides. Repeated movement probes
        // must not transfer another hit to the other hull or its operator.
        if (ApplyCollisionSelfDamage(vehicle, mover, target,
                mover.WallSmashWheelDamage * multiplier,
                mover.WallSmashHullDamage * multiplier) && mover.WallSmashHullDamage > 0f)
        {
            var damage = new DamageSpecifier
            {
                DamageDict = { [CollisionDamageType] = (double) mover.WallSmashHullDamage },
            };
            // Keep normal damage routing so armor, hardpoints and the operator
            // receive a vehicle impact, rather than raw wall-destruction damage.
            _damageable.TryChangeDamage(target, damage, origin: vehicle, tool: vehicle);
        }

        ApplyHeavySmashSlowdown(mover);
        if (ShouldApplyCrashImmobility(mover, impactSpeed))
            ApplyCrashImmobility(vehicle, mover);
        Dirty(vehicle, mover);
    }
}
