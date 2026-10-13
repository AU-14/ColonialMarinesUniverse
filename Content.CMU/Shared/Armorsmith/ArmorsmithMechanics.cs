// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Armorsmith;

/// <summary>
/// The formulas of the armorer's bench. Units: lengths in mm, areas in m², masses in kg, stresses in MPa, speeds in m/s unless a name says otherwise.
/// The same formulas are in the handbook of the bench: the player works them out by hand, the bench by this code.
/// </summary>
public static class ArmorsmithMechanics
{
    // ------------------------------------------------------------------ constants (printed in the handbook)

    /// <summary>The area of the plate of a piece worn on the body (a ghillie suit too) and of one worn on the head, m².</summary>
    public const double BodyAreaM2 = 0.30;
    public const double HeadAreaM2 = 0.08;

    /// <summary>The mass of the straps, the lining and the frame that every piece has, kg.</summary>
    public const double BodyFrameKg = 1.2;
    public const double HeadFrameKg = 0.35;
    public const double GhillieFrameKg = 0.8;

    /// <summary>A closed helmet covers the face too: its plate is larger by this factor, and the visor and the seals add some weight, kg.</summary>
    public const double ClosedAreaFactor = 1.4;
    public const double ClosedExtraKg = 0.25;

    /// <summary>What a closed helmet gives: the bullet speed, the melee and the explosion protection grow by this factor, and the bio protection by the addition (to its exponent).</summary>
    public const double ClosedBonus = 1.10;
    public const double ClosedBioBonus = 0.3;

    /// <summary>The bullet the protection is rated against: a 7.62 mm bullet of 8 g. Its area is that of a circle of the diameter, m².</summary>
    public const double ReferenceBulletMassKg = 0.008;
    public const double ReferenceBulletAreaM2 = 45.6e-6;

    /// <summary>A shape factor that turns the figure of merit of a material into the speed of the bullet the layer stops (it comes from tests of real armor).</summary>
    public const double ShapeFactor = 3.5;

    /// <summary>The speed at which an armor keeps out 63% of what a protection of the game can keep out (the scale of the bullet protection), m/s.</summary>
    public const double BulletScaleVelocity = 550.0;

    /// <summary>The greatest protection each kind can come to in the game (a little over the best armor of the game), and the scales the formulas use.</summary>
    public const int BulletCap = 42;
    public const int MeleeCap = 40;
    public const int ExplosionCap = 40;
    public const int BioCap = 25;

    /// <summary>The bending resistance σт·t² that gives 63% of the melee protection, MPa·mm².</summary>
    public const double MeleeScale = 6000.0;

    /// <summary>The mass per square metre that gives 63% of the explosion protection, kg/m².</summary>
    public const double ExplosionScale = 30.0;

    /// <summary>What a layer of backing adds to the exponent of the melee protection and what the plate coverage adds to the bio one.</summary>
    public const double MeleePerBackingKg = 1.0 / 12.0;
    public const double BioPerCoverage = 0.5;
    public const double BioPerLayer = 0.1;

    /// <summary>The least and the greatest thickness of a plate, mm (the ghillie suit is light, so its plate is thin).</summary>
    public const double MinThicknessMm = 0.5;
    public const double MaxBodyThicknessMm = 44.0;
    public const double MaxHeadThicknessMm = 38.0;
    public const double MaxGhillieThicknessMm = 4.0;

    public const double MinCoveragePercent = 20;
    public const double MaxCoveragePercent = 100;
    public const int MaxBackingLayers = 40;

    /// <summary>The heaviest a piece may be, kg. A ghillie suit never comes to the weight of a heavy armor.</summary>
    public const double MaxBodyMassKg = 70.0;
    public const double MaxHeadMassKg = 25.0;
    public const double MaxGhillieMassKg = 8.0;

    // ------------------------------------------------------------------ the slowdown

    /// <summary>
    /// How much of the speed a plate and a backing take: the thicker the plate (over the area it covers) and the more layers of cloth, the slower the wearer.
    /// A millimetre of plate takes this share of the speed, and a layer of the backing takes the other.
    /// </summary>
    public const double SlowdownPerMm = 0.012;
    public const double SlowdownPerLayer = 0.003;

    /// <summary>The slowest an armor can make its wearer, as a share of the speed.</summary>
    public const double MinSpeedShare = 0.4;

    /// <summary>The speed shares above which an armor is of the light and of the medium tier (below the second it is of the heavy one).</summary>
    public const double LightTierShare = 0.88;
    public const double MediumTierShare = 0.70;

    // ------------------------------------------------------------------ the ghillie suit

    /// <summary>
    /// The plant fiber a ghillie suit is woven of, per square metre of it. The stack is a placeholder (cotton) until the game has a plant fiber of its own.
    /// </summary>
    public const double GhillieFiberPerM2 = 80.0;
    public const string GhillieFiberStack = "Cotton";

    // ------------------------------------------------------------------ the pieces

    public static double AreaM2(ArmorSlotKind slot, bool closed = false) => slot switch
    {
        ArmorSlotKind.Head => HeadAreaM2 * (closed ? ClosedAreaFactor : 1.0),
        _ => BodyAreaM2,
    };

    public static double FrameKg(ArmorSlotKind slot, bool closed = false) => slot switch
    {
        ArmorSlotKind.Head => HeadFrameKg + (closed ? ClosedExtraKg : 0.0),
        ArmorSlotKind.Ghillie => GhillieFrameKg,
        _ => BodyFrameKg,
    };

