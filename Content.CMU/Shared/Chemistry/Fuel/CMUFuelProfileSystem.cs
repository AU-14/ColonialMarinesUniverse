using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Fuel;

/// <summary>
/// Fire behaviour of a whole solution, so that blended napalms and generated fuels can be loaded into
/// flamers instead of only the first reagent in the tank being consulted.
/// </summary>
public readonly record struct CMUFuelProfile(
    int Intensity,
    int Duration,
    int Radius,
    EntProtoId FireEntity,
    bool FireSpread,
    ProtoId<ReagentPrototype> PrimaryReagent,
    bool FirePenetrating = false)
{
    public bool IsValid => Intensity > 0 && Duration > 0 && Radius > 0;
}

public sealed partial class CMUFuelProfileSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RMCReagentSystem _reagent = default!;

    /// <summary>
    /// Computes the fuel profile of <paramref name="solution"/>. Base stats are the volume-weighted mean of
    /// every fuel reagent (one with positive intensity, duration and radius), diluted by the fraction of the
    /// solution that is fuel. Every reagent's <c>IntensityMod/DurationMod/RadiusMod</c> then scale the result as
    /// a per-unit fractional multiplier, which lets non-fuel additives such as a Flowing or Viscous research
    /// chemical shape a napalm blend. Fire entity and spread come from the largest fuel component. The blend is
    /// fire-penetrating when at least <see cref="PenetratingFraction"/> of it is a <c>firePenetrating</c> reagent.
    /// </summary>
    public bool TryCompute(Solution solution, out CMUFuelProfile profile)
    {
        profile = default;
        if (solution.Volume <= FixedPoint2.Zero)
            return false;

        FixedPoint2 fuelVolume = FixedPoint2.Zero;
        FixedPoint2 penetratingVolume = FixedPoint2.Zero;
        FixedPoint2 intensity = FixedPoint2.Zero, duration = FixedPoint2.Zero, radius = FixedPoint2.Zero;
        FixedPoint2 intensityMod = FixedPoint2.Zero, durationMod = FixedPoint2.Zero, radiusMod = FixedPoint2.Zero;
        ReagentPrototype? primary = null;
        FixedPoint2 primaryVolume = FixedPoint2.Zero;

        foreach (var quantity in solution.Contents)
        {
            if (!_reagent.TryIndex(quantity.Reagent.Prototype, out var reagent))
                continue;

            var volume = quantity.Quantity;
            if (reagent.FirePenetrating)
                penetratingVolume += volume;
            var isFuel = reagent.Intensity > 0 && reagent.Duration > 0 && reagent.Radius > 0;

            // Hand-authored fuels' *Mod values are molotov tuning (SharedRMCFlammableSystem) and already
            // baked into their own flamer stats, so they must not rescale a tank of that fuel. Modifiers
            // apply from additives (non-fuels such as phoron or a Flowing chemical) and from generated
            // chemicals, whose modifiers come from their properties.
            if (!isFuel || reagent.Generated)
            {
                intensityMod += reagent.IntensityMod * volume;
                durationMod += reagent.DurationMod * volume;
                radiusMod += reagent.RadiusMod * volume;
            }

            if (!isFuel)
                continue;

            fuelVolume += volume;
            intensity += volume * reagent.Intensity;
            duration += volume * reagent.Duration;
            radius += volume * reagent.Radius;
            if (volume > primaryVolume)
            {
                primaryVolume = volume;
                primary = reagent;
            }
        }

        if (primary is null || fuelVolume <= FixedPoint2.Zero)
            return false;

        // Modifiers are per unit of the whole solution and act as a fractional multiplier on the fuel mean,
        // so a 10% additive has the same effect in a 300u tank as in a 50u bottle. Clamped so that the
        // molotov-scaled napalm modifiers cannot zero or explode a blend.
        var fuelFraction = fuelVolume / solution.Volume;
        intensity = intensity / fuelVolume * fuelFraction * Multiplier(intensityMod, solution.Volume);
        duration = duration / fuelVolume * fuelFraction * Multiplier(durationMod, solution.Volume);
        radius = radius / fuelVolume * Multiplier(radiusMod, solution.Volume);

        profile = new CMUFuelProfile(
            (int) Math.Round(intensity.Float()),
            (int) Math.Round(duration.Float()),
            (int) Math.Round(radius.Float()),
            primary.FireEntity,
            primary.FireSpread,
            primary.ID,
            penetratingVolume / solution.Volume >= PenetratingFraction);
        return profile.IsValid;
    }

    public const float MinMultiplier = 0.25f;
    public const float MaxMultiplier = 2f;

    /// <summary>Share of the solution that must be a fire-penetrating reagent before the blend penetrates.</summary>
    public static readonly FixedPoint2 PenetratingFraction = FixedPoint2.New(0.1);

    /// <summary>
    /// Napalm UT's duration stat; the RMC tile fire prototypes' lifetimes are balanced around it, so a blend
    /// with this duration keeps the prototype lifetime and other durations scale it proportionally.
    /// </summary>
    public const int ReferenceDuration = 20;

    /// <summary>Lifetime multiplier for a tile fire lit by a blend of the given (tank-capped) duration.</summary>
    public static float TileLifetimeMultiplier(int duration)
        => Math.Clamp(duration / (float) ReferenceDuration, MinMultiplier, MaxMultiplier);

    private static float Multiplier(FixedPoint2 modSum, FixedPoint2 volume)
        => Math.Clamp(1f + (modSum / volume).Float(), MinMultiplier, MaxMultiplier);

    /// <summary>Profile of <paramref name="tank"/> after <paramref name="incoming"/> is poured into it.</summary>
    public bool TryComputeMixed(Solution tank, Solution incoming, out CMUFuelProfile profile)
    {
        var mixed = tank.Clone();
        mixed.MaxVolume = FixedPoint2.MaxValue;
        mixed.AddSolution(incoming, _prototypes);
        return TryCompute(mixed, out profile);
    }
}
