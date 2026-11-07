using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Synth;

[Prototype("cmuSynthGeneration")]
public sealed partial class CMUSynthGenerationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId GenerationName { get; private set; } = default!;

    [DataField]
    public FixedPoint2 CritThreshold { get; private set; }

    [DataField]
    public FixedPoint2 DeadThreshold { get; private set; }

    [DataField]
    public ProtoId<DamageModifierSetPrototype> DamageModifier { get; private set; } = "RMCSynth";

    [DataField]
    public float MovementSpeedModifier { get; private set; } = 1f;

    [DataField]
    public int SkillLevelOffset { get; private set; }

    [DataField]
    public float FrontalLeapBlockChance { get; private set; }
}