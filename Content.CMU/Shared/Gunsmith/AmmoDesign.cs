// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

// What the ammunition rules need to know: the gunsmith data plus the materials and payloads of cartridges.
public interface IAmmoData : IGunsmithData
{
    AmmoMaterialPrototype? AmmoMaterial(string id);
    AmmoPayloadPrototype? Payload(string reagent);
}

// Gives the ammunition rules the materials and payloads that are defined in prototypes.
public sealed class PrototypeAmmoData : IAmmoData
{
    private readonly IPrototypeManager _prototypes;
    private readonly PrototypeGunsmithData _gunsmith;
    private Dictionary<string, AmmoPayloadPrototype>? _payloads;

    public PrototypeAmmoData(IPrototypeManager prototypes)
    {
        _prototypes = prototypes;
        _gunsmith = new PrototypeGunsmithData(prototypes);
    }

    public GunsmithMaterialPrototype? Material(string id) => _gunsmith.Material(id);
    public GunsmithPowderPrototype? Powder(string id) => _gunsmith.Powder(id);
    public GunsmithActionPrototype? Action(string id) => _gunsmith.Action(id);
    public GunsmithLookPrototype? Look(string id) => _gunsmith.Look(id);
    public GunsmithClassPrototype? Class(string id) => _gunsmith.Class(id);
    public GunsmithCaliberPrototype? CaliberByKey(string key) => _gunsmith.CaliberByKey(key);

    public AmmoMaterialPrototype? AmmoMaterial(string id) => _prototypes.TryIndex<AmmoMaterialPrototype>(id, out var p) ? p : null;

    public AmmoPayloadPrototype? Payload(string reagent)
    {
        if (_payloads == null)
        {
            _payloads = new Dictionary<string, AmmoPayloadPrototype>();
            foreach (var payload in _prototypes.EnumeratePrototypes<AmmoPayloadPrototype>())
                _payloads[payload.Reagent] = payload;
        }

        return _payloads.GetValueOrDefault(reagent);
    }
}

/// <summary>
/// The kind of the bullet. A bullet is what it is made of (a core, a jacket, a tip, a liquid, a powder load); buckshot is a load of pellets of one metal with
/// none of that; heavy buckshot is the same for the shells of the heavy shotguns, and only for them.
/// </summary>
[Serializable, NetSerializable]
public enum AmmoBulletKind : byte
{
    Bullet,
    Buckshot,
    HeavyBuckshot,

    /// <summary>A syringe bullet: a hollow body with any liquid in it (up to 20 units), which is injected into whoever it hits.</summary>
    Syringe,
}

/// <summary>Everything the player decides about a cartridge.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class AmmoDesign
{
    /// <summary>The caliber and the nominal load. The bullet mass here is the nominal one: the real mass depends on the materials.</summary>
    [DataField]
    public CaliberSpec Caliber = new();

    [DataField]
    public string Core = "AmmoMaterialLead";

    [DataField]
    public string Jacket = "AmmoMaterialCopper";

    /// <summary>The tip of the bullet; empty for none.</summary>
    [DataField]
    public string Tip = string.Empty;

    [DataField]
    public string Casing = "AmmoMaterialBrass";

    /// <summary>The powder load, −3 to +4: how much more or less than the nominal charge goes in.</summary>
    [DataField]
    public int PowderLevel;

    /// <summary>The reagent that fills the hollow core of the bullet; empty for none.</summary>
    [DataField]
    public string Payload = string.Empty;

    /// <summary>How many pellets the load is shot in: one bullet (a slug), or shot of several pellets. The bullet decides that, not the caliber.</summary>
    [DataField]
    public int Pellets = 1;

    [DataField]
    public AmmoBulletKind Kind = AmmoBulletKind.Bullet;

    /// <summary>For a syringe bullet: how many units of the liquid it carries (1 to 20).</summary>
    [DataField]
    public double PayloadUnits = 10;

    public AmmoDesign Clone() => new()
    {
        Caliber = Caliber.Clone(),
        Core = Core,
        Jacket = Jacket,
        Tip = Tip,
        Casing = Casing,
        PowderLevel = PowderLevel,
        Payload = Payload,
        Pellets = Pellets,
        Kind = Kind,
        PayloadUnits = PayloadUnits,
    };
}

