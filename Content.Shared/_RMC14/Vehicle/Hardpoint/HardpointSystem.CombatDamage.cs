using System;
using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Vehicle;

// CMU14
public sealed partial class HardpointSystem
{
    private void OnVehicleDamageModify(Entity<HardpointSlotsComponent> ent, ref DamageModifyEvent args)
    {
        if (_net.IsClient)
            return;

        var incomingMultiplier = IgnoresBlastSourceMultiplier(ent.Owner, args.Impact.Delivery) // FLOW-09
            ? 1f
            : GetVehicleIncomingDamageMultiplier(args.Origin, args.Tool);
        if (incomingMultiplier > 1f)
            args.Damage = ScaleDamage(args.Damage, incomingMultiplier);

        var totalDamage = args.Damage.GetTotal().Float();
        var blast = TakePendingBlast(ent.Owner, args.Impact.Delivery); // FLOW-09
        if (totalDamage <= 0f)
            return;

        var hullChunk = TryApplyCatastrophicDamage(ent.Owner, totalDamage, args.Impact.Delivery, args.Tool, blast);
        TryTriggerBlackfootFuelLeak(ent.Owner, totalDamage);

        // A tank's catastrophic hit was already spent as a fixed hull chunk; routing it to the modules as well
        // would add the hit on top. The frame still takes its usual share so damage reactions (cook-off rolls) run.
        if (hullChunk)
        {
            args.Damage = ScaleDamage(args.Damage, ent.Comp.FrameDamageFractionWhileIntact);
            return;
        }

        if (!TryComp(ent.Owner, out ItemSlotsComponent? itemSlots))
            return;

        // The frame and installed armor have already reduced this damage. Spend one
        // budget across unique parts; a nested turret must not duplicate a hit.
        var parts = new List<EntityUid>();
        var visited = new HashSet<EntityUid>();
        foreach (var mounted in _topology.GetMountedSlots(ent.Owner, ent.Comp, itemSlots))
        {
            if (mounted.Item is { } item && visited.Add(item) &&
                TryComp(item, out HardpointIntegrityComponent? integrity) && integrity.Integrity > 0f)
                parts.Add(item);
        }

        // Protected modules must not absorb the whole damage budget or keep the hull alive forever.
        // Keep their protection while vulnerable parts survive; once only protected parts remain,
        // further hull hits consume their integrity directly.
        var routedDamage = args.Damage;
        var vulnerableParts = parts.FindAll(part => GetRoutedDamageAmount(part, routedDamage) > 0f);
        if (parts.Count > 0 && vulnerableParts.Count == 0)
        {
            ApplyHullChunk(ent.Owner, Comp<HardpointIntegrityComponent>(ent.Owner), totalDamage);
            args.Damage = ScaleDamage(args.Damage, ent.Comp.FrameDamageFractionWhileIntact);
            return;
        }

        if (parts.Count > 0)
        {
            var failureDamage = totalDamage;
            if (args.Impact.Delivery == DamageImpactDelivery.Explosion)
            {
                // FLOW-09: split evenly, capped per blast on tanks with a blast profile.
                var hullLoss = ApplyRoutedBlastDamage(ent.Owner, parts, args.Damage, blast);
                // The profile's multiplier is a tank balance knob, not a bigger blast: roll structural failure on the
                // hull the blast actually removed so it cannot inflate the failure chance past the blast cap.
                if (HasComp<CMUTankBlastProfileComponent>(ent.Owner))
                    failureDamage = hullLoss;
            }
            else
            {
                // CMU14: filter protected targets only for direct hits. Blasts split their budget before
                // module protection; excluding immune parts first increases every other part's share.
                parts = vulnerableParts;
                var primary = SelectDamageHardpoint(ent.Owner, parts, args.Origin);
                var spillover = parts.Count > 1 ? Math.Clamp(ent.Comp.DamageSpilloverFraction, 0f, 1f) : 0f;
                ApplyRoutedDamage(ent.Owner, primary, ScaleDamage(args.Damage, 1f - spillover));
                if (spillover > 0f)
                {
                    parts.Remove(primary);
                    ApplyRoutedDamage(ent.Owner, _random.Pick(parts), ScaleDamage(args.Damage, spillover));
                }
            }

            RefreshVehicleFrameIntegrityFromHardpoints(ent.Owner, ent.Comp, itemSlots);
            TryTriggerVehicleStructuralFailure(ent.Owner, failureDamage);
            args.Damage = ScaleDamage(args.Damage, ent.Comp.FrameDamageFractionWhileIntact);
        }
        else if (TryComp(ent.Owner, out HardpointIntegrityComponent? frame))
        {
            DamageHardpoint(ent.Owner, ent.Owner, totalDamage, frame);
        }
    }

