using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.CMU14.Chemistry.Effects.Reaction;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Fuel;

/// <summary>How a solution is being set alight; decides the caps <see cref="CMUFuelProfileSystem.TryResolveFire"/> applies.</summary>
public enum CMUFireSource
{
    /// <summary>Projected by a flamethrower: the tank's caps, and blue (40+) only from specialist tanks.</summary>
    Flamer,
    /// <summary>A heated beaker or other container igniting its contents.</summary>
    Container,
    /// <summary>A chemical payload (chem grenade) detonating.</summary>
    Explosion,
    /// <summary>A thrown firebomb shattering.</summary>
    Molotov,
}

/// <summary>
/// FLOW-22: the one fire spec every ignition path uses, so the same fuel burns with the same statistics whether
/// it is fired from a flamer, heated in a beaker, detonated in a grenade or thrown in a bottle.
/// </summary>
/// <param name="Intensity">Fire intensity after the source's caps.</param>
/// <param name="Duration">Burn (ignition) duration after the source's caps.</param>
/// <param name="TileLifetimeMultiplier">Multiplier on the tile fire prototype's lifetime.</param>
/// <param name="Radius">Maximum range: flamer length, or circle radius for the other sources.</param>
/// <param name="BehaviourEntity">Tile fire prototype to spawn. Colour no longer comes from it, only special behaviour.</param>
/// <param name="Tier">Colour tier from <see cref="CMUFuelProfileSystem.TierFor"/>.</param>
/// <param name="PenetrationLevel">Highest Firepenetrating level making up at least a tenth of the solution.</param>
/// <param name="FireSpread">Flamer "thick" spray (Flowing fuels, B-gel).</param>
/// <param name="SpreadDepth">Tile creep: positive forces that depth (Flowing), 0 follows the map default,
/// <see cref="CMUFireSpec.NoSpread"/> suppresses it (Viscous at least as strong as Flowing).</param>
public readonly record struct CMUFireSpec(
    int Intensity,
    int Duration,
    float TileLifetimeMultiplier,
    int Radius,
    EntProtoId BehaviourEntity,
    int Tier,
    int PenetrationLevel,
    bool FireSpread = false,
    int SpreadDepth = 0)
{
    public const int NoSpread = -1;

    public bool IsValid => Intensity > 0 && Duration > 0 && Radius > 0;
}

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
    /// solution that is fuel. Every non-fuel reagent's <c>IntensityMod/DurationMod/RadiusMod</c> then scale the
    /// result as a per-unit fractional multiplier, which lets non-fuel additives such as a Flowing or Viscous
    /// research chemical shape a napalm blend. Fire entity and spread come from the largest fuel component. The blend is
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

            // A fuel's *Mod values never rescale a tank: hand-authored fuels' mods are molotov tuning
            // (SharedRMCFlammableSystem) already baked into their flamer stats, and a generated fuel's
            // per-level property bonuses are already in its intensity/duration, so applying its mods too
            // would count every Oxidizing level twice. Modifiers apply only from additives (non-fuels such as
            // phoron or a generated Flowing chemical); fuels shape a blend through the volume-weighted mean.
            if (!isFuel)
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

    #region FLOW-22 fire spec

    /// <summary>Blue fire starts here; only specialist tanks (<see cref="RMCFlamerTankComponent.AllowBlueFire"/>) reach it.</summary>
    public const int BlueTierIntensity = 40;

    /// <summary>Highest intensity any non-specialist source (standard tanks, containers, grenades, molotovs) can burn at.</summary>
    public const int NonSpecialistMaxIntensity = BlueTierIntensity - 1;

    public const int ContainerMaxDuration = 30;

    /// <summary>Alcohol molotovs: Σ intensityMod × units must reach this, as RMC's CraftsIntoMolotov did.</summary>
    public const int MolotovMinIntensity = 10;

    /// <summary>Radius of an alcohol (non-fuel) molotov, RMC's old diamond radius.</summary>
    public const int MolotovFallbackRadius = 2;

    public const int MaxSpreadDepth = 5;

    public static readonly EntProtoId GenericFire = "RMCTileFire";
    public static readonly EntProtoId BlueFire = "RMCTileFireBlue";

    /// <summary>Colour tier by effective intensity: 1 red (&lt;20), 2 orange (20-34), 3 yellow (35-39), 4 blue (40-59), 5 white (60+).</summary>
    public static int TierFor(int intensity)
    {
        if (intensity < 20)
            return 1;
        if (intensity < 35)
            return 2;
        if (intensity < BlueTierIntensity)
            return 3;
        if (intensity < 60)
            return 4;
        return 5;
    }

    public static Color TierColor(int tier)
    {
        return tier switch
        {
            <= 1 => Color.FromHex("#E8401C"),
            2 => Color.FromHex("#FF8C1A"),
            3 => Color.FromHex("#FFD83A"),
            4 => Color.FromHex("#00B8FF"),
            _ => Color.FromHex("#F0F6FF"),
        };
    }

    /// <summary>
    /// Fire stacks a tile fire of this tier may build up. A behaviour entity with its own lower cap (green gel's 10,
    /// a brute fire's 1) keeps it.
    /// </summary>
    public static int TierMaxStacks(int tier, int? entityMaxStacks = null)
    {
        var stacks = tier switch
        {
            <= 3 => 20,
            4 => 40,
            _ => 70,
        };

        if (entityMaxStacks is { } own && own < 20)
            return Math.Min(own, stacks);

        return stacks;
    }

    /// <summary>Share of its capped reach a standard (non-specialist) tank projects.</summary>
    public const float NonSpecialistReachFraction = 0.6f;

    /// <summary>
    /// Reach of a standard tank with a capped reach of <paramref name="reach"/>: an LC9 with UT (5) reaches 3, with
    /// B-gel (7) 4; specialist tanks keep their whole reach.
    /// </summary>
    public static int NonSpecialistReach(int reach)
        => reach <= 0
            ? reach
            : Math.Max(1, (int) MathF.Round(reach * NonSpecialistReachFraction, MidpointRounding.AwayFromZero));

    /// <summary>The largest stock fuel reach (B-gel, UT tank napthal); a chemical fire never spreads further.</summary>
    public const int MaxChemicalRadius = 7;

    /// <summary>
    /// Circle radius of a chemical fire from its contents: √units, plus one for fuels that spread (B-gel), at least 1,
    /// at most the fuel's own reach.
    /// </summary>
    public static int CircleRadius(FixedPoint2 units, CMUFireSpec spec)
        => CircleRadius(units, spec.Radius, spec.FireSpread ? 1 : 0);

    public static int CircleRadius(FixedPoint2 units, int maxRadius, int bonus)
    {
        var radius = (int) MathF.Round(MathF.Sqrt(Math.Max(0f, units.Float()))) + bonus;
        return Math.Clamp(radius, 1, Math.Max(1, maxRadius));
    }

    /// <summary>Circle radius for <paramref name="units"/> of burning solution: √units, at least 1, at most <paramref name="maxRadius"/>.</summary>
    public static int CircleRadius(FixedPoint2 units, int maxRadius)
    {
        var radius = (int) MathF.Round(MathF.Sqrt(MathF.Max(0f, units.Float())));
        return Math.Clamp(radius, 1, Math.Max(1, maxRadius));
    }

    /// <summary>Firepenetrating level of a reagent; the legacy bool counts as level 1.</summary>
    public static int PenetrationLevel(ReagentPrototype reagent)
        => Math.Max(reagent.FirePenetratingLevel, reagent.FirePenetrating ? 1 : 0);

    /// <summary>Level (potency) of the RMC chemical property <typeparamref name="T"/> on a reagent, 0 when absent.</summary>
    public static int PropertyLevel<T>(ReagentPrototype reagent) where T : RMCChemicalEffect
    {
        if (reagent.Metabolisms is not { } metabolisms)
            return 0;

        var level = 0;
        foreach (var entry in metabolisms.Metabolisms.Values)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is T property)
                    level = Math.Max(level, (int) MathF.Round(property.Potency));
            }
        }

        return level;
    }

    /// <summary>Tile creep from Flowing/Viscous levels; see <see cref="CMUFireSpec.SpreadDepth"/>.</summary>
    public static int SpreadDepthFor(int flowing, int viscous)
    {
        if (viscous > 0 && viscous >= flowing)
            return CMUFireSpec.NoSpread;

        if (flowing <= 0)
            return 0;

        return Math.Clamp(1 + flowing, 2, MaxSpreadDepth);
    }

    /// <summary>Builds a spec from already-capped stats, deriving tier and lifetime.</summary>
    public static CMUFireSpec CreateSpec(int intensity, int duration, int radius, EntProtoId behaviour, int penetrationLevel,
        bool fireSpread = false, int spreadDepth = 0)
    {
        return new CMUFireSpec(intensity,
            duration,
            TileLifetimeMultiplier(duration),
            radius,
            behaviour,
            TierFor(intensity),
            penetrationLevel,
            fireSpread,
            spreadDepth);
    }

    /// <summary>
    /// Resolves the fire <paramref name="solution"/> makes when lit by <paramref name="source"/>. Wraps
    /// <see cref="TryCompute"/> and applies the source's caps: a flamer uses <paramref name="tank"/>'s caps and, unless
    /// the tank allows blue fire, never exceeds <see cref="NonSpecialistMaxIntensity"/>; every other source is capped
    /// at 39 intensity and sized by their contents (fuel reach up to <see cref="MaxChemicalRadius"/>). A
    /// molotov of alcohol (no fuel stats) still burns from Σ intensityMod × units, like RMC's.
    /// </summary>
    public bool TryResolveFire(Solution solution,
        CMUFireSource source,
        out CMUFireSpec spec,
        Entity<RMCFlamerTankComponent>? tank = null)
    {
        spec = default;
        if (solution.Volume <= FixedPoint2.Zero)
            return false;

        ScanProperties(solution, out var penetration, out var flowing, out var viscous);
        var spreadDepth = SpreadDepthFor(flowing, viscous);

        int intensity, duration, radius;
        EntProtoId behaviour;
        bool fireSpread;
        if (TryCompute(solution, out var profile))
        {
            intensity = profile.Intensity;
            duration = profile.Duration;
            radius = profile.Radius;
            behaviour = profile.FireEntity;
            fireSpread = profile.FireSpread;
        }
        else if (source == CMUFireSource.Molotov && TryAlcoholIntensity(solution, out var alcohol))
        {
            intensity = alcohol;
            duration = ReferenceDuration;
            radius = MolotovFallbackRadius;
            behaviour = GenericFire;
            fireSpread = false;
        }
        else
        {
            return false;
        }

        var allowBlue = false;
        if (source == CMUFireSource.Flamer && tank is { } t)
        {
            allowBlue = t.Comp.AllowBlueFire;
            intensity = Math.Min(t.Comp.MaxIntensity, intensity);
            duration = Math.Min(t.Comp.MaxDuration, duration);
            radius = t.Comp.IgnoreReagentRange ? t.Comp.MaxRange : Math.Min(t.Comp.MaxRange, radius);
            if (!allowBlue && IsBlueFire(behaviour))
                behaviour = t.Comp.FallbackFire;
            // Reach is the fuel's range; only specialist tanks project all of it.
            if (!allowBlue)
                radius = NonSpecialistReach(radius);
        }
        else
        {
            // Chemical fires (containers, grenades, molotovs) are sized by their contents, not their container: the
            // fuel's own reach and duration, with the amount burning deciding how much of that reach is used
            // (see CircleRadius). Only a sanity ceiling applies.
            radius = Math.Min(MaxChemicalRadius, radius);
            if (IsBlueFire(behaviour))
                behaviour = GenericFire;
        }

        if (!allowBlue)
            intensity = Math.Min(NonSpecialistMaxIntensity, intensity);

        spec = CreateSpec(intensity, duration, radius, behaviour, penetration, fireSpread, spreadDepth);
        return spec.IsValid;
    }

    /// <summary>RMC's molotov strength: Σ intensityMod × units, clamped to 10-39. False below <see cref="MolotovMinIntensity"/>.</summary>
    public bool TryAlcoholIntensity(Solution solution, out int intensity)
    {
        intensity = 0;
        var sum = FixedPoint2.Zero;
        foreach (var quantity in solution.Contents)
        {
            if (!_reagent.TryIndex(quantity.Reagent.Prototype, out var reagent))
                continue;

            sum += reagent.IntensityMod * quantity.Quantity;
        }

        if (sum < MolotovMinIntensity)
            return false;

        intensity = Math.Clamp((int) Math.Round(sum.Float()), MolotovMinIntensity, NonSpecialistMaxIntensity);
        return true;
    }

    /// <summary>
    /// Highest Firepenetrating, Flowing and Viscous levels among the reagents making up at least
    /// <see cref="PenetratingFraction"/> of the solution, so traces do not change how a whole tank burns.
    /// </summary>
    public void ScanProperties(Solution solution, out int penetration, out int flowing, out int viscous)
    {
        penetration = 0;
        flowing = 0;
        viscous = 0;
        if (solution.Volume <= FixedPoint2.Zero)
            return;

        foreach (var quantity in solution.Contents)
        {
            if (quantity.Quantity / solution.Volume < PenetratingFraction ||
                !_reagent.TryIndex(quantity.Reagent.Prototype, out var reagent))
            {
                continue;
            }

            penetration = Math.Max(penetration, PenetrationLevel(reagent));
            flowing = Math.Max(flowing, PropertyLevel<Flowing>(reagent));
            viscous = Math.Max(viscous, PropertyLevel<Viscous>(reagent));
        }
    }

    /// <summary>True for fuel reagents (positive intensity, duration and radius), the ones a fire burns away.</summary>
    public bool IsFuel(ProtoId<ReagentPrototype> id)
        => _reagent.TryIndex(id, out var reagent) && reagent.Intensity > 0 && reagent.Duration > 0 && reagent.Radius > 0;

    /// <summary>Whether <paramref name="fire"/> is blue fire (<see cref="BlueFire"/> or a child of it).</summary>
    public bool IsBlueFire(EntProtoId fire)
    {
        if (fire.Id == BlueFire.Id)
            return true;

        if (!_prototypes.HasIndex(fire))
            return false;

        foreach (var parent in _prototypes.EnumerateParents<EntityPrototype>(fire.Id))
        {
            if (parent.ID == BlueFire.Id)
                return true;
        }

        return false;
    }

    #endregion
}
