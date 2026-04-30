using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Ordnance;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Ordnance;

/// <summary>
///     Shared ordnance chemistry estimator used by scanners and simulators.
///     Keeping the numbers in one place helps the custom ordnance pipeline stay in parity.
/// </summary>
public static class RMCOrdnanceYieldEstimator
{
    private static readonly ProtoId<ReagentPrototype> IronReagent = "RMCIron";
    private static readonly ProtoId<ReagentPrototype> PhoronReagent = "RMCPhoron";
    private static readonly ProtoId<ReagentPrototype> SulphuricAcidReagent = "RMCSulphuricAcid";
    private static readonly ProtoId<ReagentPrototype> NeurotoxinReagent = "Neurotoxin";
    private static readonly ProtoId<ReagentPrototype> MindbreakerToxinReagent = "RMCMindbreakerToxin";

    private const int CMBaseShardCount = 4;
    private const int SpecialShardThreshold = 10;
    private const int SpecialShardCapReduction = 2;

    /// <summary>
    ///     Default simulator profile used when we only have raw chemistry and no physical casing profile.
    /// </summary>
    public static readonly RMCOrdnanceYieldProfile ExplosionSimulationProfile = new(
        180f,
        80f,
        25f,
        3f,
        25f,
        1f,
        5f,
        3f,
        24f,
        new RMCOrdnanceShrapnelProfile(
            40,
            360f,
            false,
            20f,
            "CMProjectileShrapnel",
            "RMCShrapnelIncendiary",
            "RMCHornetRound",
            "CMProjectileShrapnel"),
        "RMCTileFire");

    /// <summary>
    ///     Builds a reusable yield profile from a live custom casing.
    /// </summary>
    public static RMCOrdnanceYieldProfile FromCasing(RMCChembombCasingComponent casing)
    {
        return new RMCOrdnanceYieldProfile(
            casing.BasePower,
            casing.BaseFalloff,
            casing.MinFalloff,
            casing.MinFireIntensity,
            casing.MaxFireIntensity,
            casing.MinFireRadius,
            casing.MaxFireRadius,
            casing.MinFireDuration,
            casing.MaxFireDuration,
            new RMCOrdnanceShrapnelProfile(
                casing.MaxShards,
                casing.ShrapnelSpread,
                casing.DirectionalShrapnel,
                casing.ShrapnelSpeed,
                casing.DefaultShrapnelProto,
                casing.IncendiaryShrapnelProto,
                casing.HornetShrapnelProto,
                casing.NeuroShrapnelProto),
            casing.DefaultFireEntity);
    }

    /// <summary>
    ///     Estimates blast and incendiary output for a chemical payload.
    /// </summary>
    public static RMCOrdnanceYieldEstimate Estimate(
        Solution? solution,
        IPrototypeManager prototype,
        in RMCOrdnanceYieldProfile profile)
    {
        var powerMod = 0f;
        var falloffMod = 0f;
        var intensityMod = 0f;
        var radiusMod = 0f;
        var durationMod = 0f;
        var shardMod = 0f;
        var shardKind = RMCOrdnanceShardKind.Normal;
        var hasExplosive = false;
        var hasIncendiary = false;
        var fireEntity = profile.DefaultFireEntity;
        var currentVolume = solution?.Volume.Float() ?? 0f;

        if (solution != null)
        {
            foreach (var reagent in solution)
            {
                if (!prototype.TryIndexReagent(reagent.Reagent.Prototype, out ReagentPrototype? proto))
                    continue;

                var quantity = (float) reagent.Quantity;
                powerMod += quantity * (float) proto.PowerMod;
                falloffMod += quantity * (float) proto.FalloffMod;
                intensityMod += quantity * (float) proto.IntensityMod;
                radiusMod += quantity * (float) proto.RadiusMod;
                durationMod += quantity * (float) proto.DurationMod;
                if (reagent.Reagent.Prototype == IronReagent || proto.ShardMod != FixedPoint2.Zero)
                    shardMod += quantity * (float) proto.ShardMod;

                if (proto.PowerMod > FixedPoint2.Zero)
                    hasExplosive = true;

                if (proto.IntensityMod > FixedPoint2.Zero || proto.Intensity > 0)
                {
                    hasIncendiary = true;

                    if (proto.Intensity > 0)
                        fireEntity = proto.FireEntity;
                }

                shardKind = ResolveShardKind(reagent.Reagent.Prototype, quantity, shardKind);
            }
        }

        var hasExplosion = hasExplosive || powerMod > 0f;
        var blastPower = hasExplosion
            ? MathF.Max(1f, profile.BasePower + powerMod)
            : 0f;
        var blastFalloff = hasExplosion
            ? MathF.Max(profile.MinFalloff, profile.BaseFalloff + falloffMod)
            : 0f;
        var intensitySlope = hasExplosion
            ? MathF.Max(1.5f, blastFalloff / 14f)
            : 0f;
        var maxIntensity = hasExplosion
            ? MathF.Max(5f, blastPower / 15f)
            : 0f;
        var shrapnel = ResolveShrapnelEstimate(hasExplosion, shardMod, shardKind, profile.Shrapnel);

        var fireIntensity = hasIncendiary
            ? Math.Clamp(profile.MinFireIntensity + intensityMod, profile.MinFireIntensity, profile.MaxFireIntensity)
            : 0f;
        var fireRadius = hasIncendiary
            ? Math.Clamp(profile.MinFireRadius + radiusMod, profile.MinFireRadius, profile.MaxFireRadius)
            : 0f;
        var fireDuration = hasIncendiary
            ? Math.Clamp(profile.MinFireDuration + durationMod, profile.MinFireDuration, profile.MaxFireDuration)
            : 0f;

        return new RMCOrdnanceYieldEstimate(
            currentVolume,
            hasExplosion,
            blastPower,
            blastFalloff,
            intensitySlope,
            maxIntensity,
            shrapnel,
            hasIncendiary,
            fireIntensity,
            fireRadius,
            fireDuration,
            fireEntity);
    }

