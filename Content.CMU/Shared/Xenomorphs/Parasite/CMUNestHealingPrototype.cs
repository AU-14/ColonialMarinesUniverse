using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs.Parasite;

/// <summary>
///     Balance settings for passive healing of living nested human hosts.
/// </summary>
[Prototype("cmuNestHealing")]
public sealed partial class CMUNestHealingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public FixedPoint2 HealPerSecond;

    /// <summary>
    ///     Maximum remaining health as a fraction of the death threshold.
    /// </summary>
    [DataField(required: true)]
    public float MaxHealthFraction;
}
