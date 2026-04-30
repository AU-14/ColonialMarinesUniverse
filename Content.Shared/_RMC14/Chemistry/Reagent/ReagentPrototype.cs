// ReSharper disable CheckNamespace

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
    /// <summary>
    ///     Additional CM-style shard count contributed per unit when this reagent is present in a custom ordnance mix.
    ///     Runtime shard count starts from a fixed base and is then clamped by the casing's shard cap.
    /// </summary>
    [DataField]
    public FixedPoint2 ShardMod;

    [DataField]
    public bool Unknown;

    [DataField]
    public FixedPoint2? Overdose;

    [DataField]
    public FixedPoint2? CriticalOverdose;

    [DataField]
    public int Intensity;

    [DataField]
    public int Duration;

    [DataField]
    public int Radius;

    [DataField]
    public EntProtoId FireEntity = "RMCTileFire";

    [DataField]
    public FixedPoint2 IntensityMod;

    [DataField]
    public FixedPoint2 DurationMod;

    [DataField]
    public FixedPoint2 RadiusMod;

    [DataField]
    public FixedPoint2 PowerMod;

    [DataField]
    public FixedPoint2 FalloffMod;

    [DataField]
    public bool FireSpread;

    [DataField]
    public bool Toxin;

    [DataField]
    public bool Alcohol;
}
