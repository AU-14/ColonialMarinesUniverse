using System;
using Content.Shared._RMC14.Crate;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    // crates and cades are what armour is supposed to roll over, flattening one shouldn't chip the hull
    private float CMUSmashSelfDamageScale(EntityUid target, VehicleSmashableComponent? smashable)
    {
        if (HasComp<BarricadeComponent>(target) || HasComp<CrateOpenableComponent>(target))
            return 0f;

        return smashable?.SelfDamageMultiplier ?? 1f;
    }

    private bool CMUIsAboard(EntityUid other, EntityUid vehicle)
    {
        var parent = Transform(other).ParentUid;
        for (var i = 0; i < 4 && parent.IsValid(); i++)
        {
            if (parent == vehicle)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

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