/// <summary>What the bench worked out for a cartridge. Never shown to the player: the player is to work it out.</summary>
public sealed class AmmoEvaluation
{
    /// <summary>The load the cartridge really has: the real mass of the bullet and the real charge.</summary>
    public CaliberSpec Load = new();

    public double BulletMassG;
    public double DamageMultiplier = 1.0;
    public double ArmorPierceBonus;
    public double ArmorPierce;
    public int ArmorLevel;
    public double Seal = 1.0;
    public double Jam;
    public Dictionary<string, double> Extra = new();

    public string PayloadReagent = string.Empty;
    public double PayloadUnits;
    public double InjectUnits;
    public bool Ignite;
    public string? PayloadDamageType;
    public double PayloadDamagePerUnit;

    /// <summary>The materials of one cartridge, in items of their stacks, by the id of the gunsmith's material (fractions).</summary>
    public Dictionary<string, double> MaterialUnits = new();

    public double PowderUnits;
    public double BuildSeconds;
    public double ReferenceEnergyJ;
    public double ReferencePressureMpa;

    /// <summary>Whether the cartridge is a shotgun shell, and how many pellets it holds (1 for a cartridge with one bullet).</summary>
    public bool Shell;
    public bool Heavy;
    public int Pellets = 1;

    /// <summary>The barrel the cartridge is rated in for a weapon of the game, mm.</summary>
    public double RatedBarrelMm;

    /// <summary>The damage of one bullet (one pellet) in that barrel, before what the liquid does by itself.</summary>
    public double RatedDamage;

    /// <summary>The energy of the plain load of the preset in the barrel it is rated in (what the damage of the game is fitted to), J; 0 when the caliber has no preset.</summary>
    public double BaselineEnergyJ;

    /// <summary>The energy of this cartridge in that barrel, J.</summary>
    public double RatedEnergyJ;

    public double GameDamage;
    public double GameMaxDamage;
    public int NominalPellets = 1;

    public AmmoBulletKind Kind;

    /// <summary>The damage of one bullet (a pellet) by type, with what the materials and the liquid add.</summary>
    public Dictionary<string, double> Damage() =>
        AmmoMechanics.ProjectileDamage(RatedDamage, Extra, PayloadDamageType, PayloadDamagePerUnit, PayloadUnits, Pellets);
}

// The numbers of the ammunition rules: powder levels, case and jacket sizes, shell pellets and the like.
public static class AmmoMechanics
{
    /// <summary>The powder load levels and the share of the nominal charge each puts in.</summary>
    public static readonly double[] PowderFactors = { 0.55, 0.70, 0.85, 1.00, 1.12, 1.25, 1.40, 1.55 };
    public const int MinPowderLevel = -3;
    public const int MaxPowderLevel = 4;

    /// <summary>The most liquid a syringe bullet carries, units.</summary>
    public const double MaxSyringeUnits = 20.0;

    /// <summary>The share of the volume of a syringe bullet that its walls take (the rest is the hollow), and how much of the damage of a bullet of that energy it does.</summary>
    public const double SyringeBodyShare = 0.4;
    public const double SyringeDamageShare = 0.3;

    /// <summary>The pellet counts a shell can be made with (each has a prototype of its own).</summary>
    public static readonly int[] ShellPellets = { 1, 4, 6, 8, 9, 12, 16, 20 };

    /// <summary>The density the nominal mass of a bullet is taken at (a jacketed lead bullet), g/cm³.</summary>
    public const double NominalBulletDensity = 10.0;

    public const double CaseWallMm = 0.7;
    public const double CaseHeadMm = 1.5;

    /// <summary>The shares of the volume of the bullet: the jacket, and the core with or without a tip.</summary>
    public const double JacketShare = 0.28;
    public const double CoreShareNoTip = 0.72;
    public const double CoreShareTip = 0.62;
    public const double TipShare = 0.10;

    /// <summary>The share of the volume of the core that the hollow of the payload takes.</summary>
    public const double CavityShare = 0.08;

