// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Gunsmith;

/// <summary>Where the evaluator finds the materials, the powders and the actions (the prototypes; in a test, whatever the test gives).</summary>
public interface IGunsmithData
{
    GunsmithMaterialPrototype? Material(string id);
    GunsmithPowderPrototype? Powder(string id);
    GunsmithActionPrototype? Action(string id);

    /// <summary>The preset that has the signature of the caliber, if there is one.</summary>
    GunsmithCaliberPrototype? CaliberByKey(string key);

    GunsmithLookPrototype? Look(string id);

    GunsmithClassPrototype? Class(string id);
}

// Gives the gunsmith rules the materials, powders, actions, looks, classes and calibers that are defined in prototypes.
public sealed class PrototypeGunsmithData : IGunsmithData
{
    private readonly IPrototypeManager _prototypes;
    private Dictionary<string, GunsmithCaliberPrototype>? _byKey;

    public PrototypeGunsmithData(IPrototypeManager prototypes)
    {
        _prototypes = prototypes;
    }

    public GunsmithMaterialPrototype? Material(string id) => _prototypes.TryIndex<GunsmithMaterialPrototype>(id, out var p) ? p : null;
    public GunsmithPowderPrototype? Powder(string id) => _prototypes.TryIndex<GunsmithPowderPrototype>(id, out var p) ? p : null;
    public GunsmithActionPrototype? Action(string id) => _prototypes.TryIndex<GunsmithActionPrototype>(id, out var p) ? p : null;
    public GunsmithLookPrototype? Look(string id) => _prototypes.TryIndex<GunsmithLookPrototype>(id, out var p) ? p : null;
    public GunsmithClassPrototype? Class(string id) => _prototypes.TryIndex<GunsmithClassPrototype>(id, out var p) ? p : null;

    public GunsmithCaliberPrototype? CaliberByKey(string key)
    {
        if (_byKey == null)
        {
            _byKey = new Dictionary<string, GunsmithCaliberPrototype>();
            foreach (var preset in _prototypes.EnumeratePrototypes<GunsmithCaliberPrototype>())
            {
                var spec = new CaliberSpec { BoreMm = preset.BoreMm, CaseDiameterMm = preset.CaseDiameterMm, CaseLengthMm = preset.CaseLengthMm };
                _byKey.TryAdd(spec.Key(), preset);
            }
        }

        return _byKey.GetValueOrDefault(key);
    }
}

/// <summary>
/// The verdict of the bench on a design: whether it can be built at all (<c>Errors</c>, which the window tells the player, since they are about the
/// shape of the input), and, if so, how it will behave (which the window never tells: the player has to work that out).
/// </summary>
public sealed class DesignCheck<T>
{
    public T Result = default!;
    public readonly List<string> Errors = new();
    public bool Valid => Errors.Count == 0;
}

// The rules of the gunsmith: checks a firearm or magazine design and works out its stats and cost.
public static class GunsmithEvaluator
{
    // What the grade of work asks of the material (its machinability), what it gives (quality), what it costs (waste) and adds (complexity).
    private static readonly double[] GradeNeed = { 0.30, 0.50, 0.70, 0.90 };
    private static readonly double[] GradeQuality = { 0.75, 1.00, 1.08, 1.15 };
    private static readonly double[] GradeWaste = { 1.15, 1.30, 1.55, 1.90 };
    private static readonly double[] GradeComplexity = { 0, 2, 5, 9 };

    // A handmade weapon is a little better than a plain one of the game, not much: the damage, the rate of fire and the accuracy are held back.
    private const double WeaponDamageShare = 0.85;
    private const double CycleSlowdown = 1.15;
    private const double MaxFireRate = 8.0;

    /// <summary>The least and the greatest rate of fire the maker can ask for, shots a second (a rate past what the barrel stands bursts it).</summary>
    public const double MinFireRate = 0.3;
    public const double MaxDesignFireRate = 20.0;
    private const double ScatterFactor = 1.15;
    private const double MinScatterDegrees = 3.0;

    // The least and the greatest of what the window accepts. These are shape checks; they say nothing of the strength.
    private const double MinBore = 3, MaxBore = 30;
    private const double MinBarrel = 40, MaxBarrel = 1200;
    private const double MaxOuter = 90;

