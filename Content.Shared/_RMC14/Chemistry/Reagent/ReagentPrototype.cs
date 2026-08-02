// ReSharper disable CheckNamespace

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
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
    public bool FireSpread;

    [DataField]
    public ReagentClass Class = ReagentClass.None;

    [DataField]
    public ReagentFlags Flags = 0;

    [DataField]
    public int GenTier = 0;

    [DataField]
    public bool Generated = false;

    [DataField]
    public int Reward = 2;

    [DataField]
    public bool Lockdown = false;

    [DataField]
    public bool Toxin;

    [DataField]
    public bool Alcohol;
}

public enum ReagentClass
{
    None = 0,
    Basic = 1,
    Common = 2,
    Uncommon = 3,
    Rare = 4,
    Special = 5,
    Ultra = 6,
    Hydro = 7
}
[Flags]
public enum ReagentFlags
{
    Medical = 1 << 0,
    Scannable = 1 << 1,
    NotIngestible = 1 << 2,
    CannotOverdose = 1 << 3,
    Stimulant = 1 << 4,
    NoGeneration = 1 << 5,
    Specialist = 1 << 6
}
