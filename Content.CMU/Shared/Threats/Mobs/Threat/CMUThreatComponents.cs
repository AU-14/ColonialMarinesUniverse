using Content.Shared.Actions;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Threat;

public enum CMUThreatKind : byte
{
    Common,
    Hunter,
    Smoker,
    Boomer,
    Screamer,
    Jockey,
    Spitter,
    Witch,
}

public enum CMUThreatAiState : byte
{
    Idle,
    Chasing,
    Telegraphing,
    UsingAbility,
    Pinning,
    Emerging,
    Enraged,
}

public enum CMUThreatPinKind : byte
{
    Hunter,
    Smoker,
    Jockey,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUThreatComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUThreatKind Kind;

    [DataField]
    public float DetectionRadius = 10f;

    [DataField]
    public float PursuitRadius = 24f;

    [DataField]
    public float AiUpdateInterval = 0.5f;

    [DataField]
    public float DelimbChance = 0.05f;

    [DataField]
    public float CorpseBurnDuration = 60f;

    public float CorpseBurnElapsed;

    [DataField]
    public SoundSpecifier? AshSound = new SoundPathSpecifier("/Audio/Effects/cig_snuff.ogg");

    [DataField]
    public Color EyeColor = Color.FromHex("#ff3030");

    [DataField]
    public SoundSpecifier? IdleSound = new SoundCollectionSpecifier("CMUThreatDocile",
        AudioParams.Default.WithVolume(-4f).WithMaxDistance(10f));

    [DataField]
    public SoundSpecifier? AttackSound = new SoundCollectionSpecifier("CMUThreatAttack",
        AudioParams.Default.WithVolume(-2f).WithMaxDistance(10f));

    [DataField]
    public SoundSpecifier? HurtSound = new SoundCollectionSpecifier("CMUThreatHurt",
        AudioParams.Default.WithVolume(-2f).WithMaxDistance(10f));

    [DataField]
    public SoundSpecifier? DeathSound = new SoundCollectionSpecifier("CMUThreatDeath",
        AudioParams.Default.WithVolume(-1f).WithMaxDistance(12f));

    [DataField]
    public TimeSpan IdleSoundMinInterval = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan IdleSoundMaxInterval = TimeSpan.FromSeconds(28);

    [DataField]
    public TimeSpan AttackSoundCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan HurtSoundCooldown = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float HurtSoundChance = 0.4f;

    [DataField, AutoNetworkedField]
    public CMUThreatAiState State;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    public EntityUid? RallyPoint;

    public TimeSpan NextAiUpdate;

    public TimeSpan NextIdleSound;

    public TimeSpan NextAttackSound;

    public TimeSpan NextHurtSound;
}

[RegisterComponent]
public sealed partial class CMUThreatRallyPointComponent : Component
{
    [DataField]
    public float Radius = 50f;

    [DataField]
    public float ArrivalRadius = 2f;
}

[RegisterComponent]
public sealed partial class CMUThreatBarricadeTargetComponent : Component;

[RegisterComponent]
public sealed partial class CMUThreatLightTargetComponent : Component;

[RegisterComponent]
public sealed partial class CMUCommonThreatComponent : Component
{
    [DataField]
    public float BilePriorityRadius = 18f;
}

[RegisterComponent]
public sealed partial class CMUHunterThreatComponent : Component
{
    [DataField] public EntProtoId Action = "ActionXenoLeap";
    [DataField] public float Range = 8f;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(12);
    [DataField] public TimeSpan TelegraphTime = TimeSpan.FromSeconds(0.6);
    [DataField] public TimeSpan FlightDuration = TimeSpan.FromSeconds(0.8);
    [DataField] public float Strength = 30f;
    [DataField] public DamageSpecifier ImpactDamage = new();
    [DataField] public DamageSpecifier PinDamage = new();
    [DataField] public TimeSpan PinDamageInterval = TimeSpan.FromSeconds(1);
    [DataField] public TimeSpan MaxPinDuration = TimeSpan.FromSeconds(20);
    [DataField] public EntProtoId? TelegraphPrototype;
    [DataField] public SoundSpecifier? Sound;
    public EntityUid? ActionEntity;
    public TimeSpan NextUseAt;
    public EntityUid? PendingTarget;
    public TimeSpan PendingUseAt;
}

