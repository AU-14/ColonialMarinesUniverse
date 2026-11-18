using Content.Shared.Damage.Prototypes;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.Taser;

/// <summary>
/// A taser that fires a wired probe instead of a stun bolt. Once the probe embeds in someone, pulling the trigger no
/// longer fires: holding it sends current down the wire, and letting go stops it. The longer the trigger is held the
/// harder it bites: light jitter and stutter, then convulsions and a stamina collapse, then burns, then flames.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUTetherTaserSystem))]
public sealed partial class CMUTetherTaserComponent : Component
{
    /// <summary>The probe embedded in someone, at the end of the wire back to this gun.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Probe;

    /// <summary>Who the probe is in.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>What's spawned on the target when the probe embeds: an invisible anchor the wire is drawn to.</summary>
    [DataField]
    public EntProtoId EmbeddedProbe = "CMUTaserProbeEmbedded";

    /// <summary>The wire snaps past this many tiles between the shooter and the target.</summary>
    [DataField]
    public float WireLength = 8f;

    /// <summary>With the trigger left alone this long, the wire is reeled in by itself.</summary>
    [DataField]
    public TimeSpan IdleRetract = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The trigger counts as held for this long after the last shot request. The client asks to shoot every frame the
    /// trigger is down, so this only needs to cover a slow tick or two.
    /// </summary>
    [DataField]
    public TimeSpan HoldWindow = TimeSpan.FromSeconds(0.25);

    /// <summary>Battery used per second of current.</summary>
    [DataField]
    public float ChargePerSecond = 15f;

    /// <summary>Stamina damage per second the moment the trigger is pulled.</summary>
    [DataField]
    public float StaminaPerSecond = 8f;

    /// <summary>How much faster the stamina damage climbs for each second the trigger stays held.</summary>
    [DataField]
    public float StaminaRampPerSecond = 6f;

    /// <summary>The stamina damage per second never goes past this.</summary>
    [DataField]
    public float MaxStaminaPerSecond = 40f;

    /// <summary>Seconds of current, all told, before the target starts smoking.</summary>
    [DataField]
    public float SmokeAfter = 10f;

    /// <summary>Seconds of current, all told, before the target bursts into flames.</summary>
    [DataField]
    public float IgniteAfter = 16f;

    /// <summary>How fast the built-up current fades, per second, while the trigger is let go.</summary>
    [DataField]
    public float CooldownPerSecond = 0.5f;

    /// <summary>Burn damage per second once the target is smoking.</summary>
    [DataField]
    public float HeatPerSecond = 2f;

    [DataField]
    public ProtoId<DamageTypePrototype> HeatType = "Heat";

    [DataField]
    public int IgniteIntensity = 4;

    [DataField]
    public int IgniteDuration = 10;

    [DataField]
    public int IgniteMaxStacks = 20;

    /// <summary>Jitter at the first touch of current, and at the most.</summary>
    [DataField]
    public float MinJitterAmplitude = 3f;

    [DataField]
    public float MaxJitterAmplitude = 30f;

    [DataField]
    public float MinJitterFrequency = 3f;

    [DataField]
    public float MaxJitterFrequency = 14f;

    /// <summary>Seconds of one unbroken squeeze for the convulsions to reach their worst.</summary>
    [DataField]
    public float ConvulsePeakAfter = 8f;

    /// <summary>Chance, each pulse once convulsing, that the target jerks around to face a new way.</summary>
    [DataField]
    public float MinThrashChance = 0.2f;

    [DataField]
    public float MaxThrashChance = 0.8f;

    /// <summary>Chance, each pulse at the worst of a long squeeze, that the target loses their grip on what they hold.</summary>
    [DataField]
    public float DropChance = 0.25f;

    /// <summary>The time between sparks, stamina hits and status messages while the current runs.</summary>
    [DataField]
    public TimeSpan PulseInterval = TimeSpan.FromSeconds(0.5);

    [DataField]
    public EntProtoId SparkEffect = "EffectSparks";

    /// <summary>The clicking of the current, played on the target over and over while the trigger is held.</summary>
    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/Taser/taser_click.ogg");

    /// <summary>How often <see cref="ClickSound"/> restarts; about as long as the sound itself.</summary>
    [DataField]
    public TimeSpan ClickInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>The emote the target lets out when the current first hits them.</summary>
    [DataField]
    public ProtoId<EmotePrototype> ScreamEmote = "Scream";

    /// <summary>A target won't scream again from a fresh squeeze until this long has passed.</summary>
    [DataField]
    public TimeSpan ScreamCooldown = TimeSpan.FromSeconds(6);

    /// <summary>The emote the target lets out as they burst into flames; its sound is voiced for their sex.</summary>
    [DataField]
    public ProtoId<EmotePrototype> BurningEmote = "CMUBurning";

    /// <summary>When the trigger was last pulled with the probe in.</summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastTrigger;

    /// <summary>When the probe went in, or the trigger was last let go; drives the idle reel-in.</summary>
    [DataField, AutoPausedField]
    public TimeSpan LastActivity;

    /// <summary>How long the trigger has been held this squeeze.</summary>
    [DataField]
    public float HeldFor;

    /// <summary>The built-up current: seconds of shock, fading slowly while the trigger is let go.</summary>
    [DataField]
    public float ShockSeconds;

    [DataField, AutoPausedField]
    public TimeSpan NextPulse;

    /// <summary>The stage last announced, so each message is only shown once on the way up.</summary>
    [DataField]
    public CMUTaserShockStage AnnouncedStage;

    [DataField, AutoPausedField]
    public TimeSpan NextClick;

    [DataField, AutoPausedField]
    public TimeSpan NextScream;
}

public enum CMUTaserShockStage : byte
{
    None,
    Jolt,
    Convulsing,
    Smoking,
    Burning,
}

/// <summary>The wired probe in flight. Embeds in whoever it hits, if they can be stunned.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUTaserProbeComponent : Component;

/// <summary>On someone with one or more taser probes in them, so they can pull them out.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUTetherTaserSystem))]
public sealed partial class CMUTaserTetheredComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Tasers = new();

    [DataField]
    public TimeSpan PullOutDelay = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class CMUTaserProbePullOutDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Raised on a tased target, server-side, to make them emote.</summary>
[ByRefEvent]
public readonly record struct CMUTaserEmoteEvent(ProtoId<EmotePrototype> Emote);
