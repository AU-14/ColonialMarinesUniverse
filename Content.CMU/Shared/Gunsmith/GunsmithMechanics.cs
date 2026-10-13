// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Gunsmith;

/// <summary>
/// The formulas of the gunsmith's bench: strength of materials and the interior ballistics. Units: lengths in mm, masses in g, forces in N,
/// stresses in MPa (N/mm²), pressures in MPa, energies in J, temperatures in °C, speeds in m/s unless a name says otherwise. The same formulas
/// are in the handbook of the bench: the player works them out by hand, the bench by this code.
/// </summary>
public static class GunsmithMechanics
{
    // ------------------------------------------------------------------ constants (printed in the handbook)

    /// <summary>The share of the ideal closed-vessel pressure that the powder reaches at the peak, η_p.</summary>
    public const double PeakFactor = 0.2;

    /// <summary>The gases of the powder leave at this multiple of the speed of the bullet.</summary>
    public const double GasVelocityFactor = 1.5;

    /// <summary>The length of the barrel at which the powder has given most of its work, in bores: L0 = k·d.</summary>
    public const double BoreLengthsToBurn = 8.0;

    /// <summary>The share of the energy of the powder that heats the barrel, χ.</summary>
    public const double BarrelHeatShare = 0.30;

    /// <summary>The ratio of the shear yield to the tensile yield (von Mises): τт = 0.577·σт.</summary>
    public const double ShearYieldRatio = 0.577;

    /// <summary>The time the recoil takes to pass through the stock into the shoulder, seconds.</summary>
    public const double StockPulseSeconds = 0.0035;

    /// <summary>How far from the axis of the bore the force of the butt lies, in the lengths of the stock: the arm of the bending moment.</summary>
    public const double StockEccentricity = 0.6;

    /// <summary>The ambient temperature, °C.</summary>
    public const double AmbientC = 20;

    /// <summary>A magazine's feed lip: length and width, mm.</summary>
    public const double LipLengthMm = 3;
    public const double LipWidthMm = 6;

    /// <summary>The friction of the rounds in the magazine.</summary>
    public const double FeedFriction = 0.3;

    /// <summary>The acceleration the spring must give to the last round when feeding, in g.</summary>
    public const double FeedAccelerationG = 6;

    /// <summary>The share of the ultimate strength a spring wire is allowed to be stressed to.</summary>
    public const double SpringAllowableShare = 0.5;

    /// <summary>Drawn spring wire is this many times stronger than the bar of the same metal.</summary>
    public const double SpringWireFactor = 2.5;

    public const double Gravity = 9.81;

    // ------------------------------------------------------------------ interior ballistics

    /// <summary>The volume of the powder space of the case, cm³: V = π/4·d²·(L − seat).</summary>
    public static double CaseVolumeCm3(double caseDiameterMm, double caseLengthMm, double seatDepthMm)
    {
        var length = Math.Max(0.1, caseLengthMm - seatDepthMm);
        return Math.PI / 4 * caseDiameterMm * caseDiameterMm * length / 1000.0;
    }

    /// <summary>Loading density Δ = ω/V, g/cm³.</summary>
    public static double LoadingDensity(double chargeG, double caseVolumeCm3) => chargeG / Math.Max(1e-6, caseVolumeCm3);

    /// <summary>Peak pressure of the powder gases, MPa: P = η_p·f·Δ/(1 − α·Δ).</summary>
    public static double PeakPressureMpa(double forceMpaCm3PerG, double covolumeCm3PerG, double loadingDensity)
    {
        var free = 1.0 - covolumeCm3PerG * loadingDensity;

        // Past a certain density the powder simply does not fit in the case, and the pressure goes where it will.
        if (free < 0.05)
            free = 0.05;

        return PeakFactor * forceMpaCm3PerG * loadingDensity / free;
    }

    /// <summary>The share of the energy of the powder given to the bullet by a barrel of this length: ε = η·(1 − exp(−L/L0)), L0 = 8·d.</summary>
    public static double BarrelEfficiency(double powderEfficiency, double barrelLengthMm, double boreMm)
    {
        var l0 = BoreLengthsToBurn * boreMm;
        return powderEfficiency * (1.0 - Math.Exp(-barrelLengthMm / Math.Max(1e-6, l0)));
    }

    /// <summary>Muzzle velocity, m/s: v = √(2000·ε·ω·Q/(m + ω/3)) with ω and m in grams, Q in J/g.</summary>
    public static double MuzzleVelocity(double efficiency, double chargeG, double energyJPerG, double bulletMassG)
    {
        var work = efficiency * chargeG * energyJPerG;
        return Math.Sqrt(2000.0 * work / (bulletMassG + chargeG / 3.0));
    }

    /// <summary>Muzzle energy, J: E = m·v²/2 (m in g).</summary>
    public static double MuzzleEnergyJ(double bulletMassG, double velocity) => 0.5 * bulletMassG / 1000.0 * velocity * velocity;