    public static double Quality(ToleranceGrade grade, double machinability)
    {
        var g = (int) grade;
        var met = Math.Min(1.0, machinability / GradeNeed[g]);
        return GradeQuality[g] * Math.Pow(met, 1.5);
    }

    private static void Range(DesignCheck<FirearmEvaluation> check, string name, double value, double min, double max)
    {
        if (double.IsNaN(value) || value < min || value > max)
            check.Errors.Add($"{name}: {min:0.##}…{max:0.##}");
    }

    public static DesignCheck<CaliberSpec> CheckCaliber(CaliberSpec c, IGunsmithData data)
    {
        var check = new DesignCheck<CaliberSpec> { Result = c };
        void R(string name, double value, double min, double max)
        {
            if (double.IsNaN(value) || value < min || value > max)
                check.Errors.Add($"{name}: {min:0.##}…{max:0.##}");
        }

        R("bore", c.BoreMm, MinBore, MaxBore);
        R("case-diameter", c.CaseDiameterMm, c.BoreMm + 0.5, c.BoreMm * 2.4 + 1);
        R("case-length", c.CaseLengthMm, 8, 120);
        R("seat", c.SeatDepthMm, 1, Math.Max(1.5, c.CaseLengthMm * 0.6));
        R("bullet-mass", c.BulletMassG, 0.3, 150);
        R("charge", c.ChargeG, 0.02, 40);
        if (data.Powder(c.Powder) == null)
            check.Errors.Add("powder");

        return check;
    }

