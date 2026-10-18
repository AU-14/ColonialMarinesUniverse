// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Gunsmith;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Armorsmith;

// Gives the armorsmith rules the looks, materials, backings and plates that are defined in prototypes.
public sealed class PrototypeArmorsmithData : IArmorsmithData
{
    private readonly IPrototypeManager _prototypes;
    private Dictionary<string, ArmorsmithPlatePrototype>? _plates;

    public PrototypeArmorsmithData(IPrototypeManager prototypes)
    {
        _prototypes = prototypes;
    }

    public ArmorsmithLookPrototype? Look(string id) => _prototypes.TryIndex<ArmorsmithLookPrototype>(id, out var p) ? p : null;
    public GunsmithMaterialPrototype? Material(string id) => _prototypes.TryIndex<GunsmithMaterialPrototype>(id, out var p) ? p : null;
    public ArmorsmithBackingPrototype? Backing(string id) => _prototypes.TryIndex<ArmorsmithBackingPrototype>(id, out var p) ? p : null;

    public ArmorsmithPlatePrototype? Plate(string materialId)
    {
        if (_plates == null)
        {
            _plates = new Dictionary<string, ArmorsmithPlatePrototype>();
            foreach (var plate in _prototypes.EnumeratePrototypes<ArmorsmithPlatePrototype>())
                _plates.TryAdd(plate.Material, plate);
        }

        return _plates.GetValueOrDefault(materialId);
    }
}

// The rules of the armorsmith: checks an armor design (grade, backing, plates) and works out what it is worth.
public static class ArmorsmithEvaluator
{
    private static readonly double[] GradeWaste = { 1.15, 1.30, 1.55, 1.90 };
    private static readonly double[] GradeComplexity = { 0, 2, 5, 9 };

    private static void Range(DesignCheck<ArmorEvaluation> check, string name, double value, double min, double max)
    {
        if (double.IsNaN(value) || value < min || value > max)
            check.Errors.Add($"{name}: {min:0.##}…{max:0.##}");
    }