    /// <summary>Impulse of the recoil, N·s: J = (m + 1.5·ω)·v.</summary>
    public static double RecoilImpulse(double bulletMassG, double chargeG, double velocity) =>
        (bulletMassG + GasVelocityFactor * chargeG) / 1000.0 * velocity;

    /// <summary>Time the bullet spends in the bore, s: t = 2·L/v (L in m).</summary>
    public static double TimeInBore(double barrelLengthMm, double velocity) => 2.0 * barrelLengthMm / 1000.0 / Math.Max(1e-6, velocity);

    /// <summary>Peak of the half-sine pulse of the force of the recoil, N: F = π·J/(2·t).</summary>
    public static double PeakForce(double impulse, double pulseSeconds) => Math.PI * impulse / (2.0 * Math.Max(1e-9, pulseSeconds));

    /// <summary>The speed the free weapon gets from the recoil, m/s: u = J/M (M in kg).</summary>
    public static double RecoilVelocity(double impulse, double weaponMassG) => impulse / Math.Max(1e-6, weaponMassG / 1000.0);

    // ------------------------------------------------------------------ the thick-walled barrel (Lamé), closed at the end

    /// <summary>Hoop stress at the inner wall, MPa: σθ = P·(D² + d²)/(D² − d²).</summary>
    public static double BarrelHoopMpa(double pressureMpa, double outerMm, double innerMm)
    {
        var d2 = innerMm * innerMm;
        var d_2 = outerMm * outerMm;
        return pressureMpa * (d_2 + d2) / Math.Max(1e-6, d_2 - d2);
    }

    /// <summary>Axial stress, MPa: σz = P·d²/(D² − d²).</summary>
    public static double BarrelAxialMpa(double pressureMpa, double outerMm, double innerMm)
    {
        var d2 = innerMm * innerMm;
        var d_2 = outerMm * outerMm;
        return pressureMpa * d2 / Math.Max(1e-6, d_2 - d2);
    }

    /// <summary>
    /// Equivalent (von Mises) stress at the inner wall, MPa, from the three principal stresses σθ, σr = −P, σz:
    /// σэ = √(½·[(σθ − σr)² + (σr − σz)² + (σz − σθ)²]).
    /// </summary>
    public static double VonMises(double hoop, double radial, double axial)
    {
        var a = hoop - radial;
        var b = radial - axial;
        var c = axial - hoop;
        return Math.Sqrt(0.5 * (a * a + b * b + c * c));
    }

    /// <summary>Tresca (the largest shear) equivalent, MPa: σэ = σθ − σr, the more careful rule used for estimating by hand.</summary>
    public static double Tresca(double hoop, double radial) => hoop - radial;

    // ------------------------------------------------------------------ the bolt and the lugs

    /// <summary>The force the case head puts on the bolt, N: F = P·π/4·d².</summary>
    public static double BoltThrustN(double pressureMpa, double caseDiameterMm) => pressureMpa * Math.PI / 4.0 * caseDiameterMm * caseDiameterMm;

    /// <summary>Shear stress in the lugs, MPa: τ = F/(n·w·l).</summary>
    public static double LugShearMpa(double thrustN, int lugs, double widthMm, double lengthMm) =>
        thrustN / Math.Max(1e-6, lugs * widthMm * lengthMm);

    /// <summary>Bearing (crushing) stress under the lugs, MPa: σсм = F/(n·w·t).</summary>
    public static double LugBearingMpa(double thrustN, int lugs, double widthMm, double depthMm) =>
        thrustN / Math.Max(1e-6, lugs * widthMm * depthMm);

    /// <summary>Tension in the body of the bolt at the firing pin hole, MPa: σ = F/(π/4·(d² − h²)).</summary>
    public static double BoltTensionMpa(double thrustN, double boltDiameterMm, double holeMm) =>
        thrustN / Math.Max(1e-6, Math.PI / 4.0 * (boltDiameterMm * boltDiameterMm - holeMm * holeMm));

    // ------------------------------------------------------------------ cantilevers and pins

    /// <summary>Bending stress of a rectangular cantilever, MPa: σ = 6·F·L/(b·h²).</summary>
    public static double CantileverStressMpa(double forceN, double lengthMm, double widthMm, double heightMm) =>
        6.0 * forceN * lengthMm / Math.Max(1e-6, widthMm * heightMm * heightMm);

    /// <summary>Stress in a stock from the recoil, MPa: the force of the butt F = π·J/(2·t) acts with the arm k·L.</summary>
    public static double StockStressMpa(double impulse, double stockLengthMm, double stockWidthMm, double stockHeightMm) =>
        CantileverStressMpa(PeakForce(impulse, StockPulseSeconds) * StockEccentricity, stockLengthMm, stockWidthMm, stockHeightMm);

    /// <summary>The force the folding hinge carries, N.</summary>
    public static double StockForceN(double impulse) => PeakForce(impulse, StockPulseSeconds);

    /// <summary>Shear stress in a pin in double shear, MPa: τ = F/(2·π/4·d²).</summary>
    public static double PinDoubleShearMpa(double forceN, double pinDiameterMm) =>
        forceN / Math.Max(1e-6, 2.0 * Math.PI / 4.0 * pinDiameterMm * pinDiameterMm);