    /// <summary>Units of liquid the hollow carries, for every cm³ of it.</summary>
    public const double UnitsPerCm3 = 5.0;

    /// <summary>The barrel the armor penetration is rated in, in calibers.</summary>
    public const double ReferenceBarrelCalibers = 20.0;

    /// <summary>The armor penetration of the projectiles that exist.</summary>
    public static readonly int[] ArmorLevels = { 0, 5, 10, 15, 20, 25, 30, 35, 40, 50 };

    public static double PowderFactor(int level) => PowderFactors[Math.Clamp(level, MinPowderLevel, MaxPowderLevel) - MinPowderLevel];

    /// <summary>The armor penetration the energy alone gives a bullet: AP = 0.45·(E/A)^0.9, E in J, A the area of the bore in mm².</summary>
    public static double BaseArmorPierce(double energyJ, double boreMm)
    {
        var area = Math.PI / 4.0 * boreMm * boreMm;
        return 0.45 * Math.Pow(Math.Max(0.01, energyJ / area), 0.9);
    }

    public static int NearestArmorLevel(double armorPierce)
    {
        var best = ArmorLevels[0];
        foreach (var level in ArmorLevels)
        {
            if (Math.Abs(level - armorPierce) < Math.Abs(best - armorPierce))
                best = level;
        }

        return best;
    }

    /// <summary>
    /// The damage of a bullet in the game, fitted: the damage of the plain cartridge of the caliber times the square root of how much more energy this bullet has
    /// (per bullet: a load of pellets shares the energy) times what its materials do, and never more than twice the strongest bullet of that caliber in the game.
    /// </summary>
    public static double ScaledDamage(double gameDamage, double gameMax, double energyJ, double baselineJ, int pellets, int nominalPellets, double multiplier)
    {
        var perBullet = energyJ / Math.Max(1, pellets);
        var plain = baselineJ / Math.Max(1, nominalPellets);
        var damage = gameDamage * Math.Sqrt(Math.Max(0.01, perBullet / Math.Max(1.0, plain))) * multiplier;
        return gameMax > 0 ? Math.Min(damage, gameMax * MaxOverGame) : damage;
    }

    /// <summary>How many times the damage of the strongest bullet of the caliber in the game a bullet made at the bench can come to, at the most.</summary>
    public const double MaxOverGame = 1.25;

    /// <summary>The damage of one bullet (a pellet) by type: the piercing damage, the extra damage of the materials as shares of it, and the damage of the liquid.</summary>
    public static Dictionary<string, double> ProjectileDamage(double piercing, Dictionary<string, double> extra, string? payloadType, double payloadPerUnit, double payloadUnits, int pellets)
    {
        var damage = new Dictionary<string, double>();
        void Add(string type, double amount)
        {
            if (amount > 0)
                damage[type] = damage.GetValueOrDefault(type) + amount;
        }

        Add("Piercing", piercing);
        foreach (var (type, share) in extra)
            Add(type, piercing * share);

        if (payloadType != null && payloadPerUnit > 0)
            Add(payloadType, payloadPerUnit * payloadUnits / Math.Max(1, pellets));

        return damage;
    }

    /// <summary>The damage of a bullet of this energy: the same law as for the weapons.</summary>
    public static double DamageOfEnergy(double energyJ) => Math.Min(120.0, 5.0 + 0.095 * Math.Pow(Math.Max(1.0, energyJ), 0.8));

    /// <summary>The pressure of the load in the case, MPa.</summary>
    public static double PressureOf(CaliberSpec load, GunsmithPowderPrototype powder)
    {
        var volume = GunsmithMechanics.CaseVolumeCm3(load.CaseDiameterMm, load.CaseLengthMm, load.SeatDepthMm);
        return GunsmithMechanics.PeakPressureMpa(powder.Force, powder.Covolume, GunsmithMechanics.LoadingDensity(load.ChargeG, volume));
    }

