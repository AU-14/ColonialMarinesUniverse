using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fighter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUManpadLowAltitudeComponent : Component
{
    [DataField]
    public EntProtoId AimActionId = "CMUActionManpadLowAltitudeAim";

    [DataField, AutoNetworkedField]
    public EntityUid? AimAction;

    [AutoNetworkedField]
    public EntityUid? AimingUser;

    [AutoNetworkedField]
    public CMUManpadLowAltitudeLockState LockState;

    [AutoNetworkedField]
    public CMUManpadLowAltitudeTargetKind TargetKind;

    [DataField]
    public TimeSpan LockTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan LockDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    /// <summary>Tiles.</summary>
    public float LockRange = 15f;

    [DataField]
    public TimeSpan MissileFlightTime = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/manpad-launch.wav");

    [DataField]
    public SoundSpecifier IncomingSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/manpad-incoming.wav");

    [DataField]
    public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/missile-impact.ogg");

    [DataField]
    public CMUManpadLowAltitudeHitProfile GunshipHit = new()
    {
        Damage = 800f,
        Effect = "CMUManpadGunshipHitEffect",
        EffectCount = 3,
        EffectSpread = 2.5f,
    };

    [DataField]
    public CMUManpadLowAltitudeHitProfile BlackfootHit = new()
    {
        Damage = 400f,
        Effect = "CMUManpadBlackfootHitEffect",
        EffectCount = 2,
        EffectSpread = 0.8f,
    };

    [DataField]
    /// <summary>Tiles.</summary>
    public float SoundRange = 65f;

    [DataField]
    public SoundSpecifier LockingSound = new SoundPathSpecifier(
        "/Audio/Machines/quickbeep.ogg",
        AudioParams.Default.WithVolume(-4));

    [DataField]
    public SoundSpecifier LockedSound = new SoundPathSpecifier(
        "/Audio/Machines/quickbeep.ogg",
        AudioParams.Default.WithVolume(-4).WithPitchScale(1.6f));

    [DataField]
    public TimeSpan BeepIntervalStart = TimeSpan.FromSeconds(0.7);

    [DataField]
    public TimeSpan BeepIntervalEnd = TimeSpan.FromSeconds(0.2);
}

[Serializable, NetSerializable]
public enum CMUManpadLowAltitudeLockState : byte
{
    None,
    Locking,
    Locked,
}

[DataDefinition]
public sealed partial class CMUManpadLowAltitudeHitProfile
{
    [DataField]
    public float Damage;

    [DataField]
    public EntProtoId Effect;

    [DataField]
    public int EffectCount;

    [DataField]
    /// <summary>Tiles.</summary>
    public float EffectSpread;
}

[Serializable, NetSerializable]
public enum CMUManpadLowAltitudeTargetKind : byte
{
    None,
    Gunship,
    Blackfoot,
}

public sealed partial class CMUManpadLowAltitudeAimActionEvent : InstantActionEvent;

[ByRefEvent]
public readonly record struct CMUManpadLowAltitudeAimStartedEvent(EntityUid User);

[ByRefEvent]
public readonly record struct CMUManpadLowAltitudeAimStoppedEvent;

[ByRefEvent]
public readonly record struct CMUManpadLowAltitudeLaunchRequestEvent(EntityUid User);