    // ------------------------------------------------------------------ fatigue (Goodman)

    /// <summary>
    /// The factor of safety against fatigue for a stress that goes 0 → σmax at every shot (σa = σm = σmax/2), by Goodman:
    /// 1/n = σa/σ-1 + σm/σв.
    /// </summary>
    public static double GoodmanSafety(double maxStressMpa, double enduranceMpa, double ultimateMpa)
    {
        var a = maxStressMpa / 2.0;
        var inverse = a / Math.Max(1e-6, enduranceMpa) + a / Math.Max(1e-6, ultimateMpa);
        return 1.0 / Math.Max(1e-9, inverse);
    }

    /// <summary>
    /// The number of shots before a part fails from fatigue (in the units of the game: a thousandth of the real cycles). If the Goodman factor
    /// is 1 or more the part lasts for ever.
    /// </summary>
    public static double FatigueShots(double goodmanSafety)
    {
        if (goodmanSafety >= 1.0)
            return double.PositiveInfinity;

        // The ratio of the equivalent stress amplitude to the endurance limit is 1/n; Basquin's law N = 10^6·r^-8.
        var r = 1.0 / goodmanSafety;
        return 1e6 * Math.Pow(r, -8.0) / 1000.0;
    }

    // ------------------------------------------------------------------ heat

    /// <summary>The heating of the barrel by one shot, K: ΔT = χ·ω·Q/(M·c).</summary>
    public static double HeatPerShotK(double chargeG, double energyJPerG, double barrelMassG, double specificHeat) =>
        BarrelHeatShare * chargeG * energyJPerG / Math.Max(1e-6, barrelMassG * specificHeat);

    /// <summary>
    /// The temperature a barrel comes to when it is fired at the same rate for good, °C: it cools between the shots by q = 0.5^(1/(r·T½)),
    /// and the temperature just after a shot is T = 20 + ΔT/(1 − q).
    /// </summary>
    public static double SustainedTempC(double heatPerShotK, double shotsPerSecond, double halfLifeSeconds)
    {
        var q = Math.Pow(0.5, 1.0 / Math.Max(1e-6, shotsPerSecond * halfLifeSeconds));
        return AmbientC + heatPerShotK / Math.Max(1e-9, 1.0 - q);
    }

    /// <summary>The yield strength of the hot metal, MPa: σт(T) = σт·(1 − (T − 20)/(Tпр − 20)).</summary>
    public static double HotYieldMpa(double yieldMpa, double tempC, double limitTempC)
    {
        var share = 1.0 - (tempC - AmbientC) / Math.Max(1.0, limitTempC - AmbientC);
        return yieldMpa * Math.Clamp(share, 0.0, 1.0);
    }

    // ------------------------------------------------------------------ the spring of the magazine

    /// <summary>Spring index C = D/d.</summary>
    public static double SpringIndex(double coilMm, double wireMm) => coilMm / Math.Max(1e-6, wireMm);

    /// <summary>The factor of Wahl: K = (4C − 1)/(4C − 4) + 0.615/C.</summary>
    public static double WahlFactor(double index) => (4 * index - 1) / Math.Max(1e-6, 4 * index - 4) + 0.615 / Math.Max(1e-6, index);

    /// <summary>Rate of a coil spring, N/mm: k = G·d⁴/(8·D³·n), G in MPa.</summary>
    public static double SpringRate(double shearModulusMpa, double wireMm, double coilMm, double turns) =>
        shearModulusMpa * Math.Pow(wireMm, 4) / Math.Max(1e-6, 8.0 * Math.Pow(coilMm, 3) * turns);

    /// <summary>Shear stress in the wire, MPa: τ = 8·F·D·K/(π·d³).</summary>
    public static double SpringShearMpa(double forceN, double coilMm, double wireMm, double wahl) =>
        8.0 * forceN * coilMm * wahl / Math.Max(1e-6, Math.PI * Math.Pow(wireMm, 3));

    /// <summary>The length of the spring when it is solid: Ls = (n + 2)·d.</summary>
    public static double SolidLengthMm(double turns, double wireMm) => (turns + 2.0) * wireMm;

    /// <summary>The force the spring has to give to push up the round when n are left, N: F = m·(a + μ·(n)·g).</summary>
    public static double FeedForceNeededN(double roundMassG, int roundsLeft) =>
        roundMassG / 1000.0 * (FeedAccelerationG * Gravity + FeedFriction * roundsLeft * Gravity);

    // ------------------------------------------------------------------ the volumes

    public static double TubeVolumeCm3(double outerMm, double innerMm, double lengthMm) =>
        Math.PI / 4.0 * (outerMm * outerMm - innerMm * innerMm) * lengthMm / 1000.0;

    public static double BarVolumeCm3(double widthMm, double heightMm, double lengthMm) => widthMm * heightMm * lengthMm / 1000.0;

    /// <summary>The mass of the volume of the material, g: m = ρ·V.</summary>
    public static double MassG(double densityGcm3, double volumeCm3) => densityGcm3 * volumeCm3;
}