    private static RMCOrdnanceShardKind ResolveShardKind(
        ProtoId<ReagentPrototype> reagent,
        float quantity,
        RMCOrdnanceShardKind current)
    {
        if (quantity < SpecialShardThreshold)
            return current;

        var candidate = reagent switch
        {
            var id when id == PhoronReagent => RMCOrdnanceShardKind.Incendiary,
            var id when id == SulphuricAcidReagent => RMCOrdnanceShardKind.Hornet,
            var id when id == NeurotoxinReagent || id == MindbreakerToxinReagent => RMCOrdnanceShardKind.Neuro,
            _ => current,
        };

        return candidate > current ? candidate : current;
    }

    private static RMCOrdnanceShrapnelEstimate ResolveShrapnelEstimate(
        bool hasExplosion,
        float shardMod,
        RMCOrdnanceShardKind shardKind,
        in RMCOrdnanceShrapnelProfile profile)
    {
        if (!hasExplosion)
            return RMCOrdnanceShrapnelEstimate.Disabled(profile);

        var shardCap = profile.MaxShards;
        if (shardKind != RMCOrdnanceShardKind.Normal)
            shardCap = Math.Max(CMBaseShardCount, shardCap / SpecialShardCapReduction);

        var shardCount = Math.Clamp(CMBaseShardCount + (int) MathF.Floor(shardMod), 0, shardCap);
        if (shardCount <= 0)
            return RMCOrdnanceShrapnelEstimate.Disabled(profile);

        var projectileProto = shardKind switch
        {
            RMCOrdnanceShardKind.Incendiary => profile.IncendiaryProto,
            RMCOrdnanceShardKind.Hornet => profile.HornetProto,
            RMCOrdnanceShardKind.Neuro => profile.NeuroProto,
            _ => profile.DefaultProto,
        };

        return new RMCOrdnanceShrapnelEstimate(
            true,
            shardCount,
            shardKind,
            profile.SpreadAngle,
            profile.UseCasingDirection,
            profile.ProjectileSpeed,
            projectileProto);
    }
}

/// <summary>
///     CMSS13 shard families supported by the local custom ordnance pipeline.
/// </summary>
public enum RMCOrdnanceShardKind : byte
{
    Normal,
    Incendiary,
    Hornet,
    Neuro,
}

/// <summary>
///     Static shard configuration for a casing or simulator profile.
/// </summary>
public readonly record struct RMCOrdnanceShrapnelProfile(
    int MaxShards,
    float SpreadAngle,
    bool UseCasingDirection,
    float ProjectileSpeed,
    EntProtoId DefaultProto,
    EntProtoId IncendiaryProto,
    EntProtoId HornetProto,
    EntProtoId NeuroProto);

/// <summary>
///     Final shard output after chemistry modifiers and casing caps are applied.
/// </summary>
public readonly record struct RMCOrdnanceShrapnelEstimate(
    bool Enabled,
    int Count,
    RMCOrdnanceShardKind Kind,
    float SpreadAngle,
    bool UseCasingDirection,
    float ProjectileSpeed,
    EntProtoId ProjectileProto)
{
    public static RMCOrdnanceShrapnelEstimate Disabled(in RMCOrdnanceShrapnelProfile profile)
    {
        return new RMCOrdnanceShrapnelEstimate(
            false,
            0,
            RMCOrdnanceShardKind.Normal,
            profile.SpreadAngle,
            profile.UseCasingDirection,
            profile.ProjectileSpeed,
            profile.DefaultProto);
    }
}

/// <summary>
///     Envelope that defines how a casing converts reagent modifiers into final blast and fire output.
/// </summary>
public readonly record struct RMCOrdnanceYieldProfile(
    float BasePower,
    float BaseFalloff,
    float MinFalloff,
    float MinFireIntensity,
    float MaxFireIntensity,
    float MinFireRadius,
    float MaxFireRadius,
    float MinFireDuration,
    float MaxFireDuration,
    RMCOrdnanceShrapnelProfile Shrapnel,
    EntProtoId DefaultFireEntity);

/// <summary>
///     Final yield estimate returned by <see cref="RMCOrdnanceYieldEstimator"/>.
/// </summary>
public readonly record struct RMCOrdnanceYieldEstimate(
    float CurrentVolume,
    bool HasExplosion,
    float TotalIntensity,
    float BlastFalloff,
    float IntensitySlope,
    float MaxIntensity,
    RMCOrdnanceShrapnelEstimate Shrapnel,
    bool HasFire,
    float FireIntensity,
    float FireRadius,
    float FireDuration,
    EntProtoId FireEntity)
{
    public bool HasShards => Shrapnel.Enabled;

    public int ShardCount => Shrapnel.Count;

    public EntProtoId ShrapnelProto => Shrapnel.ProjectileProto;

    public float ApproxBlastRadius => HasExplosion
        ? MathF.Sqrt(MathF.Max(1f, TotalIntensity) / MathF.Max(1.5f, IntensitySlope))
        : 0f;
}
