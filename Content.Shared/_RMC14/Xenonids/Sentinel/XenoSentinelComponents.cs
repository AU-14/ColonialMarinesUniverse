using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Sentinel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoToxicSpitComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = FixedPoint2.New(30);

    [DataField, AutoNetworkedField]
    public float Speed = 30;

    [DataField, AutoNetworkedField]
    public EntProtoId ProjectileId = "XenoToxicSpitProjectile";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoSpitAcid", AudioParams.Default.WithVolume(-10f));
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoToxicSpitProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stacks = 5;

    [DataField, AutoNetworkedField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Effects/spray3.ogg", AudioParams.Default.WithVolume(-4f));
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoToxicSlashComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ActiveDuration = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public int MaxHits = 3;

    [DataField, AutoNetworkedField]
    public int StacksPerHit = 5;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.2f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier ActivateSound = new SoundCollectionSpecifier("XenoDrool", AudioParams.Default.WithVolume(-4f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier ExpireSound = new SoundCollectionSpecifier("XenoTailSwipe", AudioParams.Default.WithVolume(-8f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Effects/spray3.ogg", AudioParams.Default.WithVolume(-4f));
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoActiveToxicSlashComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public int HitsRemaining;

    [DataField, AutoNetworkedField]
    public int StacksPerHit;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoToxicSlashSpeedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.2f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoDrainStingComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PotencyPerStack = 6;

    [DataField, AutoNetworkedField]
    public float DamageDivisor = 5;

    [DataField, AutoNetworkedField]
    public float PlasmaMultiplier = 3.5f;

    [DataField, AutoNetworkedField]
    public float ConsumeFraction = 0.7f;

    [DataField, AutoNetworkedField]
    public int SurgeThreshold = 20;

    [DataField, AutoNetworkedField]
    public int SurgeArmor = 15;

    [DataField, AutoNetworkedField]
    public TimeSpan SurgeDuration = TimeSpan.FromSeconds(12);

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoTailSwipe", AudioParams.Default.WithVolume(-4f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier SurgePlasmaTransferSound = new SoundCollectionSpecifier("XenoDrool");

    [DataField, AutoNetworkedField]
    public SoundSpecifier SurgeHeadbiteSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_bite2.ogg");

    [DataField, AutoNetworkedField]
    public TimeSpan SurgeHeadbiteSoundDelay = TimeSpan.FromSeconds(0.5);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoIntoxicatedComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stacks;

    [DataField, AutoNetworkedField]
    public int MaxStacks = 30;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTick;

    [DataField, AutoNetworkedField]
    public TimeSpan TickEvery = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public FixedPoint2 TickBaseDamage = FixedPoint2.New(1);

    [DataField, AutoNetworkedField]
    public float TickDamageStackDivisor = 10;

    [DataField, AutoNetworkedField]
    public int TickDecay = 1;

    [DataField, AutoNetworkedField]
    public int HighStackThreshold = 20;

    [DataField, AutoNetworkedField]
    public float HighStackSlowAtThreshold = 0.85f;

    [DataField, AutoNetworkedField]
    public float HighStackSlowAtMax = 0.5f;

    [DataField, AutoNetworkedField]
    public int ResistReduction = 8;

    [DataField, AutoNetworkedField]
    public TimeSpan ResistDuration = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public EntityUid? LastSource;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoDrainSurgeComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public int Armor = 15;
}

[RegisterComponent]
[Access(typeof(XenoSentinelSystem))]
public sealed partial class XenoIntoxicatedResistingComponent : Component
{
    public bool Resisting;
    public bool WasDown;
    public bool ForcedDown;
}