[RegisterComponent]
public sealed partial class CMUHunterPouncingComponent : Component
{
    public TimeSpan EndsAt;
}

[RegisterComponent]
public sealed partial class CMUSmokerThreatComponent : Component
{
    [DataField] public EntProtoId Action = "ActionCMUThreatSmokerTongue";
    [DataField] public float Range = 8f;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(15);
    [DataField] public float PullSpeed = 3f;
    [DataField] public float HoldDistance = 1.25f;
    [DataField] public DamageSpecifier ChokeDamage = new();
    [DataField] public TimeSpan ChokeDamageInterval = TimeSpan.FromSeconds(1);
    [DataField] public TimeSpan MaxPinDuration = TimeSpan.FromSeconds(20);
    [DataField] public EntProtoId SmokePrototype = "CMUSmokeGrenade";
    [DataField] public SoundSpecifier? Sound;
    public EntityUid? ActionEntity;
    public TimeSpan NextUseAt;
}

[RegisterComponent]
public sealed partial class CMUBoomerThreatComponent : Component
{
    [DataField] public EntProtoId Action = "ActionCMUThreatBoomerVomit";
    [DataField] public float Range = 5f;
    [DataField] public float ConeDegrees = 70f;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(15);
    [DataField] public TimeSpan BileDuration = TimeSpan.FromSeconds(20);
    [DataField] public float HordeRadius = 18f;
    [DataField] public float ExplosionRadius = 3f;
    [DataField] public float ExplosionKnockback = 5f;
    [DataField] public EntProtoId AcidCloudPrototype = "CMUThreatBoomerAcidCloud";
    [DataField] public EntProtoId NeurotoxinCloudPrototype = "CMUThreatBoomerNeurotoxinCloud";
    [DataField] public ProtoId<ExplosionPrototype> IedExplosion = "CMUIED";
    [DataField] public float IedTotalIntensity = 80f;
    [DataField] public float IedIntensitySlope = 5f;
    [DataField] public float IedMaxIntensity = 15f;
    [DataField] public SoundSpecifier? Sound;
    public EntityUid? ActionEntity;
    public TimeSpan NextUseAt;
}

[RegisterComponent]
public sealed partial class CMUScreamerThreatComponent : Component
{
    [DataField] public float Range = 8f;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(35);
    [DataField] public TimeSpan TelegraphTime = TimeSpan.FromSeconds(1.5);
    [DataField] public int HordeCount = 8;
    [DataField] public float SpawnRadius = 4f;
    [DataField] public float HordeTargetRadius = 18f;
    [DataField]
    public List<EntProtoId> HordePrototypes = new()
    {
        "CMUThreatCommon",
        "CMUThreatCommonPolice",
    };
    [DataField] public TimeSpan EmergenceDuration = TimeSpan.FromSeconds(1.4);
    [DataField] public EntProtoId? EmergenceEffectPrototype;
    [DataField] public SoundSpecifier? Sound;
    public TimeSpan NextUseAt;
    public TimeSpan PendingUseAt;
    public EntityUid? PendingTarget;
}

[RegisterComponent]
public sealed partial class CMUJockeyThreatComponent : Component
{
    [DataField] public EntProtoId Action = "ActionCMUThreatJockeyRide";
    [DataField] public float Range = 4f;
    [DataField] public float LeapStrength = 22f;
    [DataField] public TimeSpan LeapDuration = TimeSpan.FromSeconds(0.6);
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(15);
    [DataField] public float PullSpeed = 2f;
    [DataField] public float WalkModifier = 0.7f;
    [DataField] public float SprintModifier = 0.7f;
    [DataField] public DamageSpecifier RideDamage = new();
    [DataField] public TimeSpan DamageInterval = TimeSpan.FromSeconds(1);
    [DataField] public TimeSpan MaxPinDuration = TimeSpan.FromSeconds(15);
    [DataField] public SoundSpecifier? Sound;
    public EntityUid? ActionEntity;
    public TimeSpan NextUseAt;
}

[RegisterComponent]
public sealed partial class CMUJockeyLeapingComponent : Component
{
    public TimeSpan EndsAt;
}

