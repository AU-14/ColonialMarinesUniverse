using Content.Shared.Chat.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Medical.StatusEffects;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUPainFeedbackComponent : Component
{
    [DataField]
    public TimeSpan EffectInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextEffect;

    [DataField]
    public TimeSpan SevereBlurDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ShockBlurDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public float SevereBlurStartAmount = 0.1f;

    [DataField]
    public float ShockBlurStartAmount = 2.25f;

    [DataField]
    public float ShockBlurAmount = 4f;

    [DataField]
    public float ShockBlurFullPain = 95f;

    [DataField]
    public TimeSpan SevereStutterDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ShockStutterDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public float SevereDrunkPower = 2f;

    [DataField]
    public float ShockDrunkPower = 6f;

    [DataField]
    public FixedPoint2 SevereAsphyxiation = FixedPoint2.New(0.1);

    [DataField]
    public FixedPoint2 ShockAsphyxiation = FixedPoint2.New(0.5);

    [DataField]
    public float SevereEmoteChance = 0.033f;

    [DataField]
    public float ShockEmoteChance = 0.033f;

    [DataField]
    public List<ProtoId<EmotePrototype>> SevereEmotes = new()
    {
        "PainGrimace",
        "TroubleEyeOpen",
    };

    [DataField]
    public List<ProtoId<EmotePrototype>> ShockEmotes = new()
    {
        "TroubleStanding",
    };
}