    /// <summary>The energy of the load from a barrel of the length, J: the seal of the casing counts as a share of the energy lost.</summary>
    public static double EnergyIn(CaliberSpec load, GunsmithPowderPrototype powder, double barrelLengthMm, double seal, out double velocity)
    {
        var efficiency = GunsmithMechanics.BarrelEfficiency(powder.Efficiency, barrelLengthMm, load.BoreMm) * seal;
        velocity = GunsmithMechanics.MuzzleVelocity(efficiency, load.ChargeG, powder.Energy, load.BulletMassG);
        return GunsmithMechanics.MuzzleEnergyJ(load.BulletMassG, velocity);
    }
}

// Checks a cartridge design and works out what it does and what a batch costs.
public static class AmmoEvaluator
{
    private static string Name(AmmoPart part) => part.ToString().ToLowerInvariant();

    public static DesignCheck<AmmoEvaluation> Evaluate(AmmoDesign d, IAmmoData data)
    {
        var e = new AmmoEvaluation();
        var check = new DesignCheck<AmmoEvaluation> { Result = e };

        check.Errors.AddRange(GunsmithEvaluator.CheckCaliber(d.Caliber, data).Errors);

        var preset = data.CaliberByKey(d.Caliber.Key());
        var shell = preset?.Shell == true;

        // The kinds. A plain bullet is made of a core, a jacket, a tip, powder and a fuel. Buckshot is a load of pellets of one metal (and powder, and a fuel).
        // A syringe bullet is a hollow body (the jacket's metal) with any liquid in it.
        var plain = d.Kind == AmmoBulletKind.Bullet;
        var syringe = d.Kind == AmmoBulletKind.Syringe;
        var shot = d.Kind is AmmoBulletKind.Buckshot or AmmoBulletKind.HeavyBuckshot;

        if (d.Kind == AmmoBulletKind.HeavyBuckshot && preset?.Heavy != true)
            check.Errors.Add("kind");

        if (shot && !AmmoMechanics.ShellPellets.Contains(d.Pellets))
            check.Errors.Add("pellets");

        if (d.PowderLevel < AmmoMechanics.MinPowderLevel || d.PowderLevel > AmmoMechanics.MaxPowderLevel)
            check.Errors.Add($"powder-level: {AmmoMechanics.MinPowderLevel}…{AmmoMechanics.MaxPowderLevel}");

        AmmoMaterialPrototype? Pick(string id, AmmoPart part, bool optional)
        {
            if (optional && string.IsNullOrEmpty(id))
                return null;

            var material = data.AmmoMaterial(id);
            if (material == null || !material.Parts.ContainsKey(part) || data.Material(material.Material) == null)
            {
                check.Errors.Add($"{Name(part)}-material");
                return null;
            }

            return material;
        }

        var core = syringe ? null : Pick(d.Core, AmmoPart.Core, false);
        var jacket = plain || syringe ? Pick(d.Jacket, AmmoPart.Jacket, false) : null;
        var tip = plain ? Pick(d.Tip, AmmoPart.Tip, true) : null;
        var casing = Pick(d.Casing, AmmoPart.Casing, false);

        // The liquid: a plain bullet and buckshot carry only the fuels of the flamethrowers; a syringe bullet carries any liquid, up to 20 units.
        AmmoPayloadPrototype? payload = null;
        var hasPayload = !string.IsNullOrEmpty(d.Payload);
        if (hasPayload)
        {
            if (d.Payload.Length > 64)
                check.Errors.Add("payload");
            else
                payload = data.Payload(d.Payload);

            if (!syringe && payload is not { Ignite: true })
                check.Errors.Add("payload-fuel");
        }

        if (syringe)
        {
            if (!hasPayload)
                check.Errors.Add("payload");

            if (d.PayloadUnits < 1 || d.PayloadUnits > AmmoMechanics.MaxSyringeUnits || double.IsNaN(d.PayloadUnits))
                check.Errors.Add($"payload-units: 1…{AmmoMechanics.MaxSyringeUnits}");
        }

        if (!check.Valid)
            return check;

        var coreMat = core == null ? null : data.Material(core.Material);
        var jacketMat = jacket == null ? null : data.Material(jacket.Material);
        var casingMat = data.Material(casing!.Material)!;
        var tipMat = tip == null ? null : data.Material(tip.Material);
        var powder = data.Powder(d.Caliber.Powder)!;

        // The bullet: the volume is the nominal mass over the nominal density; the parts take their shares of it.
        var volume = d.Caliber.BulletMassG / AmmoMechanics.NominalBulletDensity;
        double coreVolume = 0, jacketVolume = 0, tipVolume = 0, cavity = 0;
        if (plain)
        {
            var coreShare = tip == null ? AmmoMechanics.CoreShareNoTip : AmmoMechanics.CoreShareTip;
            cavity = hasPayload ? volume * coreShare * AmmoMechanics.CavityShare : 0.0;
            coreVolume = volume * coreShare - cavity;
            jacketVolume = volume * AmmoMechanics.JacketShare;
            tipVolume = tip == null ? 0.0 : volume * AmmoMechanics.TipShare;
        }
        else if (shot)
        {
            cavity = hasPayload ? volume * AmmoMechanics.CavityShare : 0.0;
            coreVolume = volume - cavity;
        }
        else
        {
            // A hollow body with a needle: the walls are a share of the volume of the bullet.
            jacketVolume = volume * AmmoMechanics.SyringeBodyShare;
        }

        e.BulletMassG = coreVolume * (coreMat?.DensityGcm3 ?? 0.0) + jacketVolume * (jacketMat?.DensityGcm3 ?? 0.0) + tipVolume * (tipMat?.DensityGcm3 ?? 0.0);

        // What the materials do.
        void Apply(AmmoMaterialPrototype? material, AmmoPart part)
        {
            if (material == null)
                return;

            var effect = material.Parts[part];
            e.DamageMultiplier *= effect.Damage;
            e.ArmorPierceBonus += effect.ArmorPierce;
            e.Jam += effect.Jam;
            foreach (var (type, amount) in effect.Extra)
                e.Extra[type] = e.Extra.GetValueOrDefault(type) + amount;
        }

        Apply(core, AmmoPart.Core);
        Apply(jacket, AmmoPart.Jacket);
        Apply(tip, AmmoPart.Tip);
        Apply(casing, AmmoPart.Casing);
        e.Seal = casing.Parts[AmmoPart.Casing].Seal;

        // The load, as the weapon will meet it.
        e.Load = d.Caliber.Clone();
        e.Load.BulletMassG = e.BulletMassG;
        e.Load.ChargeG = d.Caliber.ChargeG * AmmoMechanics.PowderFactor(d.PowderLevel);

        // The liquid.
        e.PayloadReagent = hasPayload ? d.Payload : string.Empty;
        if (hasPayload)
        {
            if (syringe)
            {
                e.PayloadUnits = Math.Round(Math.Clamp(d.PayloadUnits, 1.0, AmmoMechanics.MaxSyringeUnits), 1);
                e.InjectUnits = e.PayloadUnits;
            }
            else
            {
                e.PayloadUnits = Math.Max(0.2, Math.Round(cavity * AmmoMechanics.UnitsPerCm3, 1));
                e.Ignite = payload!.Ignite;
                e.InjectUnits = payload.Inject ? e.PayloadUnits * payload.InjectShare : 0.0;
            }

            // What the liquid does by itself (an acid eats) is in the table of the liquids, if the liquid is there.
            e.PayloadDamageType = payload?.DamageType;
            e.PayloadDamagePerUnit = payload?.DamagePerUnit ?? 0.0;
        }

        // The armor penetration of the bullet in the barrel it is rated in, and the projectile that has it.
        e.ReferenceEnergyJ = AmmoMechanics.EnergyIn(e.Load, powder, AmmoMechanics.ReferenceBarrelCalibers * d.Caliber.BoreMm, e.Seal, out _);
        e.ReferencePressureMpa = AmmoMechanics.PressureOf(e.Load, powder);
        e.ArmorPierce = Math.Max(0.0, AmmoMechanics.BaseArmorPierce(e.ReferenceEnergyJ, d.Caliber.BoreMm) + e.ArmorPierceBonus);
        e.Kind = d.Kind;
        e.Pellets = shot ? d.Pellets : 1;
        e.ArmorLevel = shell || !plain ? 0 : AmmoMechanics.NearestArmorLevel(e.ArmorPierce);

        // The damage in a weapon of the game: the energy in the barrel the preset is rated in, shared among the pellets.
        e.Shell = shell;
        e.Heavy = preset?.Heavy == true;
        e.RatedBarrelMm = preset is { BarrelMm: > 0 } ? preset.BarrelMm : AmmoMechanics.ReferenceBarrelCalibers * d.Caliber.BoreMm;
        var rated = AmmoMechanics.EnergyIn(e.Load, powder, e.RatedBarrelMm, e.Seal, out _);
        e.RatedEnergyJ = rated;
        var kindShare = syringe ? AmmoMechanics.SyringeDamageShare : 1.0;
        if (preset is { GameDamage: > 0 })
        {
            // The damage of the game is fitted to the plain load of the preset: a bullet of lead in a jacket of copper, the nominal charge, a seal of 1.
            var lead = data.Material("GunsmithLead")?.DensityGcm3 ?? 11.34;
            var copper = data.Material("GunsmithCopper")?.DensityGcm3 ?? 8.96;
            var baseline = new CaliberSpec
            {
                BoreMm = preset.BoreMm,
                CaseDiameterMm = preset.CaseDiameterMm,
                CaseLengthMm = preset.CaseLengthMm,
                SeatDepthMm = preset.SeatDepthMm,
                BulletMassG = preset.BulletMassG * (AmmoMechanics.CoreShareNoTip * lead + AmmoMechanics.JacketShare * copper) / AmmoMechanics.NominalBulletDensity,
                Powder = preset.Powder,
                ChargeG = preset.ChargeG,
            };

            e.BaselineEnergyJ = AmmoMechanics.EnergyIn(baseline, powder, e.RatedBarrelMm, 1.0, out _);
            e.GameDamage = preset.GameDamage;
            e.GameMaxDamage = preset.GameMaxDamage;
            e.NominalPellets = Math.Max(1, preset.NominalPellets);
            e.RatedDamage = kindShare * AmmoMechanics.ScaledDamage(e.GameDamage, e.GameMaxDamage, rated, e.BaselineEnergyJ, e.Pellets, e.NominalPellets, e.DamageMultiplier);
        }
        else
        {
            e.RatedDamage = kindShare * AmmoMechanics.DamageOfEnergy(rated / e.Pellets) * e.DamageMultiplier;
        }

        // What one cartridge costs.
        void Cost(GunsmithMaterialPrototype material, double volumeCm3)
        {
            e.MaterialUnits[material.ID] = e.MaterialUnits.GetValueOrDefault(material.ID) + volumeCm3 / Math.Max(1.0, material.VolumeCm3);
        }

        if (coreMat != null)
            Cost(coreMat, coreVolume);

        if (jacketMat != null)
            Cost(jacketMat, jacketVolume);

        if (tipMat != null)
            Cost(tipMat, tipVolume);

        var caseVolume = (Math.PI * d.Caliber.CaseDiameterMm * AmmoMechanics.CaseWallMm * d.Caliber.CaseLengthMm
                          + Math.PI / 4 * d.Caliber.CaseDiameterMm * d.Caliber.CaseDiameterMm * AmmoMechanics.CaseHeadMm) / 1000.0;
        Cost(casingMat, caseVolume);

        e.PowderUnits = e.Load.ChargeG * powder.CostPerGram;
        e.BuildSeconds = 1.5 + (tip != null ? 0.5 : 0.0) + (hasPayload ? 1.0 : 0.0);
        return check;
    }

    /// <summary>The cost of a batch, in whole items of each stack: the materials by the id of the gunsmith's material, the powder and the liquid.</summary>
    public static (Dictionary<string, double> Materials, double Powder, double Liquid) BatchCost(AmmoEvaluation e, int count)
    {
        var materials = new Dictionary<string, double>();
        foreach (var (id, perCartridge) in e.MaterialUnits)
            materials[id] = Math.Max(1, Math.Ceiling(perCartridge * count));

        return (materials, Math.Max(1, Math.Ceiling(e.PowderUnits * count)), Math.Round(e.PayloadUnits * count, 1));
    }
}
