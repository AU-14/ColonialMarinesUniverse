using System;
using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    private void CMUMarkPermanentWreck(EntityUid vehicle, HardpointIntegrityComponent frame, float previous)
    {
        if (previous > 0f && frame.Integrity <= 0f && HasComp<CMUVehiclePermanentWreckComponent>(vehicle))
            frame.DestroyedBeyondRepair = true;
    }

    /// <summary>
    ///     Handles a single catastrophic hit (after hull armour, before it is split among the modules).
    ///     Tanks that can cook off take a heavy hull chunk instead of being wrecked outright; every other vehicle
    ///     is destroyed beyond repair by a hit at least as large as its whole hull.
    /// </summary>
    /// <returns>
    ///     True when the hit was spent as a tank's hull chunk, so the caller must not also route it to the modules.
    /// </returns>
    private bool TryApplyCatastrophicDamage(EntityUid vehicle, float damage, DamageImpactDelivery delivery,
        EntityUid? tool, ProtoId<ExplosionPrototype>? blast)
    {
        if (!IsVehicleFrame(vehicle) || !TryComp<HardpointIntegrityComponent>(vehicle, out var frame) ||
            frame.DestroyedBeyondRepair || frame.MaxIntegrity <= 0f)
            return false;

        // FLOW-08: a tank's hull is the sum of its modules, so the "one hit wrecks it" rule turned every heavy
        // anti-armour hit into a guaranteed wreck and cook-off, while module multipliers shrank anything below it to
        // chip damage. A tank whose cook-off is disabled (the AEV) keeps the vehicle rule.
        if (TryComp<TankCookOffComponent>(vehicle, out var tank) && tank.Enabled)
        {
            float chunk;
            if (delivery == DamageImpactDelivery.Explosion)
            {
                // FLOW-09: blasts never take a chunk (their hull loss is capped instead), except the catastrophic
                // types (orbital bombardment), which still need the whole hull.
                if (!IsCatastrophicBlast(tank, blast) || damage < frame.MaxIntegrity)
                    return false;

                chunk = tank.CatastrophicHullFraction;
            }
            else if (delivery == DamageImpactDelivery.Projectile &&
                     TryComp(tool, out TankCookOffProjectileComponent? round) && round.HullFraction is > 0f)
            {
                // FLOW-09: a dedicated anti-armour round takes its own chunk whatever the hit's size, once per tank.
                // A repeat contact with the same tank is spent (no chunk, no module damage); a different tank it goes
                // on to strike takes a direct hit of its own.
                if (!round.ChunkedVehicles.Add(vehicle))
                    return true;

                chunk = round.HullFraction.Value;
            }
            else
            {
                if (damage < frame.MaxIntegrity * Math.Clamp(tank.CatastrophicHitFraction, 0f, 1f))
                    return false;

                chunk = tank.CatastrophicHullFraction;
            }

            ApplyHullChunk(vehicle, frame, frame.MaxIntegrity * Math.Clamp(chunk, 0f, 1f));
            TryTriggerVehicleStructuralFailure(vehicle, damage);
            return true;
        }

        if (damage < frame.MaxIntegrity)
            return false;

        // Compare the hit after hull armor, before splitting it among resistant modules.
        // A surviving turret or a replacement part cannot restore a catastrophically damaged frame.
        frame.DestroyedBeyondRepair = true;
        frame.Integrity = 0f;
        Dirty(vehicle, frame);
        UpdateFrameDamageAppearance(vehicle, frame);
        RaiseIntegrityChanged(vehicle);
        RaiseFrameIntegrityChanged(vehicle, false);
        RefreshCanRun(vehicle);
        _lock.RefreshForcedOpen(vehicle);
        UpdateHardpointUi(vehicle);
        return false;
    }

    /// <summary>FLOW-09: whether this blast is one of the tank's catastrophic explosion types (orbital bombardment).</summary>
    private static bool IsCatastrophicBlast(TankCookOffComponent tank, ProtoId<ExplosionPrototype>? blast)
    {
        return blast is { } explosion && tank.CatastrophicExplosions.Contains(explosion);
    }

    /// <summary>
    ///     FLOW-09: the type of the explosion now damaging this vehicle, as its blast profile recorded it when the
    ///     explosion asked for the vehicle's resistance (the damage event does not carry it), and forgets it so it
    ///     cannot be misread by a later hit. Null for other deliveries and for vehicles without a profile.
    /// </summary>
    private ProtoId<ExplosionPrototype>? TakePendingBlast(EntityUid vehicle, DamageImpactDelivery delivery)
    {
        if (!TryComp(vehicle, out CMUTankBlastProfileComponent? profile))
            return null;

        var blast = profile.PendingExplosion;
        profile.PendingExplosion = null;
        return delivery == DamageImpactDelivery.Explosion ? blast : null;
    }

    /// <summary>
    ///     FLOW-09: a tank with a blast profile takes blasts as the profile scales them, so the exploding entity's own
    ///     vehicle damage multiplier (meant for its direct hit) must not also apply to its blast.
    /// </summary>
    private bool IgnoresBlastSourceMultiplier(EntityUid vehicle, DamageImpactDelivery delivery)
    {
        return delivery == DamageImpactDelivery.Explosion && HasComp<CMUTankBlastProfileComponent>(vehicle);
    }

    /// <summary>
    ///     Splits one blast evenly over the intact modules. FLOW-09: on a tank with a blast profile the hull it removes
    ///     is capped at <see cref="CMUTankBlastProfileComponent.MaxBlastHullFraction"/> of the maximum hull, scaling
    ///     every module's share down alike; catastrophic blast types are not capped.
    /// </summary>
    /// <returns>The hull integrity the blast removed (after module multipliers and the cap).</returns>
    private float ApplyRoutedBlastDamage(EntityUid vehicle, List<EntityUid> parts, DamageSpecifier damage,
        ProtoId<ExplosionPrototype>? blast)
    {
        var share = ScaleDamage(damage, 1f / parts.Count);
        var amounts = new float[parts.Count];
        var integrities = new float[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            amounts[i] = GetRoutedDamageAmount(parts[i], share);
            integrities[i] = CompOrNull<HardpointIntegrityComponent>(parts[i])?.Integrity ?? 0f;
        }

        var scale = 1f;
        if (TryComp(vehicle, out CMUTankBlastProfileComponent? profile) &&
            TryComp(vehicle, out HardpointIntegrityComponent? frame) &&
            !(TryComp(vehicle, out TankCookOffComponent? tank) && IsCatastrophicBlast(tank, blast)))
        {
            var cap = frame.MaxIntegrity * Math.Clamp(profile.MaxBlastHullFraction, 0f, 1f);
            if (GetBlastHullLoss(amounts, integrities, 1f) > cap)
            {
                // A module cannot lose more than it has, so the loss is not linear in the scale; bisect for the
                // scale whose real loss meets the cap.
                var low = 0f;
                var high = 1f;
                for (var step = 0; step < 24; step++)
                {
                    var mid = (low + high) / 2f;
                    if (GetBlastHullLoss(amounts, integrities, mid) > cap)
                        high = mid;
                    else
                        low = mid;
                }

                scale = low;
            }
        }

        for (var i = 0; i < parts.Count; i++)
        {
            DamageHardpoint(vehicle, parts[i], amounts[i] * scale);
        }

        return GetBlastHullLoss(amounts, integrities, scale);
    }

    private static float GetBlastHullLoss(float[] amounts, float[] integrities, float scale)
    {
        var loss = 0f;
        for (var i = 0; i < amounts.Length; i++)
        {
            loss += MathF.Min(amounts[i] * scale, integrities[i]);
        }

        return loss;
    }

    /// <summary>
    ///     Removes exactly <paramref name="amount"/> hull integrity, bypassing module damage multipliers. The hull is
    ///     the sum of its modules, so the loss is spread over the intact modules (wheels last), carrying over whatever
    ///     a module could not absorb; a hull with less than the chunk left goes to zero.
    /// </summary>
    private void ApplyHullChunk(EntityUid vehicle, HardpointIntegrityComponent frame, float amount)
    {
        if (amount <= 0f)
            return;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            DamageHardpoint(vehicle, vehicle, amount, frame);
            return;
        }

        var targets = new List<EntityUid>();
        var remaining = amount;
        var anyModule = false;
        while (remaining > 0.01f)
        {
            targets.Clear();
            CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: false);
            if (targets.Count == 0)
                CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: true);
            if (targets.Count == 0)
                break;

            anyModule = true;
            var share = remaining / targets.Count;
            var spent = 0f;
            foreach (var target in targets)
            {
                if (!TryComp(target, out HardpointIntegrityComponent? integrity))
                    continue;
                var dealt = MathF.Min(share, integrity.Integrity);
                if (DamageHardpoint(vehicle, target, dealt, integrity))
                    spent += dealt;
            }

            if (spent <= 0.01f)
                break;
            remaining -= spent;
        }

        // A stripped frame has no modules and keeps its own integrity.
        if (!anyModule)
        {
            DamageHardpoint(vehicle, vehicle, amount, frame);
            return;
        }

        RefreshVehicleFrameIntegrityFromHardpoints(vehicle, hardpoints, itemSlots);
    }
}
