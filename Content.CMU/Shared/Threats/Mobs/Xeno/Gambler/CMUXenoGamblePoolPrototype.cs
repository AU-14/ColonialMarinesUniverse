using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;

[Prototype("cmuXenoGamblePool")]
public sealed partial class CMUXenoGamblePoolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<CMUXenoGambleTier> Tiers = new();
}

[DataDefinition]
public sealed partial class CMUXenoGambleTier
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public float Weight;

    [DataField]
    public int Priority;

    [DataField]
    public List<SoundSpecifier> Sounds = new();

    [DataField]
    public SpriteSpecifier? Overlay;

    [DataField]
    public float StopPop;

    [DataField]
    public Color StopPopColor = Color.White;

    [DataField]
    public bool StopPopRainbow;

    [DataField(required: true)]
    public List<CMUXenoGambleReward> Rewards = new();
}

[DataDefinition]
public sealed partial class CMUXenoGambleReward
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField(required: true)]
    public SpriteSpecifier Icon = SpriteSpecifier.Invalid;

    [DataField]
    public ComponentRegistry Components = new();

    // for doafter abilities
    [DataField]
    public bool ConsumeOnCooldown;
}
