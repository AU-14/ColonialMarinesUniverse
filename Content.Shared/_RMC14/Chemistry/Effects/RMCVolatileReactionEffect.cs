using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._RMC14.Chemistry.Effects;

[DataDefinition]
public sealed partial class RMCVolatileReactionEffect : EventEntityEffect<RMCVolatileReactionEffect>
{
    [DataField("threshold")]
    public FixedPoint2 Threshold = FixedPoint2.Zero;

    [DataField("explosionType", customTypeSerializer: typeof(PrototypeIdSerializer<ExplosionPrototype>))]
    public string ExplosionType = "RMC";

    [DataField("defaultFireEntity")]
    public EntProtoId DefaultFireEntity = "RMCTileFire";

    [DataField("maxExplosivePower")]
    public float MaxExplosivePower = 175f;

    [DataField("baseExplosiveFalloff")]
    public float BaseExplosiveFalloff = 75f;

    [DataField("minFireRadius")]
    public int MinFireRadius = 1;

    [DataField("maxFireRadius")]
    public int MaxFireRadius = 5;

    [DataField("minFireIntensity")]
    public int MinFireIntensity = 3;

    [DataField("maxFireIntensity")]
    public int MaxFireIntensity = 20;

    [DataField("minFireDuration")]
    public int MinFireDuration = 3;

    [DataField("maxFireDuration")]
    public int MaxFireDuration = 24;

    public override bool ShouldLog => true;

    public override LogImpact LogImpact => LogImpact.High;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
