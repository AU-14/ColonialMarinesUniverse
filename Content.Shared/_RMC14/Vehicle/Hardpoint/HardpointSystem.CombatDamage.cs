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

        var incomingMultiplier = GetVehicleIncomingDamageMultiplier(args.Origin, args.Tool);
        if (incomingMultiplier > 1f)
            args.Damage = ScaleDamage(args.Damage, incomingMultiplier);

        var totalDamage = args.Damage.GetTotal().Float();
        if (totalDamage <= 0f)
            return;

        TryTriggerBlackfootFuelLeak(ent.Owner, totalDamage);

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

        if (parts.Count > 0)
        {
            if (args.Impact.Delivery == DamageImpactDelivery.Explosion)
            {
                foreach (var part in parts)
                    ApplyRoutedDamage(ent.Owner, part, ScaleDamage(args.Damage, 1f / parts.Count));
            }
            else
            {
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
            TryTriggerVehicleStructuralFailure(ent.Owner, totalDamage);
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
        DamageHardpoint(vehicle, part, amount);
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