    public static double MaxThicknessMm(ArmorSlotKind slot) => slot switch
    {
        ArmorSlotKind.Head => MaxHeadThicknessMm,
        ArmorSlotKind.Ghillie => MaxGhillieThicknessMm,
        _ => MaxBodyThicknessMm,
    };

    public static double MaxMassKg(ArmorSlotKind slot) => slot switch
    {
        ArmorSlotKind.Head => MaxHeadMassKg,
        ArmorSlotKind.Ghillie => MaxGhillieMassKg,
        _ => MaxBodyMassKg,
    };

    // ------------------------------------------------------------------ the layers

    /// <summary>The mass of a plate of the thickness per square metre, kg/m²: A = ρ · t (ρ in g/cm³, t in mm).</summary>
    public static double ArealKgM2(double densityGcm3, double thicknessMm) => densityGcm3 * thicknessMm;

    /// <summary>
    /// The figure of merit of a material against a bullet (Cunniff): U* = σв·ε/(2ρ) · √(E/ρ), in SI. σв is the strength, ε the strain at the break,
    /// ρ the density, E the Young's modulus. The tougher, the stronger and the lighter, the better.
    /// </summary>
    public static double MeritU(double ultimateMpa, double failureStrain, double densityGcm3, double youngGpa)
    {
        var sigma = ultimateMpa * 1e6;
        var rho = densityGcm3 * 1000.0;
        var young = youngGpa * 1e9;
        return sigma * failureStrain / (2.0 * rho) * Math.Sqrt(young / rho);
    }

    /// <summary>The speed that goes with the merit: V* = U*^(1/3), m/s.</summary>
    public static double MeritVelocity(double meritU) => Math.Cbrt(Math.Max(0.0, meritU));

    /// <summary>
    /// The speed of the reference bullet that a layer of the mass per square metre just stops, m/s: V = k·V*·√(A·S/m), where S is the area and m the mass of the
    /// bullet and k the shape factor.
    /// </summary>
    public static double LayerLimitVelocity(double meritVelocity, double arealKgM2) =>
        ShapeFactor * meritVelocity * Math.Sqrt(Math.Max(0.0, arealKgM2) * ReferenceBulletAreaM2 / ReferenceBulletMassKg);

    /// <summary>
    /// The speed the whole armor stops: the energy of the layers adds, so the squares of the speeds add. The plate covers only a share of the piece, the backing all of it.
    /// A closed helmet protects more (the bonus).
    /// </summary>
    public static double TotalLimitVelocity(double plateVelocity, double coverage, double backingVelocity, double workmanship, double bonus = 1.0) =>
        bonus * workmanship * Math.Sqrt(coverage * plateVelocity * plateVelocity + backingVelocity * backingVelocity);

    // ------------------------------------------------------------------ the protection of the game

    private static int Saturate(int cap, double exponent) => (int) Math.Round(cap * (1.0 - Math.Exp(-Math.Max(0.0, exponent))));

    /// <summary>The bullet protection: cap·(1 − exp(−(V/V0)²)).</summary>
    public static int BulletArmor(double limitVelocity) => Saturate(BulletCap, Math.Pow(limitVelocity / BulletScaleVelocity, 2.0));

    /// <summary>The melee protection: cap·(1 − exp(−b·(c·σт·t²/S0 + 1/12·A_backing))), σт the yield of the plate, c the coverage, b the bonus of a closed helmet.</summary>
    public static int MeleeArmor(double yieldMpa, double thicknessMm, double coverage, double backingKgM2, double bonus = 1.0) =>
        Saturate(MeleeCap, bonus * (coverage * yieldMpa * thicknessMm * thicknessMm / MeleeScale + MeleePerBackingKg * backingKgM2));

    /// <summary>The explosion protection: cap·(1 − exp(−b·A/30)), A the mass per square metre of everything that protects.</summary>
    public static int ExplosionArmor(double plateKgM2, double coverage, double backingKgM2, double bonus = 1.0) =>
        Saturate(ExplosionCap, bonus * (coverage * plateKgM2 + backingKgM2) / ExplosionScale);

    /// <summary>The protection from bio and chemical harm: cap·(1 − exp(−(0.5·c + 0.1·n + a))), n the layers of the backing, a the addition of a closed helmet.</summary>
    public static int BioArmor(double coverage, int layers, double addition = 0.0) =>
        Saturate(BioCap, BioPerCoverage * coverage + BioPerLayer * layers + addition);

    // ------------------------------------------------------------------ the weight and the slowdown

    /// <summary>The mass of the piece, kg: the frame plus the plate and the backing over the whole area.</summary>
    public static double MassKg(ArmorSlotKind slot, double plateKgM2, double coverage, double backingKgM2, bool closed = false) =>
        FrameKg(slot, closed) + (coverage * plateKgM2 + backingKgM2) * AreaM2(slot, closed);

    /// <summary>The share of the speed an armor takes: s = 0.012·c·t + 0.003·n (t the thickness of the plate in mm, c the coverage, n the layers of the backing). A helmet takes none.</summary>
    public static double Slowdown(double thicknessMm, double coverage, int layers) =>
        SlowdownPerMm * coverage * thicknessMm + SlowdownPerLayer * layers;

    /// <summary>The share of the speed the wearer keeps: 1 − s, but not less than the least.</summary>
    public static double SpeedShare(double slowdown) => Math.Clamp(1.0 - slowdown, MinSpeedShare, 1.0);

    /// <summary>The tier of speed of a body armor by the share of the speed it leaves: light, medium or heavy.</summary>
    public static string SpeedTier(double speedShare) => speedShare >= LightTierShare ? "light" : speedShare >= MediumTierShare ? "medium" : "heavy";
}