    /// <summary>Works out a firearm. Everything about the interior ballistics, the strength and the stats is filled in only when there are no errors.</summary>
    public static DesignCheck<FirearmEvaluation> Evaluate(FirearmDesign d, IGunsmithData data)
    {
        var e = new FirearmEvaluation();
        var check = new DesignCheck<FirearmEvaluation> { Result = e };

        var action = data.Action(d.Action);
        var powder = data.Powder(d.Caliber.Powder);
        var barrelMat = data.Material(d.BarrelMaterial);
        var boltMat = data.Material(d.BoltMaterial);
        var receiverMat = data.Material(d.ReceiverMaterial);
        var stockMat = d.Stock == StockKind.None ? receiverMat : data.Material(d.StockMaterial);

        if (action == null)
            check.Errors.Add("action");
        if (barrelMat == null)
            check.Errors.Add("barrel-material");
        if (boltMat == null)
            check.Errors.Add("bolt-material");
        if (receiverMat == null)
            check.Errors.Add("receiver-material");
        if (stockMat == null)
            check.Errors.Add("stock-material");

        var caliber = d.Caliber;
        check.Errors.AddRange(CheckCaliber(caliber, data).Errors);

        // The look is a picture of a weapon of a class; the weapon is of that class, and a class has its own limits: a caliber too big for a pistol is not a pistol's.
        var weaponClass = data.Class(d.Class);
        if (weaponClass == null)
            check.Errors.Add("class");

        if (!string.IsNullOrEmpty(d.Look))
        {
            var look = data.Look(d.Look);
            if (look == null)
                check.Errors.Add("look");
            else if (look.Class != d.Class)
                check.Errors.Add("look-class");
        }

        if (weaponClass != null)
        {
            if (caliber.BoreMm < weaponClass.MinBoreMm || caliber.BoreMm > weaponClass.MaxBoreMm)
                check.Errors.Add($"class-bore: {weaponClass.MinBoreMm:0.##}…{weaponClass.MaxBoreMm:0.##}");
            if (caliber.ChargeG > weaponClass.MaxChargeG)
                check.Errors.Add($"class-charge: ≤{weaponClass.MaxChargeG:0.##}");
            if (caliber.CaseLengthMm > weaponClass.MaxCaseLengthMm)
                check.Errors.Add($"class-case: ≤{weaponClass.MaxCaseLengthMm:0.##}");
            if (d.BarrelLengthMm < weaponClass.MinBarrelMm || d.BarrelLengthMm > weaponClass.MaxBarrelMm)
                check.Errors.Add($"class-barrel: {weaponClass.MinBarrelMm:0.##}…{weaponClass.MaxBarrelMm:0.##}");
            if (weaponClass.Stocks.Count > 0 && !weaponClass.Stocks.Contains(d.Stock))
                check.Errors.Add("class-stock");
        }

        if (d.Feed != FeedKind.Magazine && (d.FeedCapacity < 2 || d.FeedCapacity > 12))
            check.Errors.Add("feed-capacity: 2…12");

        if (d.WeaponName.Length > FirearmDesign.MaxNameLength * 2)
            check.Errors.Add("name");
        if (d.WeaponDescription.Length > FirearmDesign.MaxDescriptionLength * 2)
            check.Errors.Add("description");

        Range(check, "barrel-length", d.BarrelLengthMm, MinBarrel, MaxBarrel);
        Range(check, "barrel-outer", d.BarrelOuterMm, caliber.CaseDiameterMm + 2, MaxOuter);
        Range(check, "bolt-diameter", d.BoltDiameterMm, caliber.CaseDiameterMm, 70);
        Range(check, "pin-hole", d.PinHoleMm, 0, Math.Max(0.5, d.BoltDiameterMm * 0.6));
        if (d.FireRate != 0)
            Range(check, "fire-rate", d.FireRate, MinFireRate, MaxDesignFireRate);
        Range(check, "lugs", d.Lugs, 1, 8);
        Range(check, "lug-width", d.LugWidthMm, 1, 30);
        Range(check, "lug-length", d.LugLengthMm, 1, 40);
        Range(check, "lug-depth", d.LugDepthMm, 0.5, 15);
        if (d.Stock != StockKind.None)
        {
            Range(check, "stock-width", d.StockWidthMm, 8, 120);
            Range(check, "stock-height", d.StockHeightMm, 8, 160);
            Range(check, "stock-length", d.StockLengthMm, 60, 700);
        }

        if (d.Stock == StockKind.Folding)
            Range(check, "hinge-pin", d.HingePinMm, 2, 20);

        if (!check.Valid)
            return check;

        // -------------------------------------------------------------- interior ballistics
        var volume = GunsmithMechanics.CaseVolumeCm3(caliber.CaseDiameterMm, caliber.CaseLengthMm, caliber.SeatDepthMm);
        var density = GunsmithMechanics.LoadingDensity(caliber.ChargeG, volume);
        var pressure = GunsmithMechanics.PeakPressureMpa(powder!.Force, powder.Covolume, density);
        var efficiency = GunsmithMechanics.BarrelEfficiency(powder.Efficiency, d.BarrelLengthMm, caliber.BoreMm);
        var velocity = GunsmithMechanics.MuzzleVelocity(efficiency, caliber.ChargeG, powder.Energy, caliber.BulletMassG);

        var energy = GunsmithMechanics.MuzzleEnergyJ(caliber.BulletMassG, velocity);
        var impulse = GunsmithMechanics.RecoilImpulse(caliber.BulletMassG, caliber.ChargeG, velocity);
        var timeInBore = GunsmithMechanics.TimeInBore(d.BarrelLengthMm, velocity);

        e.CaseVolumeCm3 = volume;
        e.LoadingDensity = density;
        e.PressureMpa = pressure;
        e.MuzzleVelocity = velocity;
        e.MuzzleEnergyJ = energy;
        e.RecoilImpulse = impulse;
        e.TimeInBoreS = timeInBore;
        e.RecoilForceN = GunsmithMechanics.PeakForce(impulse, timeInBore);

        // -------------------------------------------------------------- masses
        var boltLength = action!.BoltLengthMm;
        var barrelVolume = GunsmithMechanics.TubeVolumeCm3(d.BarrelOuterMm, caliber.BoreMm, d.BarrelLengthMm);
        var boltVolume = Math.PI / 4.0 * (d.BoltDiameterMm * d.BoltDiameterMm - d.PinHoleMm * d.PinHoleMm) * boltLength / 1000.0
                         + d.Lugs * d.LugWidthMm * d.LugDepthMm * d.LugLengthMm / 1000.0;
        var receiverInner = Math.Max(d.BarrelOuterMm, d.BoltDiameterMm);
        var receiverVolume = GunsmithMechanics.TubeVolumeCm3(receiverInner + 6, receiverInner, boltLength + 120)
                             + action.FixedMassG / Math.Max(1.0, receiverMat!.DensityGcm3);
        var stockVolume = d.Stock == StockKind.None ? 0.0 : GunsmithMechanics.BarVolumeCm3(d.StockWidthMm, d.StockHeightMm, d.StockLengthMm);

        e.BarrelMassG = GunsmithMechanics.MassG(barrelMat!.DensityGcm3, barrelVolume);
        e.BoltMassG = GunsmithMechanics.MassG(boltMat!.DensityGcm3, boltVolume);
        e.ReceiverMassG = GunsmithMechanics.MassG(receiverMat.DensityGcm3, receiverVolume);
        e.StockMassG = d.Stock == StockKind.None ? 0 : GunsmithMechanics.MassG(stockMat!.DensityGcm3, stockVolume);
        e.TotalMassG = e.BarrelMassG + e.BoltMassG + e.ReceiverMassG + e.StockMassG;
        e.RecoilVelocity = GunsmithMechanics.RecoilVelocity(impulse, e.TotalMassG);

        // -------------------------------------------------------------- strength
        var hoop = GunsmithMechanics.BarrelHoopMpa(pressure, d.BarrelOuterMm, caliber.CaseDiameterMm);
        var axial = GunsmithMechanics.BarrelAxialMpa(pressure, d.BarrelOuterMm, caliber.CaseDiameterMm);
        var mises = GunsmithMechanics.VonMises(hoop, -pressure, axial);
        e.BarrelHoopMpa = hoop;
        e.BarrelAxialMpa = axial;
        e.BarrelMisesMpa = mises;
        e.BarrelSafety = barrelMat.YieldMpa / mises;
        e.BarrelFatigueSafety = GunsmithMechanics.GoodmanSafety(mises, barrelMat.EnduranceMpa, barrelMat.UltimateMpa);

        var thrust = GunsmithMechanics.BoltThrustN(pressure, caliber.CaseDiameterMm);
        var shear = GunsmithMechanics.LugShearMpa(thrust, d.Lugs, d.LugWidthMm, d.LugLengthMm);
        var bearing = GunsmithMechanics.LugBearingMpa(thrust, d.Lugs, d.LugWidthMm, d.LugDepthMm);
        var tension = GunsmithMechanics.BoltTensionMpa(thrust, d.BoltDiameterMm, d.PinHoleMm);
        var weakerYield = Math.Min(boltMat.YieldMpa, receiverMat.YieldMpa);
        e.BoltThrustN = thrust;
        e.LugShearSafety = GunsmithMechanics.ShearYieldRatio * weakerYield / shear;
        e.LugBearingSafety = weakerYield / bearing;
        e.BoltTensionSafety = boltMat.YieldMpa / tension;

        var lugEquivalent = Math.Sqrt(3.0) * shear;
        var lugFatigue = GunsmithMechanics.GoodmanSafety(lugEquivalent, Math.Min(boltMat.EnduranceMpa, receiverMat.EnduranceMpa),
            Math.Min(boltMat.UltimateMpa, receiverMat.UltimateMpa));
        var boltFatigue = GunsmithMechanics.GoodmanSafety(tension, boltMat.EnduranceMpa, boltMat.UltimateMpa);

        if (d.Stock == StockKind.None)
        {
            e.StockSafety = double.PositiveInfinity;
            e.HingeSafety = double.PositiveInfinity;
        }
        else
        {
            var stockStress = GunsmithMechanics.StockStressMpa(impulse, d.StockLengthMm, d.StockWidthMm, d.StockHeightMm);
            e.StockSafety = stockMat!.YieldMpa / stockStress;
            e.HingeSafety = d.Stock == StockKind.Folding
                ? GunsmithMechanics.ShearYieldRatio * receiverMat.YieldMpa
                  / GunsmithMechanics.PinDoubleShearMpa(GunsmithMechanics.StockForceN(impulse), d.HingePinMm)
                : double.PositiveInfinity;
        }

        e.WeakestSafety = new[] { e.BarrelSafety, e.LugShearSafety, e.LugBearingSafety, e.BoltTensionSafety }.Min();
        e.FatigueLifeShots = new[]
        {
            GunsmithMechanics.FatigueShots(e.BarrelFatigueSafety),
            GunsmithMechanics.FatigueShots(lugFatigue),
            GunsmithMechanics.FatigueShots(boltFatigue),
        }.Min();
        e.HeatPerShotK = GunsmithMechanics.HeatPerShotK(caliber.ChargeG, powder.Energy, e.BarrelMassG, barrelMat.SpecificHeat);

        // -------------------------------------------------------------- what the weapon is like
        var machinability = (barrelMat.Machinability + boltMat.Machinability + receiverMat.Machinability) / 3.0;
        e.Quality = Quality(d.Grade, machinability);
        e.Reliability = Math.Clamp(0.55 + 0.45 * Math.Min(e.Quality, 1.1), 0.5, 1.0);
        e.Modes = action.Modes;

        e.Damage = WeaponDamageShare * Math.Min(120.0, 5.0 + 0.095 * Math.Pow(Math.Max(1.0, energy), 0.8));

        var cycle = CycleSlowdown * action.CycleSeconds * Math.Sqrt(Math.Max(e.BoltMassG, 20.0) / 120.0) / Math.Max(0.4, e.Quality);
        e.NaturalFireRate = Math.Clamp(1.0 / cycle, 0.3, MaxFireRate);
        e.FireRate = d.FireRate > 0 ? d.FireRate : e.NaturalFireRate;

        // A rate of fire the barrel cannot get rid of the heat of: it gets hot to the very limit, and a hot barrel is weak.
        e.SustainedTempC = GunsmithMechanics.SustainedTempC(e.HeatPerShotK, e.FireRate, GunsmithConstants.CoolingHalfLifeSeconds);
        e.RateSafety = GunsmithMechanics.HotYieldMpa(barrelMat.YieldMpa, e.SustainedTempC, barrelMat.LimitTempC) / Math.Max(1e-6, mises);

        var scatter = ScatterFactor * 14.0 * Math.Sqrt(400.0 / d.BarrelLengthMm) * Math.Pow(3000.0 / Math.Max(500.0, e.TotalMassG), 0.25) / Math.Max(0.4, e.Quality);
        e.ScatterDegrees = Math.Clamp(scatter, MinScatterDegrees, 25.0);

        var recoilEnergy = impulse * impulse / (2.0 * Math.Max(0.2, e.TotalMassG / 1000.0));
        e.RecoilFactor = Math.Clamp(0.6 + recoilEnergy / 3.0, 0.5, 6.0);

        // -------------------------------------------------------------- building
        var complexity = action.Complexity + GradeComplexity[(int) d.Grade] + (d.Feed == FeedKind.Revolver ? 6 : d.Feed == FeedKind.Internal ? 4 : 0)
                         + (d.Stock == StockKind.Folding ? 4 : 0) + Math.Max(0, d.Lugs - 2);
        e.Complexity = complexity;
        e.BuildSeconds = 25 + 4 * complexity;

        var waste = GradeWaste[(int) d.Grade];
        void Add(GunsmithMaterialPrototype material, double volumeCm3)
        {
            var units = volumeCm3 * waste / Math.Max(1.0, material.VolumeCm3);
            e.MaterialUnits[material.ID] = e.MaterialUnits.GetValueOrDefault(material.ID) + units;
        }

        Add(barrelMat, barrelVolume);
        Add(boltMat, boltVolume);
        Add(receiverMat, receiverVolume);
        if (d.Stock != StockKind.None)
            Add(stockMat!, stockVolume);

        foreach (var key in e.MaterialUnits.Keys.ToArray())
            e.MaterialUnits[key] = Math.Max(1, Math.Ceiling(e.MaterialUnits[key]));

        return check;
    }

