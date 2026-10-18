// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

/// <summary>How the weapon is fed: from a magazine that is put in, from a revolver's cylinder, or from inside (the cartridges are put in one by one, like a shotgun's).</summary>
[Serializable, NetSerializable]
public enum FeedKind : byte
{
    Magazine,
    Revolver,
    Internal,
}

// The kinds of stock a gunsmith bench holds.
public enum StockKind : byte
{
    None,
    Fixed,
    Folding,
}

/// <summary>How exactly the parts are made: the finer, the dearer and the slower, and the harder the metal must be to work.</summary>
public enum ToleranceGrade : byte
{
    Rough,
    Standard,
    Fine,
    Match,
}

/// <summary>The cartridge: what the player sets for a caliber. The pressure and the speed are not here: the player has to work them out.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CaliberSpec
{
    /// <summary>Diameter of the bore (and of the bullet), mm.</summary>
    [DataField]
    public double BoreMm = 9;

    /// <summary>Inner diameter of the case, mm.</summary>
    [DataField]
    public double CaseDiameterMm = 9.9;

    /// <summary>Length of the case, mm.</summary>
    [DataField]
    public double CaseLengthMm = 19;

    /// <summary>How deep the bullet sits in the case, mm.</summary>
    [DataField]
    public double SeatDepthMm = 8.4;

    /// <summary>Mass of the bullet, g.</summary>
    [DataField]
    public double BulletMassG = 8;

    [DataField]
    public string Powder = "GunsmithPowderSmokeless";

    /// <summary>Mass of the powder charge, g.</summary>
    [DataField]
    public double ChargeG = 0.53;

    /// <summary>
    /// A signature of the caliber: the shape of the case and the bore. A magazine and a cartridge fit a weapon if the signatures are the same;
    /// what is in the cartridge (the bullet, the powder, the charge) is another matter, and a hot load can burst a weak weapon.
    /// </summary>
    public string Key() =>
        Part(BoreMm) + "x" + Part(CaseDiameterMm) + "x" + Part(CaseLengthMm);

    // The sandbox of the client does not allow string.Create with a format provider, which is what an interpolated string with a culture compiles to.
    private static string Part(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public CaliberSpec Clone() => new()
    {
        BoreMm = BoreMm,
        CaseDiameterMm = CaseDiameterMm,
        CaseLengthMm = CaseLengthMm,
        SeatDepthMm = SeatDepthMm,
        BulletMassG = BulletMassG,
        Powder = Powder,
        ChargeG = ChargeG,
    };
}

/// <summary>Everything the player decides about a firearm.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class FirearmDesign
{
    [DataField]
    public string Action = "GunsmithActionBolt";

    [DataField]
    public CaliberSpec Caliber = new();

    // ------------------------------------------------------------ barrel (a thick-walled tube)

    [DataField]
    public string BarrelMaterial = "GunsmithSteel";

    [DataField]
    public double BarrelLengthMm = 400;

    /// <summary>Outer diameter of the barrel at the chamber, mm.</summary>
    [DataField]
    public double BarrelOuterMm = 34;

    // ------------------------------------------------------------ bolt and the locking lugs

    [DataField]
    public string BoltMaterial = "GunsmithSteel";

    /// <summary>Diameter of the body of the bolt, mm.</summary>
    [DataField]
    public double BoltDiameterMm = 16;

    /// <summary>Diameter of the hole for the firing pin, mm.</summary>
    [DataField]
    public double PinHoleMm = 2.5;

    [DataField]
    public int Lugs = 2;

    /// <summary>Width of one lug, mm.</summary>
    [DataField]
    public double LugWidthMm = 6;

    /// <summary>Length of one lug along the axis, mm.</summary>
    [DataField]
    public double LugLengthMm = 8;

    /// <summary>Depth of the engagement of one lug (radial), mm.</summary>
    [DataField]
    public double LugDepthMm = 4;

    // ------------------------------------------------------------ receiver (the tube around the bolt)

    [DataField]
    public string ReceiverMaterial = "GunsmithSteel";

    // ------------------------------------------------------------ stock (a cantilever)

    [DataField]
    public StockKind Stock = StockKind.Fixed;

    [DataField]
    public string StockMaterial = "GunsmithWood";

    [DataField]
    public double StockWidthMm = 30;

    [DataField]
    public double StockHeightMm = 40;

    [DataField]
    public double StockLengthMm = 280;

    /// <summary>Diameter of the pin of the hinge of a folding stock, mm.</summary>
    [DataField]
    public double HingePinMm = 5;

    // ------------------------------------------------------------ options

    [DataField]
    public ToleranceGrade Grade = ToleranceGrade.Standard;

    /// <summary>
    /// The rate of fire the maker asks for, shots a second; 0 for the one the mechanism gives. A rate the barrel cannot stand (it takes more heat
    /// than it gives off) makes it burst once it has got hot enough.
    /// </summary>
    [DataField]
    public double FireRate;

    /// <summary>The class of the weapon: what caliber and barrel it can have.</summary>
    [DataField]
    public string Class = "GunsmithClassRifle";

    [DataField]
    public FeedKind Feed = FeedKind.Magazine;

    /// <summary>How many cartridges a cylinder or an inner magazine holds (a weapon that takes a magazine has no capacity of its own).</summary>
    [DataField]
    public int FeedCapacity = 6;

    /// <summary>What the weapon looks like (the id of a look); empty for the weapon made of parts.</summary>
    [DataField]
    public string Look = string.Empty;

    /// <summary>The name the maker gives the weapon; empty for the usual one.</summary>
    [DataField]
    public string WeaponName = string.Empty;

    /// <summary>The description the maker writes for the weapon; empty for the usual one.</summary>
    [DataField]
    public string WeaponDescription = string.Empty;

    public const int MaxNameLength = 40;
    public const int MaxDescriptionLength = 300;

    /// <summary>The text of a name or a description cleaned of what would break the markup of the examine text, and cut to length.</summary>
    public static string CleanText(string text, int max)
    {
        var builder = new System.Text.StringBuilder(Math.Min(text.Length, max));
        foreach (var c in text)
        {
            if (builder.Length >= max)
                break;

            if (c is '[' or ']' or '{' or '}' or '\\' || char.IsControl(c))
                continue;

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    public FirearmDesign Clone() => new()
    {
        Action = Action,
        Caliber = Caliber.Clone(),
        BarrelMaterial = BarrelMaterial,
        BarrelLengthMm = BarrelLengthMm,
        BarrelOuterMm = BarrelOuterMm,
        BoltMaterial = BoltMaterial,
        BoltDiameterMm = BoltDiameterMm,
        PinHoleMm = PinHoleMm,
        Lugs = Lugs,
        LugWidthMm = LugWidthMm,
        LugLengthMm = LugLengthMm,
        LugDepthMm = LugDepthMm,
        ReceiverMaterial = ReceiverMaterial,
        Stock = Stock,
        StockMaterial = StockMaterial,
        StockWidthMm = StockWidthMm,
        StockHeightMm = StockHeightMm,
        StockLengthMm = StockLengthMm,
        HingePinMm = HingePinMm,
        Grade = Grade,
        FireRate = FireRate,
        Class = Class,
        Feed = Feed,
        FeedCapacity = FeedCapacity,
        Look = Look,
        WeaponName = WeaponName,
        WeaponDescription = WeaponDescription,
    };
}

/// <summary>A magazine for a caliber. The spring is designed by the player too.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class MagazineDesign
{
    [DataField]
    public CaliberSpec Caliber = new();

    [DataField]
    public int Capacity = 15;

    [DataField]
    public string BodyMaterial = "GunsmithSteel";

    /// <summary>Thickness of the wall (and of the feed lips), mm.</summary>
    [DataField]
    public double WallMm = 0.8;

    [DataField]
    public string SpringMaterial = "GunsmithSteel";

    /// <summary>Diameter of the wire of the spring, mm.</summary>
    [DataField]
    public double WireMm = 1.0;

    /// <summary>Mean diameter of the coil, mm.</summary>
    [DataField]
    public double CoilMm = 12;

    /// <summary>Number of the active turns.</summary>
    [DataField]
    public double Turns = 40;

    /// <summary>Free length of the spring, mm.</summary>
    [DataField]
    public double FreeLengthMm = 209;

    [DataField]
    public ToleranceGrade Grade = ToleranceGrade.Standard;

    public MagazineDesign Clone() => new()
    {
        Caliber = Caliber.Clone(),
        Capacity = Capacity,
        BodyMaterial = BodyMaterial,
        WallMm = WallMm,
        SpringMaterial = SpringMaterial,
        WireMm = WireMm,
        CoilMm = CoilMm,
        Turns = Turns,
        FreeLengthMm = FreeLengthMm,
        Grade = Grade,
    };
}

/// <summary>
/// What the bench worked out for a firearm. These are the real values: the bench uses them to decide whether the weapon holds. The window
/// never shows them: the player is to work them out with the formulas.
/// </summary>
public sealed class FirearmEvaluation
{
    // interior ballistics
    public double CaseVolumeCm3;
    public double LoadingDensity;
    public double PressureMpa;
    public double MuzzleVelocity;
    public double MuzzleEnergyJ;
    public double RecoilImpulse;
    public double TimeInBoreS;
    public double RecoilForceN;
    public double RecoilVelocity;

    // strength
    public double BarrelHoopMpa;
    public double BarrelAxialMpa;
    public double BarrelMisesMpa;
    public double BarrelSafety;
    public double BarrelFatigueSafety;
    public double BoltThrustN;
    public double LugShearSafety;
    public double LugBearingSafety;
    public double BoltTensionSafety;
    public double StockSafety;
    public double HingeSafety;
    public double HeatPerShotK;

    // masses
    public double BarrelMassG;
    public double BoltMassG;
    public double StockMassG;
    public double ReceiverMassG;
    public double TotalMassG;

    // what the weapon is like
    public double Damage;
    public double FireRate;

    /// <summary>The rate of fire the mechanism gives by itself, shots a second.</summary>
    public double NaturalFireRate;

    /// <summary>The temperature the barrel comes to at last if it is fired at that rate without a stop, °C.</summary>
    public double SustainedTempC;

    /// <summary>The yield of the barrel at that temperature over the stress in it: below 1 the barrel bursts from the heat once it has got hot.</summary>
    public double RateSafety;
    public double ScatterDegrees;
    public double RecoilFactor;
    public double Quality;
    public double Reliability;
    public int Modes;

    // building
    public double Complexity;
    public double BuildSeconds;
    public Dictionary<string, double> MaterialUnits = new();
    public double PowderUnitsPerCartridge;

    // the verdict
    /// <summary>The lowest of the static safety factors of the load-bearing parts.</summary>
    public double WeakestSafety;

    /// <summary>The number of shots after which the weapon fails from fatigue (very large: never).</summary>
    public double FatigueLifeShots;

    /// <summary>Whether the weapon bursts at the first shot.</summary>
    public bool BurstsAtOnce => WeakestSafety < 1.0;

    public List<string> Problems = new();
}

/// <summary>What the bench worked out for a magazine.</summary>
public sealed class MagazineEvaluation
{
    public double PitchMm;
    public double BodyLengthMm;
    public double SpringRateNPerMm;
    public double SpringIndex;
    public double WahlFactor;
    public double SolidLengthMm;
    public double FullForceN;
    public double SpringShearMpa;
    public double SpringSafety;
    public double LipSafety;
    public double ReliableShare;
    public double MassG;
    public double Quality;
    public double Complexity;
    public double BuildSeconds;
    public bool SpringSolid;
    public Dictionary<string, double> MaterialUnits = new();
    public List<string> Problems = new();
}