    /// <summary>Works out a piece of armor. Everything about the protection is filled in only when there are no errors.</summary>
    public static DesignCheck<ArmorEvaluation> Evaluate(ArmorDesign d, IArmorsmithData data)
    {
        var e = new ArmorEvaluation();
        var check = new DesignCheck<ArmorEvaluation> { Result = e };

        var look = data.Look(d.Look);
        if (look == null)
            check.Errors.Add("look");

        var material = data.Material(d.PlateMaterial);
        var plate = data.Plate(d.PlateMaterial);
        if (material == null || plate == null || !material.Structural)
            check.Errors.Add("plate-material");

        var slot = look?.Slot ?? ArmorSlotKind.Body;
        Range(check, "plate-thickness", d.PlateThicknessMm, ArmorsmithMechanics.MinThicknessMm, ArmorsmithMechanics.MaxThicknessMm(slot));
        Range(check, "coverage", d.CoveragePercent, ArmorsmithMechanics.MinCoveragePercent, ArmorsmithMechanics.MaxCoveragePercent);
        Range(check, "backing-layers", d.BackingLayers, 0, ArmorsmithMechanics.MaxBackingLayers);

        ArmorsmithBackingPrototype? backing = null;
        if (d.BackingLayers > 0)
        {
            backing = data.Backing(d.BackingMaterial);
            if (backing == null)
                check.Errors.Add("backing-material");
        }

        if (d.ItemName.Length > ArmorDesign.MaxNameLength * 2)
            check.Errors.Add("name");
        if (d.ItemDescription.Length > ArmorDesign.MaxDescriptionLength * 2)
            check.Errors.Add("description");

        if (!check.Valid)
            return check;

        var coverage = d.CoveragePercent / 100.0;
        var closed = d.ClosedHelmet && slot == ArmorSlotKind.Head;
        var bonus = closed ? ArmorsmithMechanics.ClosedBonus : 1.0;
        var area = ArmorsmithMechanics.AreaM2(slot, closed);

        // -------------------------------------------------------------- the layers
        var plateKgM2 = ArmorsmithMechanics.ArealKgM2(material!.DensityGcm3, d.PlateThicknessMm);
        var plateMerit = ArmorsmithMechanics.MeritVelocity(ArmorsmithMechanics.MeritU(material.UltimateMpa, plate!.FailureStrain, material.DensityGcm3, material.YoungGpa));
        var plateVelocity = ArmorsmithMechanics.LayerLimitVelocity(plateMerit, plateKgM2);

        var backingKgM2 = 0.0;
        var backingMerit = 0.0;
        var backingVelocity = 0.0;
        if (backing != null)
        {
            backingKgM2 = backing.LayerKgM2 * d.BackingLayers;
            backingMerit = ArmorsmithMechanics.MeritVelocity(ArmorsmithMechanics.MeritU(backing.UltimateMpa, backing.FailureStrain, backing.DensityGcm3, backing.YoungGpa));
            backingVelocity = ArmorsmithMechanics.LayerLimitVelocity(backingMerit, backingKgM2);
        }

        // -------------------------------------------------------------- the workmanship: a plate that is badly worked is weaker than the metal is
        e.Quality = GunsmithEvaluator.Quality(d.Grade, material.Machinability);
        e.Workmanship = Math.Clamp(0.85 + 0.15 * e.Quality, 0.8, 1.03);

        e.Slot = slot;
        e.Closed = closed;
        e.AreaM2 = area;
        e.Coverage = coverage;
        e.PlateKgM2 = plateKgM2;
        e.BackingKgM2 = backingKgM2;
        e.PlateMeritVelocity = plateMerit;
        e.BackingMeritVelocity = backingMerit;
        e.PlateLimitVelocity = plateVelocity;
        e.BackingLimitVelocity = backingVelocity;
        e.LimitVelocity = ArmorsmithMechanics.TotalLimitVelocity(plateVelocity, coverage, backingVelocity, e.Workmanship, bonus);

        // -------------------------------------------------------------- the protection of the game
        e.Bullet = ArmorsmithMechanics.BulletArmor(e.LimitVelocity);
        e.Melee = ArmorsmithMechanics.MeleeArmor(material.YieldMpa * e.Workmanship, d.PlateThicknessMm, coverage, backingKgM2, bonus);
        e.Explosion = ArmorsmithMechanics.ExplosionArmor(plateKgM2, coverage, backingKgM2, bonus);
        e.Bio = ArmorsmithMechanics.BioArmor(coverage, d.BackingLayers, closed ? ArmorsmithMechanics.ClosedBioBonus : 0.0);

        e.MassKg = ArmorsmithMechanics.MassKg(slot, plateKgM2, coverage, backingKgM2, closed);

        // The slowdown: the thicker the plate and the more layers, the slower; a helmet does not slow the wearer.
        if (slot != ArmorSlotKind.Head)
        {
            e.Slowdown = ArmorsmithMechanics.Slowdown(d.PlateThicknessMm, coverage, d.BackingLayers);
            e.SpeedShare = ArmorsmithMechanics.SpeedShare(e.Slowdown);
            e.SpeedTier = ArmorsmithMechanics.SpeedTier(e.SpeedShare);
        }

        if (e.MassKg > ArmorsmithMechanics.MaxMassKg(slot))
        {
            check.Errors.Add($"mass: ≤{ArmorsmithMechanics.MaxMassKg(slot):0.##}");
            return check;
        }

        // -------------------------------------------------------------- building
        var complexity = 6 + GradeComplexity[(int) d.Grade] + Math.Round(coverage * 4) + d.BackingLayers / 6.0 + Math.Max(0.0, d.PlateThicknessMm - 6.0);
        e.Complexity = complexity;
        e.BuildSeconds = 20 + 3 * complexity;

        // The plate: its volume is the area times the thickness; a finer grade of work wastes more metal.
        var waste = GradeWaste[(int) d.Grade];
        var plateVolumeCm3 = area * 1000.0 * coverage * d.PlateThicknessMm;
        e.MaterialUnits[material.ID] = plateVolumeCm3 * waste / Math.Max(1.0, material.VolumeCm3);

        if (backing != null)
            e.MaterialUnits[backing.StackType] = d.BackingLayers * area / Math.Max(0.05, backing.AreaPerItemM2);

        // A ghillie suit is woven of plant fiber.
        if (slot == ArmorSlotKind.Ghillie)
            e.MaterialUnits[ArmorsmithMechanics.GhillieFiberStack] = Math.Ceiling(ArmorsmithMechanics.GhillieFiberPerM2 * area);

        return check;
    }
}