[RegisterComponent]
public sealed partial class CMUSpitterThreatComponent : Component
{
    [DataField] public EntProtoId Action = "ActionCMUThreatSpitterAcid";
    [DataField] public float Range = 8f;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(15);
    [DataField] public EntProtoId AcidPoolPrototype = "CMUThreatAcidPool";
    [DataField] public EntProtoId ProjectilePrototype = "CMUThreatSpitProjectile";
    [DataField] public float ProjectileSpeed = 8f;
    [DataField] public float AcidRadius = 0.45f;
    [DataField] public TimeSpan AcidDuration = TimeSpan.FromSeconds(12);
    [DataField] public DamageSpecifier AcidDamage = new();
    [DataField] public TimeSpan AcidDamageInterval = TimeSpan.FromSeconds(1);
    [DataField] public SoundSpecifier? Sound;
    public EntityUid? ActionEntity;
    public TimeSpan NextUseAt;
}

[RegisterComponent]
public sealed partial class CMUThreatAcidPoolComponent : Component
{
    public EntityUid? Source;
    public float Radius = 0.45f;
    public DamageSpecifier Damage = new();
    public TimeSpan DamageInterval;
    public TimeSpan ExpiresAt;
    public TimeSpan NextDamageAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUThreatSpitProjectileComponent : Component
{
    [AutoNetworkedField] public TimeSpan Duration;
    [AutoNetworkedField] public TimeSpan EndsAt;
    public EntityUid? Source;
    public MapCoordinates Destination;
    public EntProtoId AcidPoolPrototype = "CMUThreatAcidPool";
    public float AcidRadius;
    public TimeSpan AcidDuration;
    public DamageSpecifier AcidDamage = new();
    public TimeSpan AcidDamageInterval;
    public bool Impacted;
}

[RegisterComponent]
public sealed partial class CMUWitchThreatComponent : Component
{
    [DataField] public float IrritationThreshold = 100f;
    [DataField] public float NoticeRadius = 4f;
    [DataField] public float NoticeGainPerSecond = 15f;
    [DataField] public float DamageGain = 100f;
    [DataField] public float DecayPerSecond = 8f;
    [DataField] public SoundSpecifier? EnrageSound;
    public float Irritation;
    public bool Enraged;
    public EntityUid? Offender;
    public TimeSpan LastUpdateAt;
    public TimeSpan NextUpdateAt;
}

[RegisterComponent]
public sealed partial class CMUThreatPinComponent : Component
{
    public EntityUid Attacker;
    public CMUThreatPinKind Kind;
    public DamageSpecifier Damage = new();
    public TimeSpan DamageInterval;
    public TimeSpan EndsAt;
    public bool Expires = true;
    public TimeSpan NextDamageAt;
    public float PullSpeed;
    public float HoldDistance;
    public float BreakDistance;
    public bool ParalyzeVictim;
    public float WalkModifier = 1f;
    public float SprintModifier = 1f;
    public List<EntityUid> TongueSegments = new();
    public TimeSpan NextTongueUpdate;
    public Vector2 LastTongueSource;
    public Vector2 LastTongueTarget;
    public TimeSpan NextSteerAt;
    public Vector2 SteerDirection;
    public bool AttackerCanCollide = true;
}

[RegisterComponent]
public sealed partial class CMUThreatTongueSegmentComponent : Component
{
    public EntityUid Victim;
}

[RegisterComponent]
public sealed partial class CMUThreatBileComponent : Component
{
    public EntityUid? Source;
    public float HordeRadius;
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CMUThreatEmergingComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan Duration = TimeSpan.FromSeconds(1.4);
    [DataField, AutoNetworkedField] public TimeSpan EndsAt;
    [DataField, AutoNetworkedField] public EntityUid? Target;
}

public sealed partial class CMUSmokerTongueActionEvent : EntityTargetActionEvent;
public sealed partial class CMUBoomerVomitActionEvent : EntityTargetActionEvent;
public sealed partial class CMUJockeyRideActionEvent : EntityTargetActionEvent;
public sealed partial class CMUSpitterAcidActionEvent : WorldTargetActionEvent;
