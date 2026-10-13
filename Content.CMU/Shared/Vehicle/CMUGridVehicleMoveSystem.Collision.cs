using System;
using Content.Shared.CMU14.Vehicle.Open;
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

    /// <summary>
    /// The share of full crash damage a vehicle takes at its current speed, if it scales its crashes.
    /// </summary>
    private float CMUCrashScale(EntityUid vehicle, GridVehicleMoverComponent mover)
    {
        if (!TryComp(vehicle, out CMUVehicleCrashComponent? crash))
            return 1f;

        var minSpeed = MathF.Max(mover.CollisionDamageMinSpeed, mover.WallSmashMinSpeed);
        return CMUVehicleCrashComponent.Scale(MathF.Abs(mover.CurrentSpeed), minSpeed, mover.MaxSpeed, crash.MinDamageFraction);
    }

    private void CMUScaleCrash(EntityUid vehicle, GridVehicleMoverComponent mover, ref float wheelDamage, ref float hullDamage)
    {
        var scale = CMUCrashScale(vehicle, mover);
        wheelDamage *= scale;
        hullDamage *= scale;
    }

    /// <summary>
    /// A crash's hull damage: only the wheels for a vehicle whose crashes spare its guns.
    /// </summary>
    private void CMUDamageCrashHull(EntityUid vehicle, float hullDamage)
    {
        if (TryComp(vehicle, out CMUVehicleCrashComponent? crash) && crash.WheelsOnly)
            _hardpoints.CMUDamageHullWheels(vehicle, hullDamage);
        else
            _hardpoints.DamageVehicleHull(vehicle, hullDamage);
    }

    private void CMUOnCrashed(EntityUid vehicle, GridVehicleMoverComponent mover, EntityUid target, float hullDamage)
    {
        if (hullDamage <= 0f)
            return;

        var speed = mover.MaxSpeed > 0f ? MathF.Min(1f, MathF.Abs(mover.CurrentSpeed) / mover.MaxSpeed) : 0f;
        var ev = new CMUVehicleCrashedEvent(hullDamage, CMUIsFrontalCrash(vehicle, mover, target), speed);
        RaiseLocalEvent(vehicle, ref ev);
    }

    /// <summary>
    /// Driving forward into something at the front. Reversing, or a contact on a side or the rear, isn't.
    /// </summary>
    private bool CMUIsFrontalCrash(EntityUid vehicle, GridVehicleMoverComponent mover, EntityUid target)
    {
        if (mover.CurrentSpeed <= 0f ||
            !fixtureQ.TryComp(vehicle, out var fixtures) ||
            !fixtureQ.TryComp(target, out var targetFixtures) ||
            !TryGetFixtureLocalAabb(fixtures, out var local) ||
            !TryGetFixtureAabb(targetFixtures, physics.GetPhysicsTransform(target), out var obstacle))
        {
            return false;
        }

        var (position, rotation) = transform.GetWorldPositionRotation(vehicle);
        return CMUVehicleCrashComponent.IsFrontContact(position, rotation, local, obstacle);
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
        // The other vehicle takes the hit as hard as this one does.
        var scale = CMUCrashScale(vehicle, mover);
        if (ApplyCollisionSelfDamage(vehicle, mover, target,
                mover.WallSmashWheelDamage * multiplier,
                mover.WallSmashHullDamage * multiplier) && mover.WallSmashHullDamage > 0f)
        {
            var damage = new DamageSpecifier
            {
                DamageDict = { [CollisionDamageType] = (double) (mover.WallSmashHullDamage * scale) },
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
