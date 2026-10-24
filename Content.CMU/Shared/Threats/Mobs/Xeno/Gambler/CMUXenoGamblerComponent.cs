using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUXenoGamblerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CMUXenoGamblePoolPrototype> Pool;

    [DataField]
    public int Rolls = 3;

    [DataField]
    public EntProtoId ReelAction = "CMUActionXenoGambleReel";

    // :godo:
    [DataField]
    public List<TimeSpan> SpinDelays = new()
    {
        TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04),
        TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04),
        TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.04), TimeSpan.FromSeconds(0.06), TimeSpan.FromSeconds(0.06),
        TimeSpan.FromSeconds(0.06), TimeSpan.FromSeconds(0.06), TimeSpan.FromSeconds(0.08), TimeSpan.FromSeconds(0.08),
        TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.15), TimeSpan.FromSeconds(0.2),
        TimeSpan.FromSeconds(0.3), TimeSpan.FromSeconds(0.4), TimeSpan.FromSeconds(0.55), TimeSpan.FromSeconds(0.75),
        TimeSpan.FromSeconds(0.95), TimeSpan.FromSeconds(1.2),
    };

    [DataField]
    public TimeSpan ReelStopInterval = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan ReelPopDuration = TimeSpan.FromSeconds(0.35);

    [DataField]
    public float ReelPopScale = 0.4f;

    [DataField]
    public TimeSpan ResultHold = TimeSpan.FromSeconds(1.25);

    [DataField]
    public TimeSpan SpinCooldown = TimeSpan.FromSeconds(10);

    /// action gets remoed on use but we meed to keep the component around in some cases
    [DataField]
    public TimeSpan ComponentLinger = TimeSpan.FromSeconds(40);

    [DataField]
    public int MaxRollAttempts = 20;

    [DataField]
    public SoundSpecifier? SpinSound = new SoundPathSpecifier("/Audio/CMU14/Xeno/Gambler/play_on_use.ogg");

    [DataField]
    public SoundSpecifier? StepSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/motion_detector.ogg", AudioParams.Default.WithVolume(-10));

    [DataField]
    public TimeSpan MinStepSoundInterval = TimeSpan.FromSeconds(0.08);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastStepSound;

    [DataField]
    public SoundSpecifier? ReelStopSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/motion_detector.ogg", AudioParams.Default.WithVolume(-3));

    [DataField]
    public SpriteSpecifier BlankIcon = new SpriteSpecifier.Rsi(new ResPath("_RMC14/Actions/xeno_actions.rsi"), "blank");

    [DataField, AutoNetworkedField]
    public List<EntityUid> Reels = new();

    [DataField, AutoNetworkedField]
    public List<SpriteSpecifier> WinnerIcons = new();

    [DataField, AutoNetworkedField]
    public List<SpriteSpecifier> WinnerOverlays = new();

    [DataField, AutoNetworkedField]
    public List<int> WinnerTiers = new();

    [DataField]
    public SpriteSpecifier DefaultOverlay = new SpriteSpecifier.Rsi(new ResPath("CMU14/Actions/Xeno/gambler.rsi"), "template_won");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan? SpinStart;

    [DataField]
    public EntityUid? GambleAction;

    [DataField]
    public List<CMUXenoGambleRoll> PendingRolls = new();

    [DataField]
    public int LastTickStep;

    [DataField]
    public int ReelsStopped;

    [DataField]
    public bool ResultPlayed;

    [DataField]
    public List<CMUXenoGambleGrant> Grants = new();

    [DataField]
    public Dictionary<string, CMUXenoGambleGrantedComponent> GrantedComponents = new();
}

[DataDefinition]
public sealed partial class CMUXenoGambleRoll
{
    [DataField]
    public int Tier;

    [DataField]
    public int Reward;
}

[DataDefinition]
public sealed partial class CMUXenoGambleGrant
{
    [DataField]
    public EntityUid Action;

    [DataField]
    public CMUXenoGambleRoll Roll = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan GrantedAt;

    [DataField]
    public bool Used;
}

[DataDefinition]
public sealed partial class CMUXenoGambleGrantedComponent
{
    [DataField]
    public CMUXenoGambleRoll Source = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? RemoveAt;
}