    private EntityUid SelectDamageHardpoint(EntityUid vehicle, List<EntityUid> parts, EntityUid? origin)
    {
        // Damage events provide an attacker, not a precise projectile contact point.
        // Use the approach side to select exposed mounts, with a fallback for blast/environment damage.
        var direction = System.Numerics.Vector2.Zero;
        if (origin is { } source && Exists(source))
        {
            var vehicleMap = _transform.GetMapCoordinates(vehicle);
            var sourceMap = _transform.GetMapCoordinates(source);
            if (vehicleMap.MapId == sourceMap.MapId)
                direction = (-_transform.GetWorldRotation(vehicle)).RotateVec(sourceMap.Position - vehicleMap.Position);
        }

        var best = new List<EntityUid>();
        var bestScore = float.MinValue;
        foreach (var part in parts)
        {
            var region = CompOrNull<HardpointItemComponent>(part)?.DamageRegion ?? VehicleDamageRegion.Exterior;
            var score = VehicleDamageRules.GetRegionPriority(region, direction);
            if (score > bestScore)
            {
                best.Clear();
                bestScore = score;
            }
            if (score == bestScore)
                best.Add(part);
        }
        return _random.Pick(best);
    }

    private void ApplyRoutedDamage(EntityUid vehicle, EntityUid part, DamageSpecifier damage)
    {
        DamageHardpoint(vehicle, part, GetRoutedDamageAmount(part, damage));
    }

    private float GetRoutedDamageAmount(EntityUid part, DamageSpecifier damage)
    {
        // Installed armor's modifier sets are already applied to the frame event.
        // Applying them again here would protect the armor module twice.
        var amount = MathF.Max(damage.GetTotal().Float(), 0f);
        if (TryComp(part, out HardpointDamageModifierComponent? modifiers))
        {
            var sets = new List<DamageModifierSet>();
            foreach (var id in modifiers.ModifierSets)
                if (_prototypeManager.TryIndex<DamageModifierSetPrototype>(id, out var set))
                    sets.Add(set);
            amount = MathF.Max(DamageSpecifier.ApplyModifierSets(damage, sets).GetTotal().Float(), 0f);
        }
        amount *= MathF.Max(CompOrNull<HardpointItemComponent>(part)?.DamageMultiplier ?? 1f, 0f);
        return amount;
    }

    private float GetVehicleIncomingDamageMultiplier(EntityUid? origin, EntityUid? tool)
    {
        var multiplier = 1f;

        if (TryGetVehicleDamageMultiplier(origin, out var originMultiplier))
            multiplier = MathF.Max(multiplier, originMultiplier);

        if (TryGetVehicleDamageMultiplier(tool, out var toolMultiplier))
            multiplier = MathF.Max(multiplier, toolMultiplier);

        return multiplier;
    }

    private bool TryGetVehicleDamageMultiplier(EntityUid? source, out float multiplier)
    {
        multiplier = 1f;

        if (source == null || !TryComp<VehicleDamageMultiplierComponent>(source.Value, out var vehicleDamage))
            return false;

        multiplier = MathF.Max(vehicleDamage.Multiplier, 0f);
        return multiplier > 0f;
    }

}