    /// <summary>Works out a magazine for a caliber.</summary>
    public static DesignCheck<MagazineEvaluation> EvaluateMagazine(MagazineDesign m, IGunsmithData data)
    {
        var e = new MagazineEvaluation();
        var check = new DesignCheck<MagazineEvaluation> { Result = e };
        var body = data.Material(m.BodyMaterial);
        var spring = data.Material(m.SpringMaterial);

        if (body == null)
            check.Errors.Add("body-material");
        if (spring == null)
            check.Errors.Add("spring-material");

        check.Errors.AddRange(CheckCaliber(m.Caliber, data).Errors);

        void R(string name, double value, double min, double max)
        {
            if (double.IsNaN(value) || value < min || value > max)
                check.Errors.Add($"{name}: {min:0.##}…{max:0.##}");
        }

        R("capacity", m.Capacity, 1, 100);
        R("wall", m.WallMm, 0.2, 5);
        R("wire", m.WireMm, 0.2, 4);
        R("coil", m.CoilMm, 2, 40);
        R("turns", m.Turns, 2, 60);
        R("free-length", m.FreeLengthMm, 10, 600);

        if (!check.Valid)
            return check;

        var c = m.Caliber;
        var pitch = c.CaseDiameterMm * 1.05;
        var solid = GunsmithMechanics.SolidLengthMm(m.Turns, m.WireMm);
        var follower = 4.0;
        var bodyLength = m.Capacity * pitch + solid + 1.0 + follower + 2.0;

        e.PitchMm = pitch;
        e.BodyLengthMm = bodyLength;
        e.SolidLengthMm = solid;
        e.SpringIndex = GunsmithMechanics.SpringIndex(m.CoilMm, m.WireMm);
        e.WahlFactor = GunsmithMechanics.WahlFactor(e.SpringIndex);
        e.SpringRateNPerMm = GunsmithMechanics.SpringRate(spring!.ShearGpa * 1000.0, m.WireMm, m.CoilMm, m.Turns);

        // The spring at full load is squeezed to its solid length (and a millimetre more): this is its hardest moment.
        var fullLength = solid + 1.0;
        e.SpringSolid = m.FreeLengthMm <= fullLength + 0.001;
        e.FullForceN = e.SpringSolid ? 0.0 : e.SpringRateNPerMm * (m.FreeLengthMm - fullLength);
        e.SpringShearMpa = GunsmithMechanics.SpringShearMpa(e.FullForceN, m.CoilMm, m.WireMm, e.WahlFactor);
        var allowable = GunsmithMechanics.SpringAllowableShare * GunsmithMechanics.SpringWireFactor * spring.UltimateMpa;
        e.SpringSafety = e.SpringShearMpa <= 0 ? double.PositiveInfinity : allowable / e.SpringShearMpa;

        var lipStress = GunsmithMechanics.CantileverStressMpa(e.FullForceN, GunsmithMechanics.LipLengthMm, GunsmithMechanics.LipWidthMm, m.WallMm);
        e.LipSafety = lipStress <= 0 ? double.PositiveInfinity : body!.YieldMpa / lipStress;

        // How many of the positions of the stack the spring pushes well enough.
        var good = 0;
        for (var left = 1; left <= m.Capacity; left++)
        {
            var length = fullLength + (m.Capacity - left) * pitch;
            var force = Math.Max(0.0, e.SpringRateNPerMm * (m.FreeLengthMm - length));
            if (!e.SpringSolid && force >= GunsmithMechanics.FeedForceNeededN(c.BulletMassG + c.ChargeG + 3.0, left))
                good++;
        }

        e.ReliableShare = good / (double) m.Capacity;

        // The mass: the walls of the box and the wire of the spring.
        var width = c.CaseDiameterMm + 2 * m.WallMm + 1;
        var depth = c.CaseLengthMm + 2;
        var wallVolume = (2.0 * (width + depth) * m.WallMm * bodyLength + 2.0 * width * depth * m.WallMm) / 1000.0;
        var wireVolume = Math.PI / 4.0 * m.WireMm * m.WireMm * Math.PI * m.CoilMm * (m.Turns + 2) / 1000.0;
        e.MassG = body!.DensityGcm3 * wallVolume + spring.DensityGcm3 * wireVolume;

        e.Quality = Quality(m.Grade, (body.Machinability + spring.Machinability) / 2.0);
        e.Complexity = 4 + GradeComplexity[(int) m.Grade] + m.Capacity / 8.0;
        e.BuildSeconds = 15 + 3 * e.Complexity;

        var waste = GradeWaste[(int) m.Grade];
        e.MaterialUnits[body.ID] = Math.Max(1, Math.Ceiling(wallVolume * waste / Math.Max(1.0, body.VolumeCm3)));
        e.MaterialUnits[spring.ID] = e.MaterialUnits.GetValueOrDefault(spring.ID) + Math.Max(1, Math.Ceiling(wireVolume * waste / Math.Max(1.0, spring.VolumeCm3)));
        return check;
    }
}
