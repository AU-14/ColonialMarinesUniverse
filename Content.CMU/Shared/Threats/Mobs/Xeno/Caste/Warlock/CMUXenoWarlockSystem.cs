using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using CMUDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;

public enum CMUXenoWarlockChannelKind : byte
{
    PsychicCrush,
    PsychicBlast,
    PsychicShield
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoWarlockComponent : Component
{
    public readonly List<EntityUid> FrozenProjectiles = new();

    public readonly List<EntityUid> PsychicCrushWarnings = new();

    public readonly List<EntityUid> PsychicShieldSegments = new();

    public TimeSpan NextPsychicCrushAt;

    public TimeSpan NextPsychicCrushPulseAt;

    public TimeSpan NextPsychicShieldAt;

    public EntityUid? PsychicBlastChannelEffect;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicBlastChannelEffectId = "CMUXenoWarlockBlastChannelEffect";

    public bool PsychicBlastChanneling;

    public EntityUid? PsychicBlastChannelParticle;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicBlastChannelParticleId = "CMUXenoWarlockBlastParticles";

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicBlastChargeDuration = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public FixedPoint2 PsychicBlastCost = 100;

    [DataField, AutoNetworkedField]
    public DamageSpecifier PsychicBlastDamage = new()
    {
        DamageDict = { ["Blunt"] = FixedPoint2.New(35) }
    };

    [DataField, AutoNetworkedField]
    public SoundSpecifier
        PsychicBlastFireSound = new SoundPathSpecifier(CMUXenoWarlockSystem.PsychicBlastFireSoundPath);

    [DataField, AutoNetworkedField]
    public SoundSpecifier PsychicBlastImpactSound
        = new SoundPathSpecifier(CMUXenoWarlockSystem.PsychicBlastImpactSoundPath);

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicBlastProjectileId = "CMUXenoPsychicBlastProjectile";

    [DataField, AutoNetworkedField]
    public float PsychicBlastProjectileSpeed = 40f;

    [DataField, AutoNetworkedField]
    public float PsychicBlastRadius = 1.25f;

    [DataField, AutoNetworkedField]
    public float PsychicBlastRange = 6f;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicBlastSlow = TimeSpan.FromSeconds(1.5);

    public EntityCoordinates PsychicBlastTarget;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushBlurId = "CMUXenoPsychicCrushBlur";

    [DataField, AutoNetworkedField]
    public SoundSpecifier PsychicCrushCancelSound
        = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/woosh_swoosh.ogg");

    public EntityUid? PsychicCrushChannelEffect;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushChannelEffectId = "CMUXenoWarlockCrushChannelEffect";

    public bool PsychicCrushChanneling;

    public EntityUid? PsychicCrushChannelParticle;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushChannelParticleId = "CMUXenoWarlockCrushParticles";

    [DataField, AutoNetworkedField]
    public float PsychicCrushChannelSpeedMultiplier = 0.9f;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicCrushCooldown = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushDetonateId = "CMUXenoPsychicCrushHard";

    public EntityUid? PsychicCrushOrb;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushOrbId = "CMUXenoPsychicCrushOrb";

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicCrushPulseInterval = TimeSpan.FromSeconds(1);

    // Stored so that an early-detonate (via TriggerPsychicCrush from the InstantAction press,
    // ContinuePsychicCrush max-radius auto-trigger, or a target-drift break) can cancel the
    // outstanding channel do-after and remove the progress bar from the warlock's HUD.
    public DoAfterId? PsychicCrushChannelDoAfter;

    public int PsychicCrushPulses;

    [DataField, AutoNetworkedField]
    public SoundSpecifier
        PsychicCrushPulseSound = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/woosh_swoosh.ogg");

    // Range at which the warlock can start channelling a new psychic crush. Matches the psychic
    // blast's initiation range (6 tiles) so both ranged abilities share the same reach at cast.
    // Distinct from PsychicCrushRange, which governs how far the target can drift from the
    // warlock during the channel before the crush breaks.
    [DataField, AutoNetworkedField]
    public float PsychicCrushInitRange = 6f;

    [DataField, AutoNetworkedField]
    public float PsychicCrushRange = CMUXenoWarlockSystem.PsychicCrushTargetRangeValue;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushSmoothId = "CMUXenoPsychicCrushSmooth";

    public EntityCoordinates PsychicCrushTarget;

    [DataField, AutoNetworkedField]
    public SoundSpecifier PsychicCrushTriggerSound = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/EMPulse.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushWarningId = "CMUXenoPsychicCrushWarning";

    public bool PsychicCrushWindingUp;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicCrushWindupDuration = TimeSpan.FromSeconds(0.8);

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicShieldBlastParalyze = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public SoundSpecifier PsychicShieldBlastSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/bamf.ogg");

    [DataField, AutoNetworkedField]
    public float PsychicShieldBlastThrowSpeed = 4f;

    public EntityUid? PsychicShieldChannelEffect;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicShieldChannelEffectId = "CMUXenoWarlockShieldChannelEffect";

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicShieldCooldown = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public FixedPoint2 PsychicShieldCost = FixedPoint2.New(CMUXenoWarlockSystem.PsychicShieldPlasmaCost);

    public Direction PsychicShieldDirection;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicShieldDuration = TimeSpan.FromSeconds(6);

    public TimeSpan PsychicShieldExpiresAt;

    [DataField, AutoNetworkedField]
    public FixedPoint2 PsychicShieldIntegrity = FixedPoint2.New(CMUXenoWarlockSystem.PsychicShieldIntegrityValue);

    public FixedPoint2 PsychicShieldIntegrityRemaining;

    [DataField, AutoNetworkedField]
    public int PsychicShieldMaxFrozenProjectiles = CMUXenoWarlockSystem.PsychicShieldMaxFrozenProjectilesValue;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicShieldMoveCancelGrace = TimeSpan.FromSeconds(0.25);

    public TimeSpan PsychicShieldMoveCancelGraceUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicShieldOwnerStun = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public SoundSpecifier PsychicShieldReflectSound = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/portal.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier
        PsychicShieldRoarSound = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/roar_warlock.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicShieldSegmentId = "CMUXenoPsychicShieldSegment";

    // Distance (world units / tiles) a reflected thrown item is sent back. Thrown items do
    // not have a projectile velocity to invert, so the reflect path uses ThrowingSystem which
    // needs a target-relative displacement vector.
    [DataField, AutoNetworkedField]
    public float PsychicShieldReflectedThrowDistance = 3f;

    // Base throw speed passed to ThrowingSystem.TryThrow on reflect. Combined with the
    // distance above this gives ~0.3 s fly time back at a marine, in line with a hand throw.
    [DataField, AutoNetworkedField]
    public float PsychicShieldReflectedThrowSpeed = 10f;

    // Minimum world-space velocity (units / second) a thrown item must be moving at to be
    // frozen by the shield. Anything slower - specifically hand-thrown items which get their
    // effective velocity halved by friction compensation in ThrowingSystem - passes through.
    // Launcher-propelled grenades keep their raw projectileSpeed (~20) and stay above this
    // threshold, so they still freeze. Only affects thrown items; ProjectileComponent-based
    // shots (bullets, rockets) are handled by the projectile freeze path and ignore this.
    [DataField, AutoNetworkedField]
    public float PsychicShieldMinimumFreezeSpeed = 15f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier PsychicShieldStartSound = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/magic.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicShieldVisualId = "CMUXenoPsychicShield";
}

[RegisterComponent, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoWarlockChannelingComponent : Component
{
    [DataField]
    public float SpeedMultiplier = 0.3f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoPsychicShieldSegmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public Direction Direction;

    [DataField, AutoNetworkedField]
    public EntityUid Warlock;
}

[RegisterComponent, NetworkedComponent, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoPsychicShieldRootComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoFrozenProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public BodyStatus BodyStatus;

    [DataField, AutoNetworkedField]
    public BodyType BodyType;

    [DataField, AutoNetworkedField]
    public bool CanCollide;

    [DataField, AutoNetworkedField]
    public bool DeleteOnCollide;

    [DataField, AutoNetworkedField]
    public bool FixedDistanceArcProj;

    [DataField, AutoNetworkedField]
    public TimeSpan FixedDistanceRemaining;

    [DataField, AutoNetworkedField]
    public MapCoordinates? FixedDistanceTargetCoordinates;

    [DataField, AutoNetworkedField]
    public bool HadDeleteOnCollideComponent;

    [DataField, AutoNetworkedField]
    public bool HadDeleteOnFixedDistanceStopComponent;

    [DataField, AutoNetworkedField]
    public bool HadProjectileFixedDistanceComponent;

    [DataField, AutoNetworkedField]
    public bool IgnoreShooter;

    [DataField, AutoNetworkedField]
    public bool ProjectileSpent;

    // When true, BeforeTriggerEvent is NOT cancelled while the entity is frozen. Used for
    // thrown grenades so their fuse timer still expires normally (if the warlock does not
    // reflect in time, the grenade detonates at the shield face). Rockets/other collision-
    // triggered projectiles keep this false so they cannot self-detonate on shield contact.
    [DataField, AutoNetworkedField]
    public bool AllowTriggerWhileFrozen;

    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;

    [DataField, AutoNetworkedField]
    public Vector2 Velocity;

    [DataField, AutoNetworkedField]
    public EntityUid? Weapon;
}

public sealed partial class CMUXenoPsychicBlastActionEvent : WorldTargetActionEvent;
public sealed partial class CMUXenoPsychicCrushActionEvent : WorldTargetActionEvent;
public sealed partial class CMUXenoPsychicShieldActionEvent : WorldTargetActionEvent;

/// <summary>
/// Instant-action event fired when the shield is up and the player presses the shield
/// button again. The shield action's components are swapped to InstantAction while up
/// so the press triggers this directly, no target mode.
/// </summary>
public sealed partial class CMUXenoPsychicShieldDetonateActionEvent : InstantActionEvent;

/// <summary>
/// Marker placed on the shield action prototype so we can find it via the warlock's
/// ActionsComponent and swap its target/instant components on the fly.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUXenoPsychicShieldActionMarkerComponent : Component;

/// <summary>
/// Instant-action event fired while the crush is channelling and the player presses the crush
/// button again. Same swap pattern as the shield: WorldTargetAction is swapped out for
/// InstantAction so the press early-detonates immediately without a second target click.
/// </summary>
public sealed partial class CMUXenoPsychicCrushDetonateActionEvent : InstantActionEvent;

/// <summary>
/// Marker placed on the crush action prototype so the swap function can locate it via the
/// warlock's ActionsComponent, mirroring the shield's marker.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUXenoPsychicCrushActionMarkerComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUXenoPsychicCrushDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates TargetCoordinates;

    public CMUXenoPsychicCrushDoAfterEvent(NetCoordinates targetCoordinates) => TargetCoordinates = targetCoordinates;

    public override DoAfterEvent Clone() => new CMUXenoPsychicCrushDoAfterEvent(TargetCoordinates);
}

[Serializable, NetSerializable]
public sealed partial class CMUXenoPsychicCrushChannelDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new CMUXenoPsychicCrushChannelDoAfterEvent();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoPsychicCrushBlurComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float Radius = 0.55f;

    [DataField, AutoNetworkedField]
    public float Strength = 1.6f;
}

[Serializable, NetSerializable]
public sealed partial class CMUXenoPsychicBlastDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates TargetCoordinates;

    public CMUXenoPsychicBlastDoAfterEvent(NetCoordinates targetCoordinates)
    {
        TargetCoordinates = targetCoordinates;
    }

    public override DoAfterEvent Clone() => new CMUXenoPsychicBlastDoAfterEvent(TargetCoordinates);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoPsychicBlastProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Blunt"] = FixedPoint2.New(35) }
    };

    [DataField, AutoNetworkedField]
    public EntProtoId ImpactEffectId = "CMUXenoPsychicBlastShockwave";

    [DataField, AutoNetworkedField]
    public SoundSpecifier ImpactSound = new SoundPathSpecifier(CMUXenoWarlockSystem.PsychicBlastImpactSoundPath);

    [DataField, AutoNetworkedField]
    public float KnockbackSpeed = CMUXenoWarlockSystem.PsychicBlastKnockbackSpeed;

    // Maximum knockback throw distance in tiles for a victim sitting at the exact impact tile.
    // Actual throw distance for a victim scales linearly down to KnockbackMinDistance at the edge
    // of the blast radius so nearest targets fly the furthest, matching TGMC's `3 - victim_dist`
    // knockback formula in psy_blast/drop_nade.
    [DataField, AutoNetworkedField]
    public float MaxKnockback = 3f;

    // Floor on the knockback throw distance so a victim at the edge of the blast still moves a
    // full tile out of the impact area instead of a hair.
    [DataField, AutoNetworkedField]
    public float KnockbackMinDistance = 1f;

    [DataField, AutoNetworkedField]
    public float Radius = 1.25f;

    [DataField, AutoNetworkedField]
    public TimeSpan Slow = TimeSpan.FromSeconds(1.5);

    // Paralyze applied to victims caught in the AoE on the same tick they get flung. Mirrors the
    // crusher charge's StunTime + TryParalyze pattern in ProcessChargeHit.
    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(1.5);

    public bool Triggered;
}

// Marker component attached to every mob flung by the psychic blast. Mirrors ChargeFlungComponent
// from Content.Shared._RMC14.Xenonids.Charge: while the marker is present, the mob's throw path
// generates ThrowDoHitEvent callbacks so it can bowl into other mobs, damaging and knocking down
// whoever it collides with. Removed via StopThrowEvent when the flight ends. Serialised so both
// client and server can react (client for prediction, server for authoritative damage).
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoPsychicBlastFlungComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier CollisionDamage = new()
    {
        DamageDict = { ["Blunt"] = FixedPoint2.New(30) }
    };

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(1.5);

    // Warlock that fired the blast. Used by OnBlastFlungHit to skip the shooter and same-hive
    // xenos when the flung mob crashes into another entity. May be null when the projectile has
    // no recorded shooter (e.g. admin spawn).
    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;
}

public sealed partial class CMUXenoWarlockSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private RMCDazedSystem _daze = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private ThrownItemSystem _thrownItem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private XenoProjectileSystem _xenoProjectile = default!;
    private static readonly FixedPoint2 PsychicCrushVehicleDamageMultiplier = FixedPoint2.New(0.5f);
    private static readonly FixedPoint2 PsychicCrushMechDamageMultiplier = FixedPoint2.New(2.3f);
    public const string PsychicBlastFireSoundPath = "/Audio/_CMU14/Xeno/Warlock/volkite_4.ogg";
    public const string PsychicBlastImpactSoundPath = "/Audio/_CMU14/Xeno/Warlock/EMPulse.ogg";
    public const int PsychicCrushBaseDamage = 25;
    public const int PsychicCrushDamagePerPulse = 15;
    public const int PsychicCrushMaxAreaRadius = 3;
    public const int PsychicCrushMaxPulses = 5;
    public const int PsychicCrushPlasmaPerPulse = 40;
    public const float PsychicCrushTargetRangeValue = 9f;
    // At or above this pulse count, the crush also forces a short Paralyze on affected mobs,
    // mirroring TGMC's stamina-exhaustion knockout that only triggers on max-charge hits.
    public const int PsychicCrushHighPulseParalyzeThreshold = 4;
    // Base throw speed (world units / second) passed to ThrowingSystem.TryThrow for the blast's
    // knockback. Only affects flight time and initial impulse magnitude; the actual travel
    // distance is fixed by the compensated displacement vector passed alongside it.
    public const float PsychicBlastKnockbackSpeed = 15f;
    public const int PsychicShieldIntegrityValue = 2000;
    // 0 disables the hard cap - ShouldPsychicShieldBreakFromFrozenProjectiles returns false
    // when max <= 0, so the shield only breaks from integrity or from being interrupted.
    public const int PsychicShieldMaxFrozenProjectilesValue = 0;
    // Paid once when the shield goes up. Detonating it afterwards is free, so the whole
    // raise-and-reflect cycle costs this much and nothing more.
    public const int PsychicShieldPlasmaCost = 300;
    public const int PsychicShieldDetonationPlasmaCost = 0;
    public const float PsychicShieldHalfThickness = 0.5f;
    public const float PsychicShieldHalfWidth = 1.5f;
    public const float PsychicShieldProjectileStopOffset = 0.1f;
    // Small padding added to the tick sweep's oriented-rectangle test so a fast grenade that
    // just barely overshoots the shield face between ticks still gets caught.
    public const float ShieldSweepMargin = 0.15f;

    private const float WarlockDirectedParticleVelocity = 7f;
    // Mirrors Robust.Client.Graphics.EyeManager.PixelsPerMeter (client-only). Used by shared code
    // that has to convert a world-space distance into the pixel-space unit the particle overlay
    // draws in.
    private const float WarlockParticlePixelsPerMeter = 32f;
    private readonly HashSet<EntityUid> _affected = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicBlastActionEvent>(OnPsychicBlastAction);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicBlastDoAfterEvent>(OnPsychicBlastDoAfter);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicCrushActionEvent>(OnPsychicCrushAction);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicCrushDoAfterEvent>(OnPsychicCrushDoAfter);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicCrushChannelDoAfterEvent>(
            OnPsychicCrushChannelDoAfter);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicShieldActionEvent>(OnPsychicShieldAction);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicShieldDetonateActionEvent>(OnPsychicShieldDetonate);
        SubscribeLocalEvent<CMUXenoWarlockComponent, CMUXenoPsychicCrushDetonateActionEvent>(OnPsychicCrushDetonate);
        SubscribeLocalEvent<CMUXenoWarlockComponent, MoveEvent>(OnWarlockMove);
        SubscribeLocalEvent<CMUXenoWarlockComponent, StunnedEvent>(OnWarlockStunned);
        SubscribeLocalEvent<CMUXenoWarlockComponent, KnockedDownEvent>(OnWarlockKnockedDown);
        SubscribeLocalEvent<CMUXenoWarlockComponent, MobStateChangedEvent>(OnWarlockMobStateChanged);
        SubscribeLocalEvent<CMUXenoWarlockComponent, RejuvenateEvent>(OnWarlockRejuvenate);
        SubscribeLocalEvent<CMUXenoWarlockComponent, ThrownEvent>(OnWarlockThrown);

        // Re-initialize the Event field of swapped shield action components. The Event field is
        // NonSerialized, so after a client receives a networked add of InstantAction (or
        // WorldTargetAction) it will have Event = null. That breaks client-side prediction of
        // the action, which in turn breaks PlayPredicted (server-side broadcast excludes the
        // acting client on the assumption it predicted the sound locally). Setting Event here
        // ensures the swap works correctly on both sides for both predicted execution and audio.
        SubscribeLocalEvent<InstantActionComponent, ComponentStartup>(OnAnyInstantActionStartup);
        SubscribeLocalEvent<WorldTargetActionComponent, ComponentStartup>(OnAnyWorldTargetActionStartup);

        SubscribeLocalEvent<CMUXenoWarlockChannelingComponent, RefreshMovementSpeedModifiersEvent>(
            OnChannelingRefreshSpeed);
        SubscribeLocalEvent<CMUXenoPsychicShieldRootComponent, RefreshMovementSpeedModifiersEvent>(
            OnPsychicShieldRootRefreshSpeed);
        SubscribeLocalEvent<CMUXenoPsychicShieldRootComponent, AttemptMobCollideEvent>(
            OnPsychicShieldRootAttemptMobCollide);
        SubscribeLocalEvent<CMUXenoPsychicBlastProjectileComponent, ProjectileHitEvent>(OnPsychicBlastProjectileHit);
        SubscribeLocalEvent<CMUXenoPsychicBlastProjectileComponent, ProjectileFixedDistanceStopEvent>(
            OnPsychicBlastProjectileFixedDistanceStop);
        SubscribeLocalEvent<CMUXenoPsychicBlastProjectileComponent, PreventCollideEvent>(
            OnPsychicBlastProjectilePreventCollide);
        SubscribeLocalEvent<CMUXenoPsychicBlastFlungComponent, ThrowDoHitEvent>(OnBlastFlungHit);
        SubscribeLocalEvent<CMUXenoPsychicBlastFlungComponent, StopThrowEvent>(OnBlastFlungStop);
        SubscribeLocalEvent<CMUXenoPsychicShieldSegmentComponent, PreventCollideEvent>(
            OnShieldProjectilePreventCollide);
        SubscribeLocalEvent<CMUXenoPsychicShieldSegmentComponent, ProjectileReflectAttemptEvent>(
            OnShieldProjectileReflectAttempt);
        SubscribeLocalEvent<CMUXenoPsychicShieldSegmentComponent, ThrowHitByEvent>(
            OnShieldThrowHitBy);
        SubscribeLocalEvent<CMUXenoFrozenProjectileComponent, MapInitEvent>(OnFrozenProjectileInit);
        SubscribeLocalEvent<CMUXenoFrozenProjectileComponent, ComponentAdd>(OnFrozenProjectileInit);
    }

    public override void Update(float frameTime)
    {
        TimeSpan time = _timing.CurTime;

        if (_net.IsClient)
        {
            EntityQueryEnumerator<CMUXenoFrozenProjectileComponent, PhysicsComponent> frozenQuery
                = EntityQueryEnumerator<CMUXenoFrozenProjectileComponent, PhysicsComponent>();
            while (frozenQuery.MoveNext(out EntityUid uid, out _, out PhysicsComponent? physics))
            {
                if (physics.BodyType != BodyType.Static) _physics.SetBodyType(uid, BodyType.Static, body: physics);
            }
        }

        EntityQueryEnumerator<CMUXenoWarlockComponent> query = EntityQueryEnumerator<CMUXenoWarlockComponent>();
        while (query.MoveNext(out EntityUid uid, out CMUXenoWarlockComponent? warlock))
        {
            Entity<CMUXenoWarlockComponent> ent = (uid, warlock);
            if (warlock.PsychicCrushChanneling && time >= warlock.NextPsychicCrushPulseAt)
                ContinuePsychicCrush(ent);

            // Server-authoritative broadphase sweep for thrown items that overlap a shield
            // segment. ThrowHitByEvent covers the standard case, but launcher-fired grenades
            // (M85A1 shooting a CMGrenadeHighExplosive) go through Gun.ShootOrThrow's TryThrow
            // fallback and can slip past the collision event chain if their throw-fixture is
            // not created in time or their layer masks do not match the shield fixture. This
            // sweep is a one-tick-late safety net that catches anything intersecting the shield
            // bounds and hands it to the same freeze pipeline.
            if (!_net.IsClient && warlock.PsychicShieldSegments.Count > 0)
                TryFreezeThrownItemsInShieldArea(ent);

            // General disruption check. StunnedComponent, KnockedDownComponent, and
            // ThrownItemComponent are the marker components any stun/knockdown/knockback path
            // adds to the target, regardless of source (grenade blast, xeno lunge, admin verb,
            // ...). Reading them per tick is a source-agnostic signal that the warlock is under
            // disruption. This runs in addition to the event handlers so the shield still ends
            // even when the upstream event chain skips (server-only event that never fires on
            // the client, ordering ambiguity, etc). "Break-style" end path: no reflect, no owner
            // stun - same behaviour as OnWarlockStunned / OnWarlockKnockedDown / OnWarlockThrown.
            if (warlock.PsychicShieldSegments.Count > 0
                && (HasComp<StunnedComponent>(uid)
                    || HasComp<KnockedDownComponent>(uid)
                    || HasComp<ThrownItemComponent>(uid)))
            {
                EndPsychicShield(ent, false, false);
                continue;
            }

            // Natural expiry auto-detonates: same reflect + blast + roar + reflect sound as a
            // manual button press. Detonation is free, so expiry always reflects and no plasma
            // check can strand the caught projectiles. Disruption paths (stun, knockdown, throw,
            // mob-state, rejuvenate) still call EndPsychicShield with reflectProjectiles: false
            // so the drop-and-release behaviour is preserved there.
            if (warlock.PsychicShieldSegments.Count > 0 && time >= warlock.PsychicShieldExpiresAt)
                DetonatePsychicShield(ent);
        }
    }

    private void TryFreezeThrownItemsInShieldArea(Entity<CMUXenoWarlockComponent> warlock)
    {
        foreach (EntityUid segmentUid in warlock.Comp.PsychicShieldSegments)
        {
            if (!TryComp<CMUXenoPsychicShieldSegmentComponent>(segmentUid, out var segmentComp))
                continue;

            MapCoordinates shieldMap = _transform.GetMapCoordinates(segmentUid);

            // Shield footprint is an oriented rectangle: HalfWidth along the perpendicular axis,
            // HalfThickness along the shield normal. Circular query has to reach the far corner
            // of that rectangle for fast items entering diagonally.
            Vector2 shieldNormal = segmentComp.Direction.ToVec();
            Vector2 shieldPerpAxis = new(-shieldNormal.Y, shieldNormal.X);
            float queryRadius = MathF.Sqrt(PsychicShieldHalfWidth * PsychicShieldHalfWidth
                + PsychicShieldHalfThickness * PsychicShieldHalfThickness) + ShieldSweepMargin;

            HashSet<Entity<ThrownItemComponent>> candidates = new();
            _lookup.GetEntitiesInRange(shieldMap, queryRadius, candidates);

            foreach (Entity<ThrownItemComponent> candidate in candidates)
            {
                if (HasComp<CMUXenoFrozenProjectileComponent>(candidate.Owner))
                    continue;

                if (!TryComp<PhysicsComponent>(candidate.Owner, out var physics))
                    continue;

                // Restrict the freeze zone to the actual shield rectangle plus a small margin,
                // instead of the whole circular query. Prevents grenades from freezing 1-2 tiles
                // in front of the shield face - they should only stop when they reach the shield.
                MapCoordinates candMap = _transform.GetMapCoordinates(candidate.Owner);
                if (candMap.MapId != shieldMap.MapId)
                    continue;

                Vector2 relPos = candMap.Position - shieldMap.Position;
                float normalDist = Vector2.Dot(relPos, shieldNormal);
                float perpDist = Vector2.Dot(relPos, shieldPerpAxis);
                if (MathF.Abs(normalDist) > PsychicShieldHalfThickness + ShieldSweepMargin)
                    continue;
                if (MathF.Abs(perpDist) > PsychicShieldHalfWidth)
                    continue;

                TryFreezeShieldThrownItem((segmentUid, segmentComp), candidate.Owner, candidate.Comp, physics);
            }
        }
    }

    private void OnFrozenProjectileInit(Entity<CMUXenoFrozenProjectileComponent> frozen, ref MapInitEvent args)
    {
        EnsureFrozen(frozen);
    }

    private void OnFrozenProjectileInit(Entity<CMUXenoFrozenProjectileComponent> frozen, ref ComponentAdd args)
    {
        EnsureFrozen(frozen);
    }

    private void EnsureFrozen(Entity<CMUXenoFrozenProjectileComponent> frozen)
    {
        if (TryComp(frozen, out PhysicsComponent? physics))
        {
            _physics.SetBodyType(frozen, BodyType.Static, body: physics);
            _physics.SetLinearVelocity(frozen, Vector2.Zero, body: physics);
            _physics.SetCanCollide(frozen, false, body: physics);
        }

        if (TryComp(frozen, out ProjectileComponent? projectile))
        {
            projectile.DeleteOnCollide = false;
            projectile.ProjectileSpent = false;
            Dirty(frozen, projectile);
        }

        RemCompDeferred<DeleteOnCollideComponent>(frozen);
        RemCompDeferred<ProjectileFixedDistanceComponent>(frozen);
    }

    private void OnPsychicBlastAction(Entity<CMUXenoWarlockComponent> warlock, ref CMUXenoPsychicBlastActionEvent args)
    {
        if (args.Handled || warlock.Comp.PsychicBlastChanneling)
            return;

        // The action's TargetActionComponent gate is intentionally looser than the ability's real
        // range so any click landing on a tile the overlay highlighted reaches this handler. Snap
        // the click to that tile's centre and enforce the true 7-tile initiation reach via
        // closest-point-of-tile - matches what the overlay highlights, so every highlighted tile
        // is usable. Out-of-range clicks are silently ignored to keep parity with the vanilla
        // action gate (no popup on plasma-preserving misses).
        if (!TrySnapAbilityTargetToTile(warlock, args.Target, warlock.Comp.PsychicBlastRange, out var target))
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null), warlock.Comp.PsychicBlastCost))
            return;

        args.Handled = true;
        warlock.Comp.PsychicBlastChanneling = true;
        warlock.Comp.PsychicBlastTarget = target;
        StartWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicBlast);
        StartWarlockChannelParticles(warlock, CMUXenoWarlockChannelKind.PsychicBlast, target);
        SetActionToggled<CMUXenoPsychicBlastActionEvent>(warlock, true);

        var ev = new CMUXenoPsychicBlastDoAfterEvent(GetNetCoordinates(target));
        var doAfter = new DoAfterArgs(EntityManager, warlock, warlock.Comp.PsychicBlastChargeDuration, ev, warlock,
            args.Action)
        {
            BreakOnMove = true,
            RootEntity = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            StopPsychicBlastChannel(warlock);
    }

    private void OnPsychicBlastDoAfter(Entity<CMUXenoWarlockComponent> warlock,
        ref CMUXenoPsychicBlastDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StopPsychicBlastChannel(warlock);

        if (args.Cancelled)
            return;

        EntityCoordinates target = GetCoordinates(args.TargetCoordinates);
        FirePsychicBlastProjectile(warlock, target);
    }

    private void FirePsychicBlastProjectile(Entity<CMUXenoWarlockComponent> warlock,
        EntityCoordinates target)
    {
        MapCoordinates origin = _transform.GetMapCoordinates(warlock);
        var targetMap = _transform.ToMapCoordinates(target);
        if (origin.MapId != targetMap.MapId)
            return;

        Vector2 direction = targetMap.Position - origin.Position;
        if (direction.LengthSquared() <= 0f)
            return;

        float distance = Math.Min(direction.Length(), warlock.Comp.PsychicBlastRange);

        bool shot = _xenoProjectile.TryShoot(warlock,
            target,
            FixedPoint2.Zero,
            warlock.Comp.PsychicBlastProjectileId,
            null,
            1,
            Angle.Zero,
            warlock.Comp.PsychicBlastProjectileSpeed,
            distance,
            predicted: false,
            stopAtTarget: true);

        if (shot)
            _audio.PlayPvs(warlock.Comp.PsychicBlastFireSound, warlock);
    }

    private void StopPsychicBlastChannel(Entity<CMUXenoWarlockComponent> warlock)
    {
        if (!warlock.Comp.PsychicBlastChanneling)
            return;

        warlock.Comp.PsychicBlastChanneling = false;
        StopWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicBlast);
        StopWarlockChannelParticles(warlock, CMUXenoWarlockChannelKind.PsychicBlast);
        SetActionToggled<CMUXenoPsychicBlastActionEvent>(warlock, false);
    }

    private void OnPsychicBlastProjectileHit(Entity<CMUXenoPsychicBlastProjectileComponent> projectile,
        ref ProjectileHitEvent args)
    {
        EntityCoordinates coords = Transform(args.Target).Coordinates;
        TryTriggerPsychicBlastProjectile(projectile, coords, args.Shooter);
    }

    private void OnPsychicBlastProjectileFixedDistanceStop(Entity<CMUXenoPsychicBlastProjectileComponent> projectile,
        ref ProjectileFixedDistanceStopEvent args)
    {
        if (_net.IsClient && !IsClientSide(projectile))
            return;

        TryTriggerPsychicBlastProjectile(projectile, Transform(projectile).Coordinates, null);
        if (CMUXenoWarlockSystem.ShouldDeletePsychicBlastProjectileOnFixedDistanceStop(_net.IsClient,
            IsClientSide(projectile)))
            QueueDel(projectile);
    }

    private void OnPsychicBlastProjectilePreventCollide(Entity<CMUXenoPsychicBlastProjectileComponent> projectile,
        ref PreventCollideEvent args)
    {
        if (CMUXenoWarlockSystem.ShouldPsychicBlastIgnoreCollisionLayer(args.OtherFixture.CollisionLayer)
            || CMUXenoWarlockSystem.ShouldPsychicBlastIgnoreCollisionLayer(args.OtherBody.CollisionLayer))
            args.Cancelled = true;
    }

    private void TryTriggerPsychicBlastProjectile(Entity<CMUXenoPsychicBlastProjectileComponent> projectile,
        EntityCoordinates coords,
        EntityUid? shooter)
    {
        if (_net.IsClient && !IsClientSide(projectile))
            return;

        if (projectile.Comp.Triggered)
            return;

        projectile.Comp.Triggered = true;
        Dirty(projectile);

        if (_net.IsClient)
            return;

        if (shooter == null && TryComp(projectile, out ProjectileComponent? projectileComp))
            shooter = projectileComp.Shooter;

        _audio.PlayPvs(projectile.Comp.ImpactSound, coords);
        Spawn(projectile.Comp.ImpactEffectId, coords);
        var mapCoords = _transform.ToMapCoordinates(coords);
        Vector2 projectileVelocity = Vector2.Zero;
        if (TryComp(projectile, out PhysicsComponent? projectilePhysics))
            projectileVelocity = _physics.GetMapLinearVelocity(projectile, projectilePhysics);

        _affected.Clear();
        foreach ((EntityUid target, MobStateComponent state) in _lookup.GetEntitiesInRange<MobStateComponent>(mapCoords,
            projectile.Comp.Radius))
        {
            if (target == shooter
                || !_affected.Add(target)
                || _mobState.IsDead(target, state)
                || (shooter != null && !_xeno.CanAbilityAttackTarget(shooter.Value, target)))
                continue;

            _damageable.TryChangeDamage(target, projectile.Comp.Damage, origin: shooter, tool: projectile);
            _slow.TrySlowdown(target, projectile.Comp.Slow);
            // Primary-target knockdown. Matches the crusher charge's ProcessChargeHit calling
            // TryParalyze before the throw so the mob is on the ground when the flight resolves,
            // rather than trying to walk off mid-fling.
            _stun.TryParalyze(target, projectile.Comp.StunTime, true);

            // Outward unit vector from impact through the victim; falls back to the projectile's
            // travel direction when the victim happens to sit exactly on the impact tile.
            Vector2 targetPos = _transform.GetMapCoordinates(target).Position;
            Vector2 outward = CMUXenoWarlockSystem.GetPsychicBlastKnockbackDirection(mapCoords.Position,
                targetPos,
                projectileVelocity);
            if (outward == Vector2.Zero)
                continue;

            // TGMC parity: throw distance = MaxKnockback - distance_from_impact, floored at
            // KnockbackMinDistance. Closer victims fly further; edge victims still clear the AoE.
            // The vector fed to TryThrow is the full displacement in tiles - with
            // compensateFriction: true the mob decelerates to a stop at exactly that offset from
            // its current position, unlike the previous normalized (magnitude 1) vector which
            // only ever nudged them a single tile.
            float distFromImpact = (targetPos - mapCoords.Position).Length();
            float throwDistance = Math.Max(projectile.Comp.KnockbackMinDistance,
                projectile.Comp.MaxKnockback - distFromImpact);
            Vector2 displacement = outward * throwDistance;

            // Zero the victim's velocity before the throw so an already-moving mob does not
            // arrive short (existing walk momentum against the outward vector) or overshoot
            // (walk momentum along it). Matches the ProcessChargeHit pattern.
            if (TryComp(target, out PhysicsComponent? targetPhysics))
            {
                _physics.SetLinearVelocity(target, Vector2.Zero, body: targetPhysics);
                _physics.SetAngularVelocity(target, 0f, body: targetPhysics);
            }

            // Marker component turns the throw into a "bowl through mobs" charge: while it is
            // present, ThrowDoHitEvent callbacks apply collision damage / paralyze / slowdown to
            // any non-shooter, non-hivemate mob the flung target crashes into. Removed on
            // StopThrowEvent when the flight ends. Storing the shooter lets us reject the warlock
            // themselves and same-hive xenos from the collision handler.
            var flung = EnsureComp<CMUXenoPsychicBlastFlungComponent>(target);
            flung.Shooter = shooter;
            Dirty(target, flung);

            _throwing.TryThrow(target, displacement, projectile.Comp.KnockbackSpeed, shooter, animated: false,
                playSound: false, compensateFriction: true);
        }
    }

    // Fires while a psychic-blast-flung mob is mid-throw and physically collides with a hard
    // entity. Mirrors ChargeFlungComponent's OnChargeFlungHit in XenoChargeSystem: damage,
    // paralyze, and slowdown the entity we crashed into, then keep flying. Xeno hivemates of the
    // shooter and the warlock themselves are skipped so a marine flung into the warlock or into
    // a friendly xeno does no friendly-fire damage.
    private void OnBlastFlungHit(Entity<CMUXenoPsychicBlastFlungComponent> ent, ref ThrowDoHitEvent args)
    {
        EntityUid target = args.Target;

        if (!HasComp<MobStateComponent>(target))
            return;

        if (_mobState.IsDead(target))
            return;

        if (ent.Comp.Shooter is { } shooter)
        {
            if (target == shooter)
                return;
            if (!_xeno.CanAbilityAttackTarget(shooter, target))
                return;
        }

        _damageable.TryChangeDamage(target, ent.Comp.CollisionDamage, origin: ent.Owner);
        _stun.TryParalyze(target, ent.Comp.KnockdownTime, true);
        _slow.TrySlowdown(target, ent.Comp.SlowTime);

        if (_net.IsServer)
            _audio.PlayPvs(new SoundCollectionSpecifier("Punch"), target);
        // Fall-through by design: the flight is not stopped, so the flung mob keeps bowling into
        // whoever else is in its path until it hits a wall or the throw completes.
    }

    private void OnBlastFlungStop(Entity<CMUXenoPsychicBlastFlungComponent> ent, ref StopThrowEvent args)
    {
        RemCompDeferred<CMUXenoPsychicBlastFlungComponent>(ent);
    }

    private void OnPsychicCrushAction(Entity<CMUXenoWarlockComponent> warlock, ref CMUXenoPsychicCrushActionEvent args)
    {
        if (args.Handled)
            return;

        if (warlock.Comp.PsychicCrushChanneling)
        {
            args.Handled = true;
            if (CMUXenoWarlockSystem.CanTriggerPsychicCrush(warlock.Comp.PsychicCrushPulses))
                TriggerPsychicCrush(warlock);

            return;
        }

        if (warlock.Comp.PsychicCrushWindingUp)
        {
            args.Handled = true;
            return;
        }

        if (_timing.CurTime < warlock.Comp.NextPsychicCrushAt)
            return;

        // Same tile-snap pattern as OnPsychicBlastAction. Enforces the 7-tile initiation reach
        // via closest-point-of-tile against PsychicCrushInitRange so every highlighted tile is
        // a legal cast, and hands the snapped tile-centre downstream so the crush warning and
        // orb spawn on that tile. Keep-alive during the channel still uses the larger drift
        // range via CanKeepPsychicCrushTarget on the snapped target.
        if (!TrySnapAbilityTargetToTile(warlock, args.Target, warlock.Comp.PsychicCrushInitRange, out var target)
            || !CanKeepPsychicCrushTarget(warlock, target))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-warlock-psychic-crush-invalid-target"), warlock, warlock,
                PopupType.SmallCaution);
            return;
        }

        StartPsychicCrushWindup(warlock, target, args.Action);
        args.Handled = true;
    }

    private void StartPsychicCrushWindup(Entity<CMUXenoWarlockComponent> warlock,
        EntityCoordinates target,
        EntityUid? action)
    {
        warlock.Comp.PsychicCrushWindingUp = true;
        warlock.Comp.PsychicCrushTarget = target;

        var channeling = EnsureComp<CMUXenoWarlockChannelingComponent>(warlock);
        channeling.SpeedMultiplier = 0f;
        _movement.RefreshMovementSpeedModifiers(warlock);

        var ev = new CMUXenoPsychicCrushDoAfterEvent(GetNetCoordinates(target));
        var doAfter = new DoAfterArgs(EntityManager, warlock, warlock.Comp.PsychicCrushWindupDuration, ev, warlock,
            action)
        {
            BreakOnMove = true,
            RootEntity = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            StopPsychicCrushWindup(warlock);
    }

    private void OnPsychicCrushDoAfter(Entity<CMUXenoWarlockComponent> warlock,
        ref CMUXenoPsychicCrushDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!warlock.Comp.PsychicCrushWindingUp)
            return;

        warlock.Comp.PsychicCrushWindingUp = false;

        if (args.Cancelled)
        {
            RemovePsychicCrushMovementModifier(warlock);
            return;
        }

        EntityCoordinates target = GetCoordinates(args.TargetCoordinates);
        if (!CanKeepPsychicCrushTarget(warlock, target))
        {
            RemovePsychicCrushMovementModifier(warlock);
            return;
        }

        StartPsychicCrush(warlock, target);
    }

    private void OnPsychicCrushChannelDoAfter(Entity<CMUXenoWarlockComponent> warlock,
        ref CMUXenoPsychicCrushChannelDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!warlock.Comp.PsychicCrushChanneling)
            return;

        if (!args.Cancelled)
            warlock.Comp.PsychicCrushPulses = PsychicCrushMaxPulses;

        ResolvePsychicCrush(warlock, true, true);
    }

    private void OnPsychicShieldAction(Entity<CMUXenoWarlockComponent> warlock,
        ref CMUXenoPsychicShieldActionEvent args)
    {
        if (args.Handled)
            return;

        // Shield-up detonation is normally dispatched via CMUXenoPsychicShieldDetonateActionEvent
        // after SetPsychicShieldActionMode swaps the action to InstantAction in StartPsychicShield.
        // If the raise event still fires while the shield is up (action-swap not yet networked,
        // integration tests raising the event directly, or an admin using the raw verb) treat the
        // press as a detonate so the input is never eaten silently. Free, like the dedicated
        // detonate handler; identical DetonatePsychicShield call site.
        if (warlock.Comp.PsychicShieldSegments.Count > 0)
        {
            DetonatePsychicShield(warlock);
            args.Handled = true;
            return;
        }

        if (_timing.CurTime < warlock.Comp.NextPsychicShieldAt)
            return;

        Direction direction = GetPsychicShieldDirectionFromTarget(warlock, args.Target);

        if (!CanStartPsychicShield(warlock, direction))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-warlock-psychic-shield-obstructed"),
                warlock,
                warlock,
                PopupType.SmallCaution);
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null), warlock.Comp.PsychicShieldCost))
            return;

        StartPsychicShield(warlock, direction);
        args.Handled = true;
    }

    private void OnPsychicShieldDetonate(Entity<CMUXenoWarlockComponent> warlock,
        ref CMUXenoPsychicShieldDetonateActionEvent args)
    {
        if (args.Handled)
            return;
        if (warlock.Comp.PsychicShieldSegments.Count == 0)
            return;

        // Detonating costs nothing; the whole cycle is paid for when the shield goes up.
        DetonatePsychicShield(warlock);
        args.Handled = true;
    }

    // Runs on both client and server whenever an InstantActionComponent finishes starting up
    // (whether from local EnsureComp or from a network add). Filters by the shield-action marker
    // and re-establishes the Event field, which is NonSerialized and would otherwise be null on
    // the client after network reconciliation.
    private void OnAnyInstantActionStartup(Entity<InstantActionComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<CMUXenoPsychicShieldActionMarkerComponent>(ent))
        {
            if (ent.Comp.Event == null)
                ent.Comp.Event = new CMUXenoPsychicShieldDetonateActionEvent();
            return;
        }

        if (HasComp<CMUXenoPsychicCrushActionMarkerComponent>(ent))
        {
            if (ent.Comp.Event == null)
                ent.Comp.Event = new CMUXenoPsychicCrushDetonateActionEvent();
        }
    }

    // Same idea as OnAnyInstantActionStartup, but for the WorldTargetAction state (both actions'
    // default idle mode). Restores the correct raise event so the next press predicts locally.
    private void OnAnyWorldTargetActionStartup(Entity<WorldTargetActionComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<CMUXenoPsychicShieldActionMarkerComponent>(ent))
        {
            if (ent.Comp.Event == null)
                ent.Comp.Event = new CMUXenoPsychicShieldActionEvent();
            return;
        }

        if (HasComp<CMUXenoPsychicCrushActionMarkerComponent>(ent))
        {
            if (ent.Comp.Event == null)
                ent.Comp.Event = new CMUXenoPsychicCrushActionEvent();
        }
    }

    // Detonate handler for the crush's instant-action state. Triggered when the swap is active
    // (during channel) and the warlock presses the crush button. Same gate as the WorldTarget
    // path — only detonates if enough pulses have completed.
    private void OnPsychicCrushDetonate(Entity<CMUXenoWarlockComponent> warlock,
        ref CMUXenoPsychicCrushDetonateActionEvent args)
    {
        if (args.Handled)
            return;

        if (!warlock.Comp.PsychicCrushChanneling)
            return;

        if (!CMUXenoWarlockSystem.CanTriggerPsychicCrush(warlock.Comp.PsychicCrushPulses))
            return;

        TriggerPsychicCrush(warlock);
        args.Handled = true;
    }

    // Swaps the crush action's components so pressing it while channeling early-detonates
    // immediately instead of entering target mode. Mirrors SetPsychicShieldActionMode.
    private void SetPsychicCrushActionMode(EntityUid warlock, bool channeling)
    {
        var query = EntityQueryEnumerator<CMUXenoPsychicCrushActionMarkerComponent, ActionComponent>();
        while (query.MoveNext(out var actionId, out _, out var actionComp))
        {
            if (actionComp.AttachedEntity != warlock)
                continue;

            if (channeling)
            {
                RemComp<TargetActionComponent>(actionId);
                RemComp<WorldTargetActionComponent>(actionId);
                var instant = EnsureComp<InstantActionComponent>(actionId);
                instant.Event = new CMUXenoPsychicCrushDetonateActionEvent();
                Dirty(actionId, instant);
            }
            else
            {
                RemComp<InstantActionComponent>(actionId);
                var target = EnsureComp<TargetActionComponent>(actionId);
                target.Range = 9f;
                target.CheckCanAccess = true;
                Dirty(actionId, target);
                var world = EnsureComp<WorldTargetActionComponent>(actionId);
                world.Event = new CMUXenoPsychicCrushActionEvent();
                Dirty(actionId, world);
            }

            // Toggled state drives the action icon flip. Icon "psy_crush_activate" is shown
            // during the channel to signal the button can now detonate; back to "psy_crush"
            // once we swap out of channel mode.
            _actions.SetToggled((actionId, actionComp), channeling);

            return;
        }
    }

    // Swaps the shield action's components so pressing it while the shield is up fires immediately
    // instead of entering target mode.
    private void SetPsychicShieldActionMode(EntityUid warlock, bool shieldUp)
    {
        var query = EntityQueryEnumerator<CMUXenoPsychicShieldActionMarkerComponent, ActionComponent>();
        while (query.MoveNext(out var actionId, out _, out var actionComp))
        {
            if (actionComp.AttachedEntity != warlock)
                continue;

            // Skip the swap when the action entity is on its way out. Happens during round
            // cleanup and integration test teardown: a MoveEvent triggered by parent detach
            // reaches EndPsychicShield after the action has already been queued for deletion,
            // and EnsureComp on a terminating entity fires a DebugAssert.
            if (TerminatingOrDeleted(actionId))
                return;

            if (shieldUp)
            {
                RemComp<TargetActionComponent>(actionId);
                RemComp<WorldTargetActionComponent>(actionId);
                var instant = EnsureComp<InstantActionComponent>(actionId);
                instant.Event = new CMUXenoPsychicShieldDetonateActionEvent();
                Dirty(actionId, instant);
            }
            else
            {
                RemComp<InstantActionComponent>(actionId);
                var target = EnsureComp<TargetActionComponent>(actionId);
                target.Range = 7f;
                target.CheckCanAccess = false;
                Dirty(actionId, target);
                var world = EnsureComp<WorldTargetActionComponent>(actionId);
                world.Event = new CMUXenoPsychicShieldActionEvent();
                Dirty(actionId, world);
            }

            // Toggled=true selects iconOn (psy_shield_reflect, the detonate icon);
            // Toggled=false selects icon (psy_shield, the raise icon). Doing it here
            // guarantees the icon flips at the same tick as the behaviour swap.
            _actions.SetToggled((actionId, actionComp), shieldUp);

            return;
        }
    }

    private Direction GetPsychicShieldDirectionFromTarget(Entity<CMUXenoWarlockComponent> warlock,
        EntityCoordinates target)
    {
        MapCoordinates origin = _transform.GetMapCoordinates(warlock);
        var targetMap = _transform.ToMapCoordinates(target);
        if (origin.MapId != targetMap.MapId)
            return _transform.GetWorldRotation(warlock).GetCardinalDir();

        Vector2 delta = targetMap.Position - origin.Position;
        // Click within the warlock's own tile: fall back to current facing to avoid picking a
        // wrong cardinal from a tiny click-vs-position jitter.
        if (Math.Abs(delta.X) < 0.5f && Math.Abs(delta.Y) < 0.5f)
            return _transform.GetWorldRotation(warlock).GetCardinalDir();

        return delta.ToWorldAngle().GetCardinalDir();
    }

    private bool CanStartPsychicShield(Entity<CMUXenoWarlockComponent> warlock, Direction direction)
    {
        EntityCoordinates target = _transform.GetMoverCoordinates(warlock)
            .Offset(CMUXenoWarlockSystem.GetPsychicShieldObstructionCheckOffset(direction));
        return !_rmcMap.IsTileBlocked(target, CollisionGroup.MobMask);
    }

    private void StartPsychicCrush(Entity<CMUXenoWarlockComponent> warlock, EntityCoordinates target)
    {
        warlock.Comp.PsychicCrushWindingUp = false;
        warlock.Comp.PsychicCrushChanneling = true;
        warlock.Comp.PsychicCrushTarget = target;
        warlock.Comp.PsychicCrushPulses = 0;
        warlock.Comp.NextPsychicCrushPulseAt = _timing.CurTime + warlock.Comp.PsychicCrushPulseInterval;
        warlock.Comp.PsychicCrushOrb = Spawn(warlock.Comp.PsychicCrushOrbId, target);
        SpawnPsychicCrushWarnings(warlock, 0);
        StartWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicCrush);
        StartWarlockChannelParticles(warlock, CMUXenoWarlockChannelKind.PsychicCrush, target);
        // Swap the action from WorldTargetAction to InstantAction so pressing the ability again
        // during the channel early-detonates without a second target click. The swap function
        // also flips the icon via SetToggled(true), so no separate SetActionToggled is needed.
        SetPsychicCrushActionMode(warlock, channeling: true);

        var channeling = EnsureComp<CMUXenoWarlockChannelingComponent>(warlock);
        channeling.SpeedMultiplier = warlock.Comp.PsychicCrushChannelSpeedMultiplier;
        _movement.RefreshMovementSpeedModifiers(warlock);

        var ev = new CMUXenoPsychicCrushChannelDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager,
            warlock,
            CMUXenoWarlockSystem.GetPsychicCrushChannelDuration(warlock.Comp.PsychicCrushPulseInterval),
            ev,
            warlock)
        {
            BreakOnMove = true,
            RootEntity = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
        {
            warlock.Comp.PsychicCrushChannelDoAfter = null;
            ResolvePsychicCrush(warlock, true, true);
            return;
        }

        warlock.Comp.PsychicCrushChannelDoAfter = doAfterId;
    }

    private void StopPsychicCrushWindup(Entity<CMUXenoWarlockComponent> warlock)
    {
        if (!warlock.Comp.PsychicCrushWindingUp)
            return;

        warlock.Comp.PsychicCrushWindingUp = false;
        RemovePsychicCrushMovementModifier(warlock);
    }

    private void ContinuePsychicCrush(Entity<CMUXenoWarlockComponent> warlock)
    {
        if (!CanKeepPsychicCrushTarget(warlock, warlock.Comp.PsychicCrushTarget))
        {
            StopPsychicCrush(warlock, true);
            return;
        }

        if (CMUXenoWarlockSystem.HasPsychicCrushReachedMaxRange(warlock.Comp.PsychicCrushPulses))
        {
            warlock.Comp.PsychicCrushPulses = PsychicCrushMaxPulses;
            TriggerPsychicCrush(warlock);
            return;
        }

        // No plasma is spent per pulse; the full crush cost (40 * pulses) is charged once at
        // detonation in ResolvePsychicCrush, matching TGMC's single-payment model.
        warlock.Comp.PsychicCrushPulses++;
        warlock.Comp.NextPsychicCrushPulseAt = _timing.CurTime + warlock.Comp.PsychicCrushPulseInterval;
        SpawnPsychicCrushWarnings(warlock, warlock.Comp.PsychicCrushPulses);
        _audio.PlayPredicted(warlock.Comp.PsychicCrushPulseSound,
            warlock.Comp.PsychicCrushTarget,
            warlock,
            AudioParams.Default.WithVolume(-2f + warlock.Comp.PsychicCrushPulses));

        // No post-increment auto-detonate here. The outer ring is spawned on the pulse that
        // reaches max radius, then the next ContinuePsychicCrush tick (one pulse interval later)
        // hits the pre-increment HasReachedMaxRange check at the top of this method and
        // detonates. That leaves the last ring visible for a full pulse interval instead of
        // being clipped on the same tick it spawns. The channel do-after (also timed to that
        // same tick via GetPsychicCrushChannelDuration) fires at the same instant as a fallback.
    }

    private void TriggerPsychicCrush(Entity<CMUXenoWarlockComponent> warlock)
    {
        if (!CMUXenoWarlockSystem.CanTriggerPsychicCrush(warlock.Comp.PsychicCrushPulses))
            return;

        if (!CanKeepPsychicCrushTarget(warlock, warlock.Comp.PsychicCrushTarget))
        {
            StopPsychicCrush(warlock, true);
            return;
        }

        // Plasma cost is deducted inside ResolvePsychicCrush so both the manual trigger and the
        // channel-do-after auto-detonate path pay through the same code path.
        ResolvePsychicCrush(warlock, true, true);
    }

    private void StopPsychicCrush(Entity<CMUXenoWarlockComponent> warlock, bool setCooldown,
        bool showSmoothEffect = true)
    {
        if (!warlock.Comp.PsychicCrushChanneling)
            return;

        if (showSmoothEffect)
            SpawnPsychicCrushEndEffect(warlock, false);

        FinishPsychicCrush(warlock, setCooldown);
    }

    private void ResolvePsychicCrush(Entity<CMUXenoWarlockComponent> warlock, bool detonated, bool setCooldown)
    {
        int areaPulses = Math.Clamp(warlock.Comp.PsychicCrushPulses, 0, PsychicCrushMaxPulses);
        int damagePulses = CMUXenoWarlockSystem.GetPsychicCrushResolvedPulses(warlock.Comp.PsychicCrushPulses);

        // Full plasma cost paid once at detonation (40 * pulses), matching TGMC's single-payment
        // model. If the warlock can't afford it, cancel and take the cooldown.
        if (detonated && !_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null),
                CMUXenoWarlockSystem.GetPsychicCrushCost(damagePulses)))
        {
            StopPsychicCrush(warlock, true);
            return;
        }

        _audio.PlayPredicted(warlock.Comp.PsychicCrushTriggerSound, warlock.Comp.PsychicCrushTarget, warlock);
        SpawnPsychicCrushEndEffect(warlock, detonated);
        SpawnPsychicCrushBlur(warlock, areaPulses);
        ApplyPsychicCrushDamage(warlock, areaPulses, damagePulses);
        FinishPsychicCrush(warlock, setCooldown);
    }

    private void ApplyPsychicCrushDamage(Entity<CMUXenoWarlockComponent> warlock,
        int areaPulses,
        int damagePulses)
    {
        var damageAmount = FixedPoint2.New(CMUXenoWarlockSystem.GetPsychicCrushDamage(damagePulses));
        var mobDamage = new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = damageAmount }
        };
        var vehicleDamage = new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = damageAmount }
        };

        _affected.Clear();

        // Resolve the crush centre's grid. Prefer the target's stored parent grid (set at cast
        // time by TrySnapAbilityTargetToTile) over TryFindGridAt to avoid picking a stacked grid
        // at the same map position. Fall back to a spatial lookup only if the stored parent is
        // somehow not a grid.
        var targetCoords = warlock.Comp.PsychicCrushTarget;
        var targetMap = _transform.ToMapCoordinates(targetCoords);
        EntityUid gridUid = targetCoords.EntityId;
        if (!TryComp(gridUid, out MapGridComponent? grid))
        {
            if (!_map.TryFindGridAt(targetMap, out gridUid, out grid))
                return;
        }

        // Build the absolute tile indices for the diamond, then let the engine's tile-based
        // intersection lookup return every entity whose physics fixture overlaps any of those
        // tiles. This is the same pattern the boiler's acid gas uses: the visible tile and the
        // damage source are keyed on the same grid tile, so if a mob's fixture touches a
        // highlighted tile they are hit, and if it doesn't they aren't. Uses the engine's
        // default TileEnlargementRadius so mobs whose fixture just grazes a tile edge are not
        // false positives.
        Vector2i centreTile = _map.WorldToTile(gridUid, grid, targetMap.Position);
        var affectedTiles = new List<Vector2i>();
        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(areaPulses))
        {
            // Same wall filter the warning ring and the blur use, so what the marines were shown
            // is exactly what gets hit.
            if (!IsPsychicCrushTileAffected(targetMap, targetCoords.Offset(new(offset.X, offset.Y))))
                continue;

            affectedTiles.Add(new Vector2i(centreTile.X + offset.X, centreTile.Y + offset.Y));
        }

        var candidates = _lookup.GetLocalEntitiesIntersecting(gridUid, affectedTiles);

        foreach (var target in candidates)
        {
            if (target == warlock.Owner)
                continue;
            if (!TryComp<MobStateComponent>(target, out var state))
                continue;
            if (_mobState.IsDead(target, state))
                continue;
            if (!_xeno.CanAbilityAttackTarget(warlock.Owner, target))
                continue;
            if (!_affected.Add(target))
                continue;

            _damageable.TryChangeDamage(target, mobDamage, origin: warlock, tool: warlock);
            _daze.TryDaze(target, CMUXenoWarlockSystem.GetPsychicCrushStaggerDuration(damagePulses), true,
                stutter: true);
            _slow.TrySlowdown(target, CMUXenoWarlockSystem.GetPsychicCrushSlowDuration(damagePulses),
                ignoreDurationModifier: true);

            // High-pulse Paralyze mirrors TGMC's stamina-exhaustion slump: at charge 4+ the
            // marine is forced prone and stunned briefly on top of the daze/slow debuffs.
            TimeSpan paralyzeDuration = CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(damagePulses);
            if (paralyzeDuration > TimeSpan.Zero)
                _stun.TryParalyze(target, paralyzeDuration, true);
        }

        // Vehicle / mech pass reuses the same tile-intersection candidates and filters on
        // Damageable so mobs already handled above are skipped via the _affected set.
        foreach (var target in candidates)
        {
            if (_affected.Contains(target))
                continue;
            if (!HasComp<DamageableComponent>(target))
                continue;

            if (HasComp<MechComponent>(target))
            {
                _damageable.TryChangeDamage(target, vehicleDamage * PsychicCrushMechDamageMultiplier,
                    origin: warlock, tool: warlock);
            }
            else if (HasComp<VehicleComponent>(target))
            {
                _damageable.TryChangeDamage(target, vehicleDamage * PsychicCrushVehicleDamageMultiplier,
                    origin: warlock, tool: warlock);
            }
        }
    }

    private void FinishPsychicCrush(Entity<CMUXenoWarlockComponent> warlock, bool setCooldown)
    {
        warlock.Comp.PsychicCrushChanneling = false;
        warlock.Comp.PsychicCrushWindingUp = false;
        warlock.Comp.PsychicCrushPulses = 0;
        warlock.Comp.NextPsychicCrushPulseAt = TimeSpan.Zero;
        // Cancel the outstanding channel do-after (if any) so the progress bar clears from the
        // warlock's HUD on early detonate or interruption. PsychicCrushChanneling was set to
        // false above; the OnPsychicCrushChannelDoAfter handler short-circuits on that flag, so
        // the resulting Cancelled event cannot re-enter ResolvePsychicCrush.
        if (warlock.Comp.PsychicCrushChannelDoAfter is { } channelDoAfter)
        {
            _doAfter.Cancel(channelDoAfter);
            warlock.Comp.PsychicCrushChannelDoAfter = null;
        }
        // Swap back to WorldTargetAction FIRST so the subsequent SetActionCooldown lookup, which
        // finds actions via CMUXenoPsychicCrushActionEvent, sees the restored event. The swap
        // function also toggles the icon back to the raise state.
        SetPsychicCrushActionMode(warlock, channeling: false);
        ClearPsychicCrushEffects(warlock);
        StopWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicCrush);
        StopWarlockChannelParticles(warlock, CMUXenoWarlockChannelKind.PsychicCrush);

        if (setCooldown)
        {
            warlock.Comp.NextPsychicCrushAt = _timing.CurTime + warlock.Comp.PsychicCrushCooldown;
            SetActionCooldown<CMUXenoPsychicCrushActionEvent>(warlock, warlock.Comp.PsychicCrushCooldown);
        }

        RemovePsychicCrushMovementModifier(warlock);
    }

    private void SpawnPsychicCrushEndEffect(Entity<CMUXenoWarlockComponent> warlock, bool detonated)
    {
        EntProtoId prototype = detonated
            ? warlock.Comp.PsychicCrushDetonateId
            : warlock.Comp.PsychicCrushSmoothId;

        Spawn(prototype, warlock.Comp.PsychicCrushTarget);
    }

    // Walls stop the crush from growing past them. A tile counts only when it is not itself blocked
    // and the epicentre has a clear line to it. CollisionGroup.Impassable is the wall layer, so
    // walls and windows cut the area whereas barricades do not - a cade's fixture sits on
    // TableLayer, BarricadeImpassable and BulletImpassable, all outside this mask. Same two-part
    // tile filter SharedXenoForTheHiveSystem uses to keep its acid smoke out of sealed rooms.
    private bool IsPsychicCrushTileAffected(MapCoordinates origin, EntityCoordinates tile)
    {
        if (_rmcMap.IsTileBlocked(tile, CollisionGroup.Impassable))
            return false;

        return _interaction.InRangeUnobstructed(origin,
            _transform.ToMapCoordinates(tile),
            PsychicCrushMaxAreaRadius + 1f,
            CollisionGroup.Impassable);
    }

    private void SpawnPsychicCrushBlur(Entity<CMUXenoWarlockComponent> warlock, int areaPulses)
    {
        if (!CMUXenoWarlockSystem.ShouldSpawnPsychicCrushTileBlur(true))
            return;

        var originMap = _transform.ToMapCoordinates(warlock.Comp.PsychicCrushTarget);
        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(areaPulses))
        {
            EntityCoordinates tile = warlock.Comp.PsychicCrushTarget.Offset(new(offset.X, offset.Y));
            if (!IsPsychicCrushTileAffected(originMap, tile))
                continue;

            Spawn(warlock.Comp.PsychicCrushBlurId, tile);
        }
    }

    private void StartWarlockChannelParticles(Entity<CMUXenoWarlockComponent> warlock,
        CMUXenoWarlockChannelKind kind,
        EntityCoordinates target)
    {
        if (CMUXenoWarlockSystem.GetWarlockChannelParticle(warlock.Comp, kind) != null)
            return;

        EntProtoId prototype = CMUXenoWarlockSystem.GetWarlockChannelParticlePrototype(warlock.Comp, kind);
        EntityUid holder = SpawnAttachedTo(prototype, warlock.Owner.ToCoordinates());
        CMUXenoWarlockSystem.SetWarlockChannelParticle(warlock.Comp, kind, holder);

        if (kind != CMUXenoWarlockChannelKind.PsychicBlast
            || !TryComp(holder, out CMUXenoWarlockParticleEmitterComponent? particles))
            return;

        MapCoordinates originMap = _transform.GetMapCoordinates(warlock);
        var targetMap = _transform.ToMapCoordinates(target);
        if (originMap.MapId != targetMap.MapId)
            return;

        CMUXenoWarlockParticleMotion? motion = CMUXenoWarlockSystem.GetWarlockDirectedParticleMotion(originMap.Position,
            targetMap.Position, WarlockDirectedParticleVelocity);
        if (motion == null)
            return;

        particles.UseMotionOverride = true;
        particles.MotionVelocity = motion.Value.Velocity;
        particles.MotionGravity = motion.Value.Gravity;

        // Cap the wind-up particles' outward travel to the projectile's actual impact distance
        // (click distance capped at PsychicBlastRange). The densest particle ring settles at cap
        // because every particle accelerates until it hits the wall and then holds there for the
        // rest of its lifespan, so aligning cap with the impact distance makes the visible outer
        // edge of the cloud land on the tile the projectile detonates on. The initial ring can
        // still push a small handful of outliers ~0.5 tiles past cap; the corresponding fade-in
        // near cap keeps that overshoot from reading as visual spill.
        if (kind == CMUXenoWarlockChannelKind.PsychicBlast)
        {
            float distanceMeters = Math.Min(
                (targetMap.Position - originMap.Position).Length(),
                warlock.Comp.PsychicBlastRange);
            particles.MaxDirectedTravelPixelsOverride = distanceMeters * WarlockParticlePixelsPerMeter;
        }

        Dirty(holder, particles);
    }

    private void StopWarlockChannelParticles(Entity<CMUXenoWarlockComponent> warlock, CMUXenoWarlockChannelKind kind)
    {
        if (CMUXenoWarlockSystem.GetWarlockChannelParticle(warlock.Comp, kind) is not { } particles)
            return;

        if (!Deleted(particles))
            QueueDel(particles);

        CMUXenoWarlockSystem.SetWarlockChannelParticle(warlock.Comp, kind, null);
    }

    private static EntityUid? GetWarlockChannelParticle(CMUXenoWarlockComponent warlock, CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicCrush => warlock.PsychicCrushChannelParticle,
            CMUXenoWarlockChannelKind.PsychicBlast => warlock.PsychicBlastChannelParticle, _ => null
        };
    }

    private static void SetWarlockChannelParticle(CMUXenoWarlockComponent warlock, CMUXenoWarlockChannelKind kind,
        EntityUid? particles)
    {
        switch (kind)
        {
            case CMUXenoWarlockChannelKind.PsychicCrush:
                warlock.PsychicCrushChannelParticle = particles;
                break;
            case CMUXenoWarlockChannelKind.PsychicBlast:
                warlock.PsychicBlastChannelParticle = particles;
                break;
        }
    }

    private bool CanKeepPsychicCrushTarget(Entity<CMUXenoWarlockComponent> warlock, EntityCoordinates target)
    {
        MapCoordinates origin = _transform.GetMapCoordinates(warlock);
        var targetMap = _transform.ToMapCoordinates(target);
        if (origin.MapId != targetMap.MapId)
            return false;

        return (origin.Position - targetMap.Position).Length() <= warlock.Comp.PsychicCrushRange
            && _interaction.InRangeUnobstructed(warlock.Owner, target, warlock.Comp.PsychicCrushRange, popup: false);
    }

    // Resolves a raw click into the centre of its containing tile and confirms the warlock is
    // close enough to the tile to cast on it. The reach test is closest-point-of-tile against
    // <paramref name="range"/> - the same rule the client range overlay uses to decide whether a
    // tile is highlighted, so every highlighted tile is a legal target regardless of where on
    // the tile the click actually landed. Returns false (and leaves <paramref name="snapped"/>
    // as the raw click) when there is no grid under the click, the click is on a different map
    // from the warlock, or the tile lies outside range.
    private bool TrySnapAbilityTargetToTile(
        Entity<CMUXenoWarlockComponent> warlock,
        EntityCoordinates click,
        float range,
        out EntityCoordinates snapped)
    {
        snapped = click;

        MapCoordinates originMap = _transform.GetMapCoordinates(warlock);
        MapCoordinates clickMap = _transform.ToMapCoordinates(click);
        if (originMap.MapId != clickMap.MapId || originMap.MapId == MapId.Nullspace)
            return false;

        if (!_map.TryFindGridAt(clickMap, out EntityUid gridUid, out MapGridComponent? grid))
            return false;

        Vector2i tileIndices = _map.CoordinatesToTile(gridUid, grid, click);
        MapCoordinates tileCentreMap = _map.GridTileToWorld(gridUid, grid, tileIndices);

        // Closest-point-of-tile distance from the warlock. Per axis, zero when the warlock is
        // inside the tile on that axis, else |delta.axis| - halfTile. Mirrors the client overlay
        // at [XenoAbilityPreviewOverlay.cs] DrawWarlockRangeCircle so the highlight and the
        // acceptance rule match by construction.
        float halfTile = grid.TileSize / 2f;
        Vector2 delta = tileCentreMap.Position - originMap.Position;
        Vector2 closest = new(
            MathF.Max(0f, MathF.Abs(delta.X) - halfTile),
            MathF.Max(0f, MathF.Abs(delta.Y) - halfTile));
        if (closest.LengthSquared() > range * range)
            return false;

        snapped = _transform.ToCoordinates(gridUid, tileCentreMap);
        return true;
    }

    private void StartPsychicShield(Entity<CMUXenoWarlockComponent> warlock, Direction direction)
    {
        warlock.Comp.PsychicShieldDirection = direction;
        warlock.Comp.PsychicShieldIntegrityRemaining = warlock.Comp.PsychicShieldIntegrity;
        warlock.Comp.PsychicShieldExpiresAt = _timing.CurTime + warlock.Comp.PsychicShieldDuration;
        warlock.Comp.PsychicShieldMoveCancelGraceUntil = _timing.CurTime + warlock.Comp.PsychicShieldMoveCancelGrace;
        _audio.PlayPredicted(warlock.Comp.PsychicShieldStartSound, warlock, warlock);
        StartWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicShield);
        // Icon toggle is handled inside SetPsychicShieldActionMode below, alongside the swap.
        EnsureComp<CMUXenoPsychicShieldRootComponent>(warlock);
        _movement.RefreshMovementSpeedModifiers(warlock);
        if (TryComp(warlock, out PhysicsComponent? physics))
            _physics.SetLinearVelocity(warlock, Vector2.Zero, body: physics);

        // Spawn 1 world-unit in front of the warlock's current position, in the facing direction.
        // Not tile-snapped - shield tracks the warlock's exact sub-tile position.
        EntityCoordinates spawnCoords = _transform.GetMoverCoordinates(warlock)
            .Offset(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(direction));
        EntityUid shield = Spawn(warlock.Comp.PsychicShieldVisualId, spawnCoords);
        _transform.SetWorldRotationNoLerp(shield, direction.ToAngle());
        _rmcSprite.SetOffset(shield,
            CMUXenoWarlockSystem.GetPsychicShieldVisualOffset(direction));


        var comp = EnsureComp<CMUXenoPsychicShieldSegmentComponent>(shield);
        comp.Warlock = warlock;
        comp.Direction = warlock.Comp.PsychicShieldDirection;
        Dirty(shield, comp);
        warlock.Comp.PsychicShieldSegments.Add(shield);

        // Swap the shield action to InstantAction so the next button press detonates immediately
        // instead of entering target mode again.
        SetPsychicShieldActionMode(warlock, shieldUp: true);
    }

    private void DetonatePsychicShield(Entity<CMUXenoWarlockComponent> warlock)
    {
        ReflectShieldProjectiles(warlock);
        ApplyPsychicShieldBlast(warlock);
        _audio.PlayPredicted(warlock.Comp.PsychicShieldBlastSound, warlock, warlock);
        _audio.PlayPredicted(warlock.Comp.PsychicShieldRoarSound, warlock, warlock);
        EndPsychicShield(warlock, false, false);
    }

    private void EndPsychicShield(Entity<CMUXenoWarlockComponent> warlock, bool reflectProjectiles, bool stunOwner)
    {
        foreach (EntityUid segment in warlock.Comp.PsychicShieldSegments)
        {
            if (!Deleted(segment))
            {
                if (TryComp(segment, out PhysicsComponent? physics))
                    _physics.SetCanCollide(segment, false, body: physics);

                QueueDel(segment);
            }
        }

        if (reflectProjectiles)
            ReflectShieldProjectiles(warlock);
        else
            ReleaseShieldProjectiles(warlock);

        warlock.Comp.PsychicShieldSegments.Clear();
        warlock.Comp.PsychicShieldIntegrityRemaining = FixedPoint2.Zero;
        warlock.Comp.PsychicShieldExpiresAt = TimeSpan.Zero;
        warlock.Comp.PsychicShieldMoveCancelGraceUntil = TimeSpan.Zero;
        warlock.Comp.NextPsychicShieldAt = _timing.CurTime + warlock.Comp.PsychicShieldCooldown;
        StopWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicShield);
        // Swap the shield action back to WorldTargetAction so the next press enters target mode.
        // The icon toggle back to the raise icon is handled inside SetPsychicShieldActionMode.
        SetPsychicShieldActionMode(warlock, shieldUp: false);
        SetActionCooldown<CMUXenoPsychicShieldActionEvent>(warlock, warlock.Comp.PsychicShieldCooldown);
        if (RemComp<CMUXenoPsychicShieldRootComponent>(warlock))
            _movement.RefreshMovementSpeedModifiers(warlock);

        if (stunOwner)
            _stun.TryParalyze(warlock, warlock.Comp.PsychicShieldOwnerStun, true);
    }

    private void ReleaseShieldProjectiles(Entity<CMUXenoWarlockComponent> warlock)
    {
        foreach (EntityUid projectile in warlock.Comp.FrozenProjectiles)
        {
            if (!TryComp(projectile, out CMUXenoFrozenProjectileComponent? frozen))
                continue;

            RemComp<CMUXenoFrozenProjectileComponent>(projectile);

            if (TryComp(projectile, out PhysicsComponent? physics))
                RestoreFrozenProjectilePhysics(projectile, frozen, frozen.Velocity, physics);

            RestoreFrozenProjectile(projectile, frozen);
            ResetShieldProjectilePrediction(projectile);
        }

        warlock.Comp.FrozenProjectiles.Clear();
    }

    private void ReflectShieldProjectiles(Entity<CMUXenoWarlockComponent> warlock)
    {
        _audio.PlayPredicted(warlock.Comp.PsychicShieldReflectSound, GetPsychicShieldSoundCoordinates(warlock),
            warlock);

        foreach (EntityUid frozenEnt in warlock.Comp.FrozenProjectiles)
        {
            if (!TryComp(frozenEnt, out CMUXenoFrozenProjectileComponent? frozen))
                continue;

            // Thrown items (grenades, molotovs) are not projectiles - they do not carry a
            // velocity that ThrowingSystem's flight loop reads. Reflecting them means feeding
            // the reflected direction into TryThrow so ThrownItemComponent gets rebuilt with a
            // proper LandTime, throw-fixture, and spin. The remove-frozen-component still runs
            // for the outline shutdown; TryThrow handles the physics restore itself.
            bool isThrownItem = !HasComp<ProjectileComponent>(frozenEnt)
                && TryComp(frozenEnt, out ThrownItemComponent? _);

            // Everything reflects straight back along the shield face, projectiles and thrown
            // items alike.
            Vector2 reflected = CMUXenoWarlockSystem.ReflectProjectileVelocity(frozen.Velocity,
                warlock.Comp.PsychicShieldDirection);

            if (!isThrownItem)
            {
                if (TryComp(frozenEnt, out PhysicsComponent? physics))
                    RestoreFrozenProjectilePhysics(frozenEnt, frozen, reflected, physics);
            }

            RemComp<CMUXenoFrozenProjectileComponent>(frozenEnt);

            if (isThrownItem)
            {
                // ThrowingSystem expects a target-relative displacement vector (magnitude =
                // travel distance, direction = which way). Normalize the reflected velocity so
                // we can rescale it to the configured reflect distance.
                Vector2 direction = reflected;
                if (direction.LengthSquared() > 0f)
                    direction = Vector2.Normalize(direction) * warlock.Comp.PsychicShieldReflectedThrowDistance;

                _throwing.TryThrow(
                    frozenEnt,
                    direction,
                    warlock.Comp.PsychicShieldReflectedThrowSpeed,
                    warlock,
                    animated: true,
                    playSound: false);

                ResetShieldProjectilePrediction(frozenEnt);
                continue;
            }

            Angle projectileAngle = Angle.Zero;
            if (TryComp(frozenEnt, out ProjectileComponent? projectileComp))
            {
                projectileComp.Shooter = warlock;
                projectileComp.Weapon = warlock;
                projectileComp.IgnoreShooter = false;
                projectileComp.DeleteOnCollide = frozen.DeleteOnCollide;
                projectileComp.ProjectileSpent = false;
                Dirty(frozenEnt, projectileComp);
                projectileAngle = projectileComp.Angle;
            }

            // Restore DeleteOnCollide but skip the fixed-distance restore. The freeze captured how
            // much of the pre-catch flight time was left when the shield stopped the projectile;
            // handing that tiny remainder back would leave the reflected rocket to stall a few tiles
            // out and then explode on anyone who walks over it. A reflected projectile should behave
            // like it was freshly fired - fly until it hits something.
            if (frozen.HadDeleteOnCollideComponent)
                EnsureComp<DeleteOnCollideComponent>(frozenEnt);

            _transform.SetWorldRotationNoLerp(frozenEnt, reflected.ToWorldAngle() + projectileAngle);
            ResetShieldProjectilePrediction(frozenEnt);
        }

        warlock.Comp.FrozenProjectiles.Clear();
    }

    private EntityCoordinates GetPsychicShieldSoundCoordinates(Entity<CMUXenoWarlockComponent> warlock)
    {
        EntityCoordinates origin = _transform.GetMoverCoordinates(warlock);
        Vector2 offset = CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(warlock.Comp.PsychicShieldDirection);
        return origin.Offset(offset);
    }

    private void ApplyPsychicShieldBlast(Entity<CMUXenoWarlockComponent> warlock)
    {
        _affected.Clear();
        EntityCoordinates origin = _transform.GetMoverCoordinates(warlock);
        Vector2 direction = warlock.Comp.PsychicShieldDirection.ToVec();

        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicShieldBlastOffsets(warlock.Comp
            .PsychicShieldDirection))
        {
            EntityCoordinates coords = origin.Offset(new(offset.X, offset.Y));
            foreach ((EntityUid target, MobStateComponent state) in _lookup.GetEntitiesInRange<MobStateComponent>(
                coords, 0.45f))
            {
                if (target == warlock.Owner
                    || !_affected.Add(target)
                    || _mobState.IsDead(target, state)
                    || !_xeno.CanAbilityAttackTarget(warlock.Owner, target))
                    continue;

                _stun.TryParalyze(target, warlock.Comp.PsychicShieldBlastParalyze, true);
                _throwing.TryThrow(target, direction, warlock.Comp.PsychicShieldBlastThrowSpeed, warlock,
                    animated: false, playSound: false, compensateFriction: true);
            }
        }
    }

    private void OnShieldProjectilePreventCollide(Entity<CMUXenoPsychicShieldSegmentComponent> segment,
        ref PreventCollideEvent args)
    {
        if (!TryComp(args.OtherEntity, out ProjectileComponent? projectile)
            || !TryComp(args.OtherEntity, out PhysicsComponent? physics))
            return;

        if (TryFreezeShieldProjectile(segment, args.OtherEntity, projectile, physics))
            args.Cancelled = true;
    }

    private void OnShieldProjectileReflectAttempt(Entity<CMUXenoPsychicShieldSegmentComponent> segment,
        ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || !TryComp(args.ProjUid, out PhysicsComponent? physics))
            return;

        if (TryFreezeShieldProjectile(segment, args.ProjUid, args.Component, physics))
            args.Cancelled = true;
    }

    // ThrownItemSystem raises ThrowHitByEvent on the target (the shield segment) whenever a
    // thrown item's soft in-flight fixture contacts a hard fixture. Server-authoritative: we
    // funnel it into the same freeze pipeline used for projectiles so the grenade gets the
    // pulsing outline, joins the FrozenProjectiles list, and rides the reflect / release
    // paths. AllowTriggerWhileFrozen is set true on the freeze so the grenade's fuse timer
    // keeps ticking - if the warlock does not reflect in time, it detonates at the shield face.
    private void OnShieldThrowHitBy(Entity<CMUXenoPsychicShieldSegmentComponent> segment,
        ref ThrowHitByEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(args.Thrown, out PhysicsComponent? physics))
            return;

        TryFreezeShieldThrownItem(segment, args.Thrown, args.Component, physics);
    }

    // Freeze a thrown item (grenade, molotov, etc.) at the shield face. Same pattern as
    // TryFreezeShieldProjectile but keys on ThrownItemComponent instead of ProjectileComponent.
    // The thrown item is added to the warlock's FrozenProjectiles list so it participates in
    // reflect and release; ReflectShieldProjectiles reads HasComp<ProjectileComponent> to pick
    // the projectile-reflect vs throw-back-via-ThrowingSystem branch.
    private bool TryFreezeShieldThrownItem(Entity<CMUXenoPsychicShieldSegmentComponent> segment,
        EntityUid thrown,
        ThrownItemComponent thrownComp,
        PhysicsComponent physics)
    {
        if (!TryComp(segment.Comp.Warlock, out CMUXenoWarlockComponent? warlock))
            return false;

        if (HasComp<CMUXenoFrozenProjectileComponent>(thrown))
            return true;

        Vector2 velocity = _physics.GetMapLinearVelocity(thrown, physics);
        if (velocity.LengthSquared() <= 0f)
            return false;

        if (!CMUXenoWarlockSystem.IsProjectileIncomingFromFront(velocity, segment.Comp.Direction))
            return false;

        // Friendly-fire filter. Same-hive throws pass through the shield untouched instead of
        // being caught and later reflected at the friendly thrower. CanAbilityAttackTarget
        // returns false when warlock and thrower share a hive, so we skip the freeze in that
        // case. Missing thrower (e.g., scatter grenade sub-explosive with no throw origin) is
        // treated as hostile so the shield still catches it.
        if (thrownComp.Thrower is { } thrower
            && !_xeno.CanAbilityAttackTarget(segment.Comp.Warlock, thrower))
            return false;

        // Slow throws (hand-thrown grenades) pass straight through. Only fast throws (launcher
        // projectileSpeed = 20, cannon-launched items, xeno slams, etc.) get caught. The cutoff
        // sits above the effective hand-throw velocity (base 11, friction-compensated to ~5-9
        // depending on throw distance) and below the launcher's 20, so it separates cleanly.
        if (velocity.Length() <= warlock.PsychicShieldMinimumFreezeSpeed)
            return false;

        BodyStatus bodyStatus = physics.BodyStatus;
        BodyType bodyType = physics.BodyType;
        bool canCollide = physics.CanCollide;

        var frozen = EnsureComp<CMUXenoFrozenProjectileComponent>(thrown);
        frozen.Velocity = velocity;
        frozen.BodyStatus = bodyStatus;
        frozen.CanCollide = canCollide;
        frozen.BodyType = bodyType;
        // Keep the fuse alive. Rockets keep the default (false) so their trigger cannot fire
        // at the shield; grenades opt in here so their timer expires normally at the shield face.
        frozen.AllowTriggerWhileFrozen = true;
        Dirty(thrown, frozen);

        _physics.SetBodyType(thrown, BodyType.Static, body: physics);
        _physics.SetLinearVelocity(thrown, Vector2.Zero, body: physics);
        _physics.SetCanCollide(thrown, false, body: physics);

        if (!warlock.FrozenProjectiles.Contains(thrown))
            warlock.FrozenProjectiles.Add(thrown);

        return true;
    }

    // Public so the server-side StartCollide fallback can reuse the same freeze pipeline.
    public bool TryFreezeShieldProjectile(Entity<CMUXenoPsychicShieldSegmentComponent> segment,
        EntityUid projectile,
        ProjectileComponent projectileComp,
        PhysicsComponent physics)
    {
        if (!TryComp(segment.Comp.Warlock, out CMUXenoWarlockComponent? warlock))
            return false;

        if (HasComp<CMUXenoFrozenProjectileComponent>(projectile))
            return true;

        Vector2 velocity = _physics.GetMapLinearVelocity(projectile, physics);
        if (!CMUXenoWarlockSystem.IsProjectileIncomingFromFront(velocity, segment.Comp.Direction))
            return false;

        // Friendly-fire filter. Same-hive shots pass through the shield untouched instead of
        // being caught and later reflected at the friendly shooter. CanAbilityAttackTarget
        // returns false when warlock and shooter share a hive, so we skip the freeze in that
        // case. Missing shooter (unknown origin, admin spawn, etc.) is treated as hostile so
        // the shield still catches it.
        if (projectileComp.Shooter is { } projShooter
            && !_xeno.CanAbilityAttackTarget(segment.Comp.Warlock, projShooter))
            return false;

        BodyStatus bodyStatus = physics.BodyStatus;
        BodyType bodyType = physics.BodyType;
        bool canCollide = physics.CanCollide;
        EntityUid? shooter = projectileComp.Shooter;
        EntityUid? weapon = projectileComp.Weapon;
        bool ignoreShooter = projectileComp.IgnoreShooter;
        bool deleteOnCollide = projectileComp.DeleteOnCollide;
        bool projectileSpent = projectileComp.ProjectileSpent;

        MoveProjectileToShieldFace(segment, projectile);

        var frozen = EnsureComp<CMUXenoFrozenProjectileComponent>(projectile);
        frozen.Velocity = velocity;
        frozen.BodyStatus = bodyStatus;
        frozen.CanCollide = canCollide;
        frozen.BodyType = bodyType;
        frozen.Shooter = shooter;
        frozen.Weapon = weapon;
        frozen.IgnoreShooter = ignoreShooter;
        frozen.DeleteOnCollide = deleteOnCollide;
        frozen.ProjectileSpent = projectileSpent;
        frozen.HadDeleteOnCollideComponent = RemComp<DeleteOnCollideComponent>(projectile);
        FreezeFixedDistanceProjectileLifetime(projectile, frozen);
        Dirty(projectile, frozen);

        _physics.SetBodyType(projectile, BodyType.Static, body: physics);
        _physics.SetLinearVelocity(projectile, Vector2.Zero, body: physics);
        _physics.SetCanCollide(projectile, false, body: physics);

        projectileComp.DeleteOnCollide = false;
        projectileComp.ProjectileSpent = false;
        Dirty(projectile, projectileComp);

        if (!warlock.FrozenProjectiles.Contains(projectile))
            warlock.FrozenProjectiles.Add(projectile);

        if (!CMUXenoWarlockSystem.ShouldPsychicShieldApplyAuthoritativeFreezeSideEffects(_net.IsClient))
            return true;

        warlock.PsychicShieldIntegrityRemaining -= projectileComp.Damage.GetTotal();
        UpdatePsychicShieldAlpha((segment.Comp.Warlock, warlock));

        if (warlock.PsychicShieldIntegrityRemaining <= FixedPoint2.Zero
            || CMUXenoWarlockSystem.ShouldPsychicShieldBreakFromFrozenProjectiles(warlock.FrozenProjectiles.Count,
                warlock.PsychicShieldMaxFrozenProjectiles))
            EndPsychicShield((segment.Comp.Warlock, warlock), false, true);

        return true;
    }

    private void RestoreFrozenProjectilePhysics(EntityUid projectile,
        CMUXenoFrozenProjectileComponent frozen,
        Vector2 velocity,
        PhysicsComponent physics)
    {
        _physics.SetBodyType(projectile, frozen.BodyType, body: physics);
        _physics.SetBodyStatus(projectile, physics, BodyStatus.InAir);
        _physics.SetLinearVelocity(projectile, velocity, body: physics);
        _physics.SetCanCollide(projectile, frozen.CanCollide, body: physics);
    }

    private void RestoreFrozenProjectile(EntityUid projectile, CMUXenoFrozenProjectileComponent frozen)
    {
        if (!TryComp(projectile, out ProjectileComponent? projectileComp))
        {
            RestoreFrozenProjectileDeleteOnCollide(projectile, frozen);
            return;
        }

        projectileComp.Shooter = frozen.Shooter;
        projectileComp.Weapon = frozen.Weapon;
        projectileComp.IgnoreShooter = frozen.IgnoreShooter;
        projectileComp.DeleteOnCollide = frozen.DeleteOnCollide;
        projectileComp.ProjectileSpent = false;
        Dirty(projectile, projectileComp);
        RestoreFrozenProjectileDeleteOnCollide(projectile, frozen);
    }

    private void MoveProjectileToShieldFace(Entity<CMUXenoPsychicShieldSegmentComponent> segment, EntityUid projectile)
    {
        MapCoordinates shieldCoordinates = _transform.GetMapCoordinates(segment);
        MapCoordinates projectileCoordinates = _transform.GetMapCoordinates(projectile);
        if (shieldCoordinates.MapId != projectileCoordinates.MapId)
            return;

        Vector2 stopPosition = CMUXenoWarlockSystem.GetPsychicShieldFrozenProjectilePosition(shieldCoordinates.Position,
            projectileCoordinates.Position,
            segment.Comp.Direction);
        _transform.SetMapCoordinates(projectile, new(stopPosition, shieldCoordinates.MapId));
    }

    private void RestoreFrozenProjectileDeleteOnCollide(EntityUid projectile, CMUXenoFrozenProjectileComponent frozen)
    {
        if (frozen.HadDeleteOnCollideComponent)
            EnsureComp<DeleteOnCollideComponent>(projectile);

        RestoreFixedDistanceProjectileLifetime(projectile, frozen);
    }

    private void FreezeFixedDistanceProjectileLifetime(EntityUid projectile, CMUXenoFrozenProjectileComponent frozen)
    {
        if (TryComp(projectile, out ProjectileFixedDistanceComponent? fixedDistance))
        {
            frozen.HadProjectileFixedDistanceComponent = true;
            frozen.FixedDistanceRemaining = fixedDistance.FlyEndTime - _timing.CurTime;
            if (frozen.FixedDistanceRemaining < TimeSpan.Zero)
                frozen.FixedDistanceRemaining = TimeSpan.Zero;

            frozen.FixedDistanceTargetCoordinates = fixedDistance.TargetCoordinates;
            frozen.FixedDistanceArcProj = fixedDistance.ArcProj;
            RemComp<ProjectileFixedDistanceComponent>(projectile);
        }

        frozen.HadDeleteOnFixedDistanceStopComponent = RemComp<DeleteOnFixedDistanceStopComponent>(projectile);
    }

    private void RestoreFixedDistanceProjectileLifetime(EntityUid projectile, CMUXenoFrozenProjectileComponent frozen)
    {
        if (frozen.HadProjectileFixedDistanceComponent)
        {
            var fixedDistance = EnsureComp<ProjectileFixedDistanceComponent>(projectile);
            fixedDistance.FlyEndTime = _timing.CurTime + frozen.FixedDistanceRemaining;
            fixedDistance.TargetCoordinates = null;
            fixedDistance.ArcProj = frozen.FixedDistanceArcProj;
            Dirty(projectile, fixedDistance);
        }

        if (frozen.HadDeleteOnFixedDistanceStopComponent)
            EnsureComp<DeleteOnFixedDistanceStopComponent>(projectile);
    }

    private void UpdatePsychicShieldAlpha(Entity<CMUXenoWarlockComponent> warlock)
    {
        Color color = Color.White.WithAlpha(CMUXenoWarlockSystem.GetPsychicShieldAlpha(
            warlock.Comp.PsychicShieldIntegrityRemaining,
            warlock.Comp.PsychicShieldIntegrity));

        foreach (EntityUid segment in warlock.Comp.PsychicShieldSegments)
        {
            if (!Deleted(segment))
                _rmcSprite.SetColor(segment, color);
        }
    }

    private void OnWarlockMove(Entity<CMUXenoWarlockComponent> warlock, ref MoveEvent args)
    {
        StopPsychicBlastChannel(warlock);
        if (warlock.Comp.PsychicShieldSegments.Count > 0
            && CMUXenoWarlockSystem.ShouldPsychicShieldApplyMoveCancel(_net.IsClient)
            && CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(args.OldPosition.Position,
                args.NewPosition.Position,
                args.ParentChanged,
                _timing.CurTime,
                warlock.Comp.PsychicShieldMoveCancelGraceUntil))
            EndPsychicShield(warlock, false, false);
    }

    private void OnWarlockStunned(Entity<CMUXenoWarlockComponent> warlock, ref StunnedEvent args)
    {
        StopPsychicBlastChannel(warlock);
        StopPsychicCrushWindup(warlock);
        StopPsychicCrush(warlock, true);
        if (warlock.Comp.PsychicShieldSegments.Count > 0)
            EndPsychicShield(warlock, false, false);
    }

    private void OnWarlockKnockedDown(Entity<CMUXenoWarlockComponent> warlock, ref KnockedDownEvent args)
    {
        StopPsychicBlastChannel(warlock);
        StopPsychicCrushWindup(warlock);
        StopPsychicCrush(warlock, true);
        if (warlock.Comp.PsychicShieldSegments.Count > 0)
            EndPsychicShield(warlock, false, false);
    }

    private void OnWarlockMobStateChanged(Entity<CMUXenoWarlockComponent> warlock, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        StopPsychicBlastChannel(warlock);
        StopPsychicCrushWindup(warlock);
        StopPsychicCrush(warlock, true);
        if (warlock.Comp.PsychicShieldSegments.Count > 0)
            EndPsychicShield(warlock, false, false);
    }

    // Displacement (thrown/knocked-back/lunged) ends the shield. MoveEvent already handles most
    // cases with a 0.25 s grace period, but ThrownEvent fires immediately at the start of a
    // throw regardless of grace, so it catches early-lunge cases too.
    private void OnWarlockThrown(Entity<CMUXenoWarlockComponent> warlock, ref ThrownEvent args)
    {
        if (warlock.Comp.PsychicShieldSegments.Count > 0)
            EndPsychicShield(warlock, false, false);
    }

    // Warlock-specific rejuvenate cleanup. Ends any active shield or channel and clears the
    // warlock's own next-use timers. Action button cooldowns are already cleared engine-side
    // by SharedActionsSystem.OnRejuventate for every action on the entity.
    private void OnWarlockRejuvenate(Entity<CMUXenoWarlockComponent> warlock, ref RejuvenateEvent args)
    {
        StopPsychicBlastChannel(warlock);
        StopPsychicCrushWindup(warlock);
        StopPsychicCrush(warlock, false);

        if (warlock.Comp.PsychicShieldSegments.Count > 0)
            EndPsychicShield(warlock, false, false);

        warlock.Comp.NextPsychicShieldAt = TimeSpan.Zero;
        warlock.Comp.NextPsychicCrushAt = TimeSpan.Zero;
        warlock.Comp.NextPsychicCrushPulseAt = TimeSpan.Zero;
    }

    private void SetActionToggled<T>(EntityUid warlock, bool toggled) where T : BaseActionEvent
    {
        foreach (Entity<ActionComponent> action in _rmcActions.GetActionsWithEvent<T>(warlock))
        {
            _actions.SetToggled((action, action), toggled);
        }
    }

    private void SetActionCooldown<T>(EntityUid warlock, TimeSpan cooldown) where T : BaseActionEvent
    {
        TimeSpan start = _timing.CurTime;
        TimeSpan end = start + cooldown;
        foreach (Entity<ActionComponent> action in _rmcActions.GetActionsWithEvent<T>(warlock))
        {
            Timer.Spawn(0, () => _actions.SetCooldown(action.AsNullable(), start, end));
        }
    }

    private void SpawnPsychicCrushWarnings(Entity<CMUXenoWarlockComponent> warlock, int pulse)
    {
        var originMap = _transform.ToMapCoordinates(warlock.Comp.PsychicCrushTarget);
        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushWarningOffsets(pulse))
        {
            EntityCoordinates tile = warlock.Comp.PsychicCrushTarget.Offset(new(offset.X, offset.Y));
            if (!IsPsychicCrushTileAffected(originMap, tile))
                continue;

            EntityUid warning = Spawn(warlock.Comp.PsychicCrushWarningId, tile);
            warlock.Comp.PsychicCrushWarnings.Add(warning);
        }
    }

    private void ClearPsychicCrushEffects(Entity<CMUXenoWarlockComponent> warlock)
    {
        if (warlock.Comp.PsychicCrushOrb is { } orb && !Deleted(orb))
            QueueDel(orb);

        warlock.Comp.PsychicCrushOrb = null;

        foreach (EntityUid warning in warlock.Comp.PsychicCrushWarnings)
        {
            if (!Deleted(warning))
                QueueDel(warning);
        }

        warlock.Comp.PsychicCrushWarnings.Clear();
    }

    private void RemovePsychicCrushMovementModifier(Entity<CMUXenoWarlockComponent> warlock)
    {
        RemCompDeferred<CMUXenoWarlockChannelingComponent>(warlock);
        _movement.RefreshMovementSpeedModifiers(warlock);
    }

    private void StartWarlockChannelEffect(Entity<CMUXenoWarlockComponent> warlock, CMUXenoWarlockChannelKind kind)
    {
        if (!CMUXenoWarlockSystem.ShouldShowWarlockChannelEffect(kind)
            || CMUXenoWarlockSystem.GetWarlockChannelEffect(warlock.Comp, kind) != null)
            return;

        EntProtoId prototype = CMUXenoWarlockSystem.GetWarlockChannelEffectPrototype(warlock.Comp, kind);
        EntityUid effect = SpawnAttachedTo(prototype, warlock.Owner.ToCoordinates());
        CMUXenoWarlockSystem.SetWarlockChannelEffect(warlock.Comp, kind, effect);
    }

    private void StopWarlockChannelEffect(Entity<CMUXenoWarlockComponent> warlock, CMUXenoWarlockChannelKind kind)
    {
        if (CMUXenoWarlockSystem.GetWarlockChannelEffect(warlock.Comp, kind) is not { } effect)
            return;

        if (!Deleted(effect))
            QueueDel(effect);

        CMUXenoWarlockSystem.SetWarlockChannelEffect(warlock.Comp, kind, null);
    }

    private static EntityUid? GetWarlockChannelEffect(CMUXenoWarlockComponent warlock, CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicCrush  => warlock.PsychicCrushChannelEffect,
            CMUXenoWarlockChannelKind.PsychicBlast  => warlock.PsychicBlastChannelEffect,
            CMUXenoWarlockChannelKind.PsychicShield => warlock.PsychicShieldChannelEffect, _ => null
        };
    }

    private static void SetWarlockChannelEffect(CMUXenoWarlockComponent warlock, CMUXenoWarlockChannelKind kind,
        EntityUid? effect)
    {
        switch (kind)
        {
            case CMUXenoWarlockChannelKind.PsychicCrush:
                warlock.PsychicCrushChannelEffect = effect;
                break;
            case CMUXenoWarlockChannelKind.PsychicBlast:
                warlock.PsychicBlastChannelEffect = effect;
                break;
            case CMUXenoWarlockChannelKind.PsychicShield:
                warlock.PsychicShieldChannelEffect = effect;
                break;
        }
    }

    private static EntProtoId
        GetWarlockChannelEffectPrototype(CMUXenoWarlockComponent warlock, CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicCrush  => warlock.PsychicCrushChannelEffectId,
            CMUXenoWarlockChannelKind.PsychicBlast  => warlock.PsychicBlastChannelEffectId,
            CMUXenoWarlockChannelKind.PsychicShield => warlock.PsychicShieldChannelEffectId,
            _                                       => warlock.PsychicCrushChannelEffectId
        };
    }

    private void OnChannelingRefreshSpeed(Entity<CMUXenoWarlockChannelingComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnPsychicShieldRootRefreshSpeed(Entity<CMUXenoPsychicShieldRootComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(CMUXenoWarlockSystem.GetPsychicShieldOwnerMoveSpeedMultiplier(),
            CMUXenoWarlockSystem.GetPsychicShieldOwnerMoveSpeedMultiplier());
    }

    // Mob collision would otherwise displace the warlock while the shield is up, and OnWarlockMove
    // then ends the shield once the 0.25 s grace expires - so any xeno bumping into the warlock
    // costs it the shield, the plasma and the cooldown. The boiler's bombard and the drone's
    // construction avoid this by setting RootEntity on their do-afters, which
    // DoAfterMobCollisionSystem turns into this same cancel. The shield holds its state without a
    // do-after, so it cancels the event directly off its root component, the way XenoRestSystem
    // and XenoDodgeSystem do for resting and dodging xenos.
    private void OnPsychicShieldRootAttemptMobCollide(Entity<CMUXenoPsychicShieldRootComponent> ent,
        ref AttemptMobCollideEvent args)
    {
        args.Cancelled = true;
    }

    private void ResetShieldProjectilePrediction(EntityUid projectile)
    {
        if (_net.IsServer)
        {
            RemComp<PredictedProjectileServerComponent>(projectile);
            return;
        }

        if (TryComp(projectile, out PredictedProjectileClientComponent? predicted))
            predicted.Hit = false;
    }

    public static int GetPsychicCrushDamage(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        return PsychicCrushBaseDamage + PsychicCrushDamagePerPulse * pulses;
    }

    public static FixedPoint2 GetPsychicCrushCost(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        return FixedPoint2.New(PsychicCrushPlasmaPerPulse * pulses);
    }

    public static int GetPsychicCrushResolvedPulses(int completedPulses)
        => Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);

    public static bool CanTriggerPsychicCrush(int completedPulses) => completedPulses > 1;

    public static TimeSpan GetPsychicCrushPulseInterval() => TimeSpan.FromSeconds(1);

    public static TimeSpan GetPsychicCrushWindupDuration() => TimeSpan.FromSeconds(0.8);

    public static TimeSpan GetPsychicCrushChannelDuration()
        => CMUXenoWarlockSystem.GetPsychicCrushChannelDuration(CMUXenoWarlockSystem.GetPsychicCrushPulseInterval());

    // One pulse longer than the number of expansion rings so the final outer ring is on screen
    // for a full pulse interval before detonation instead of being clipped on the same tick that
    // spawns it. With MaxAreaRadius = 3 and PulseInterval = 1s this gives a 4-second channel.
    public static TimeSpan GetPsychicCrushChannelDuration(TimeSpan pulseInterval)
        => pulseInterval * (PsychicCrushMaxAreaRadius + 1);

    public static bool ShouldPsychicCrushCancellationResolve() => true;

    public static bool ShouldPsychicCrushInterruptionResolve()
        => CMUXenoWarlockSystem.ShouldPsychicCrushCancellationResolve();

    public static bool ShouldSpawnPsychicCrushTileBlur(bool detonated) => true;

    public static string GetPsychicCrushBlurPrototype() => "CMUXenoPsychicCrushBlur";

    public static TimeSpan GetPsychicCrushBlurDuration() => TimeSpan.FromSeconds(1);

    public static bool ShouldPsychicCrushShowActionCooldown() => true;

    public static bool ShouldDeferWarlockActionCooldownUntilAfterActionPerformed() => true;

    public static TimeSpan GetPsychicCrushCooldownDuration() => TimeSpan.FromSeconds(15);

    public static float GetPsychicCrushTargetRange() => PsychicCrushTargetRangeValue;

    public static bool ShouldDeletePsychicBlastProjectileOnFixedDistanceStop(bool isClient, bool isClientSide)
        => !isClient || isClientSide;

    public static bool ShouldPsychicBlastIgnoreCollisionLayer(int collisionLayer)
        => collisionLayer == (int)CollisionGroup.GlassLayer || collisionLayer == (int)CollisionGroup.GlassAirlockLayer;

    public static float GetPsychicCrushRadius(int completedPulses)
        => CMUXenoWarlockSystem.GetPsychicCrushAreaRadius(completedPulses);

    public static int GetPsychicCrushAreaRadius(int completedPulses)
        => Math.Clamp(completedPulses, 0, PsychicCrushMaxAreaRadius);

    public static bool HasPsychicCrushReachedMaxRange(int completedPulses)
        => CMUXenoWarlockSystem.GetPsychicCrushAreaRadius(completedPulses) >= PsychicCrushMaxAreaRadius;

    // TGMC's stagger effect uses a stack-count that decays quickly, and RMC14's Daze is a heavier
    // effect than TGMC's Stagger, so the ported time values are cut roughly 4x to match the
    // effective severity of a max-charge crush in the original.
    public static TimeSpan GetPsychicCrushStaggerDuration(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        return TimeSpan.FromSeconds(0.5 * pulses);
    }

    // TGMC's add_slowdown(N) adds N decaying stacks, not a wall-clock duration. Cut roughly 3x so
    // the total slowdown wears off at a comparable pace.
    public static TimeSpan GetPsychicCrushSlowDuration(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        return TimeSpan.FromSeconds(1 * pulses);
    }

    // Returns TimeSpan.Zero for pulses below the high-pulse threshold, meaning no Paralyze is
    // applied at low charge. At threshold and above, the crush forces the victim prone.
    public static TimeSpan GetPsychicCrushParalyzeDuration(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        if (pulses < PsychicCrushHighPulseParalyzeThreshold)
            return TimeSpan.Zero;
        return TimeSpan.FromSeconds(1.5);
    }

    public static TimeSpan GetPsychicBlastChargeDuration() => TimeSpan.FromSeconds(1);

    public static string GetPsychicBlastBeamPrototype() => "CMUXenoPsychicBlastProjectile";

    public static bool ShouldShowWarlockChannelEffect(CMUXenoWarlockChannelKind kind)
        => kind is CMUXenoWarlockChannelKind.PsychicCrush
            or CMUXenoWarlockChannelKind.PsychicBlast
            or CMUXenoWarlockChannelKind.PsychicShield;

    public static string GetWarlockChannelColor(CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast => "#970f0f",
            CMUXenoWarlockChannelKind.PsychicCrush => "#6a59b3",
            CMUXenoWarlockChannelKind.PsychicShield => "#5999b3", _ => "#ffffff"
        };
    }

    public static bool ShouldSpawnWarlockChannelStream(CMUXenoWarlockChannelKind kind) => false;

    public static string GetWarlockChannelParticlePrototype(CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast => "CMUXenoWarlockBlastParticles",
            CMUXenoWarlockChannelKind.PsychicCrush => "CMUXenoWarlockCrushParticles",
            _                                      => "CMUXenoWarlockCrushParticles"
        };
    }

    private static EntProtoId GetWarlockChannelParticlePrototype(CMUXenoWarlockComponent warlock,
        CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast => warlock.PsychicBlastChannelParticleId,
            CMUXenoWarlockChannelKind.PsychicCrush => warlock.PsychicCrushChannelParticleId,
            _                                      => warlock.PsychicCrushChannelParticleId
        };
    }

    public static CMUXenoWarlockParticleProfile GetWarlockParticleProfile(CMUXenoWarlockParticleEffect effect)
    {
        return effect switch
        {
            CMUXenoWarlockParticleEffect.PsychicBlastCharge => new("#970f0f",
                300,
                20,
                12,
                12,
                -0.02f,
                Vector2.Zero,
                Vector2.Zero,
                Vector2.Zero,
                Vector2.Zero,
                new(15, 17),
                new(0.1f, 0.1f),
                new(0.5f, 0.5f),
                new(16, 0)),
            CMUXenoWarlockParticleEffect.CrushWarning => new("#4b3f7e",
                50,
                5,
                8,
                10,
                -0.04f,
                new(0, 0.2f),
                new(0, 0.6f),
                new(-0.5f, -0.5f),
                new(0.5f, 0.5f),
                new(15, 17),
                new(0.3f, 0.3f),
                new(0.7f, 0.7f),
                Vector2.Zero),
            CMUXenoWarlockParticleEffect.DroneOperatorTransfer => new("#6eb8ff",
                28,
                3,
                16,
                10,
                -0.003f,
                new(0, 0.04f),
                new(0, 0.02f),
                new(-0.05f, -0.04f),
                new(0.05f, 0.08f),
                new(4, 11),
                new(0.08f, 0.08f),
                new(0.18f, 0.18f),
                Vector2.Zero),
            CMUXenoWarlockParticleEffect.DroneAndroidDormant => new("#d44848",
                24,
                2,
                18,
                14,
                -0.003f,
                new(0, 0.02f),
                new(0, 0.015f),
                new(-0.04f, -0.03f),
                new(0.04f, 0.05f),
                new(3, 9),
                new(0.07f, 0.07f),
                new(0.16f, 0.16f),
                Vector2.Zero),
            CMUXenoWarlockParticleEffect.DroneTransferConnect => new("#6eb8ff",
                48,
                8,
                6,
                5,
                -0.006f,
                new(0, 0.2f),
                Vector2.Zero,
                new(-0.03f, -0.03f),
                new(0.03f, 0.03f),
                new(1, 3),
                new(0.07f, 0.07f),
                new(0.16f, 0.16f),
                Vector2.Zero,
                1100f),
            CMUXenoWarlockParticleEffect.DroneTransferDisconnect => new("#d44848",
                48,
                8,
                6,
                5,
                -0.006f,
                new(0, 0.2f),
                Vector2.Zero,
                new(-0.03f, -0.03f),
                new(0.03f, 0.03f),
                new(1, 3),
                new(0.07f, 0.07f),
                new(0.16f, 0.16f),
                Vector2.Zero,
                1100f),
            _ => new("#6a59b3",
                300,
                15,
                8,
                12,
                -0.02f,
                new(0, 3),
                new(0, 3),
                new(0, -0.5f),
                new(0, 0.2f),
                new(15, 17),
                new(0.1f, 0.1f),
                new(0.5f, 0.5f),
                new(16, 5))
        };
    }

    public static Vector2 GetWarlockParticleRenderOffset(CMUXenoWarlockParticleEffect effect)
    {
        if (effect is CMUXenoWarlockParticleEffect.PsychicBlastCharge
            or CMUXenoWarlockParticleEffect.PsychicCrushCharge)
            return Vector2.Zero;

        return CMUXenoWarlockSystem.GetWarlockParticleProfile(effect).HolderOffset;
    }

    public static CMUXenoWarlockParticleMotion? GetWarlockDirectedParticleMotion(Vector2 origin, Vector2 target,
        float velocity)
    {
        if (velocity <= 0f)
            return null;

        Vector2 direction = target - origin;
        float length = direction.Length();
        if (length <= 0f)
            return null;

        Vector2 normalized = direction / length;
        return new CMUXenoWarlockParticleMotion(normalized * (velocity * 0.5f), normalized * velocity);
    }

    public bool TrySetWarlockDirectedParticleMotion(
        Entity<CMUXenoWarlockParticleEmitterComponent> particles,
        Vector2 origin,
        Vector2 target,
        float velocity)
    {
        CMUXenoWarlockParticleMotion? motion = CMUXenoWarlockSystem.GetWarlockDirectedParticleMotion(
            origin,
            target,
            velocity);
        if (motion == null)
            return false;

        particles.Comp.UseMotionOverride = true;
        particles.Comp.MotionVelocity = motion.Value.Velocity;
        particles.Comp.MotionGravity = motion.Value.Gravity;
        Dirty(particles);
        return true;
    }

    public static string GetWarlockChannelLightPrototype(CMUXenoWarlockChannelKind kind)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast  => "CMUXenoWarlockBlastChannelEffect",
            CMUXenoWarlockChannelKind.PsychicCrush  => "CMUXenoWarlockCrushChannelEffect",
            CMUXenoWarlockChannelKind.PsychicShield => "CMUXenoWarlockShieldChannelEffect",
            _                                       => "CMUXenoWarlockCrushChannelEffect"
        };
    }

    public static string GetPsychicBlastImpactEffectPrototype() => "CMUXenoPsychicBlastShockwave";

    public static string GetPsychicBlastFireSoundPath() => PsychicBlastFireSoundPath;

    public static string GetPsychicBlastImpactSoundPath() => PsychicBlastImpactSoundPath;

    public static bool ShouldPsychicBlastPlayFireSoundFromWarlockSystem() => true;

    public static bool ShouldPsychicBlastUsePvsAudio() => true;

    public static bool ShouldPsychicBlastKnockbackAffectedTargets() => true;

    public static float GetPsychicBlastKnockbackSpeed() => PsychicBlastKnockbackSpeed;

    public static Vector2 GetPsychicBlastKnockbackDirection(Vector2 impact, Vector2 target, Vector2 fallback)
    {
        Vector2 direction = target - impact;
        if (direction.LengthSquared() <= 0.0001f)
            direction = fallback;

        if (direction.LengthSquared() <= 0.0001f)
            return Vector2.Zero;

        return Vector2.Normalize(direction);
    }

    public static string GetPsychicCrushEndEffectPrototype(bool detonated)
        => detonated ? "CMUXenoPsychicCrushHard" : "CMUXenoPsychicCrushSmooth";

    public static int GetPsychicCrushEndEffectCount(bool detonated, int completedPulses) => 1;

    public static CMUDrawDepth GetPsychicCrushOrbDrawDepth() => CMUDrawDepth.Overlays;

    public static FixedPoint2 GetPsychicShieldCost() => FixedPoint2.New(PsychicShieldPlasmaCost);

    public static FixedPoint2 GetPsychicShieldDetonationCost() => FixedPoint2.New(PsychicShieldDetonationPlasmaCost);

    public static TimeSpan GetPsychicShieldDuration() => TimeSpan.FromSeconds(6);

    public static TimeSpan GetPsychicShieldCooldownDuration() => TimeSpan.FromSeconds(10);

    public static FixedPoint2 GetPsychicShieldIntegrity() => FixedPoint2.New(PsychicShieldIntegrityValue);

    public static int GetPsychicShieldMaxFrozenProjectiles() => PsychicShieldMaxFrozenProjectilesValue;

    public static TimeSpan GetPsychicShieldBreakStunDuration() => TimeSpan.FromSeconds(1);

    public static bool ShouldPsychicShieldOwnerChannelDrawShieldSprite() => false;

    public static bool ShouldPsychicShieldFreezeIncomingProjectiles() => true;

    public static bool ShouldPsychicShieldReleaseProjectilesOnCancel() => true;

    public static bool ShouldPsychicShieldReflectProjectilesOnManualDetonation() => true;

    public static bool ShouldPsychicShieldReleaseProjectilesAndStunOwnerOnBreak() => true;

    public static bool ShouldPsychicShieldRestoreOriginalProjectileOnBreak() => true;

    public static bool ShouldPsychicShieldDisableFrozenProjectileCollision() => true;

    public static bool ShouldPsychicShieldRestoreFrozenProjectileCollision() => true;

    public static bool ShouldPsychicShieldUseHardProjectileCollision() => true;

    public static bool ShouldPsychicShieldCatchProjectilesBeforeProjectileSystems() => true;

    public static bool ShouldPsychicShieldSubscribeToProjectilePreventCollide() => false;

    public static bool ShouldPsychicShieldSuspendDeleteOnCollideComponent() => true;

    public static bool ShouldPsychicShieldSuspendFixedDistanceProjectileLifetime() => true;

    public static bool ShouldPsychicShieldBreakFromFrozenProjectiles(int frozenProjectiles, int maxFrozenProjectiles)
        => maxFrozenProjectiles > 0 && frozenProjectiles >= maxFrozenProjectiles;

    public static bool ShouldPsychicShieldRootOwnerWhileActive() => true;

    public static float GetPsychicShieldOwnerMoveSpeedMultiplier() => 0f;

    public static bool ShouldPlayPsychicShieldReflectSoundAtShield() => true;

    public static bool ShouldPsychicShieldRequireClearForwardTile() => true;

    public static bool ShouldPsychicShieldShowActionCooldown() => true;

    public static bool ShouldPsychicShieldApplyAuthoritativeFreezeSideEffects(bool isClient) => !isClient;

    public static bool ShouldPsychicShieldApplyMoveCancel(bool isClient) => !isClient;

    public static bool ShouldPsychicShieldCancelOnMove(Vector2 oldPosition, Vector2 newPosition, bool parentChanged)
        => parentChanged || !oldPosition.EqualsApprox(newPosition, 0.001f);

    public static bool ShouldPsychicShieldCancelOnMove(Vector2 oldPosition,
        Vector2 newPosition,
        bool parentChanged,
        TimeSpan currentTime,
        TimeSpan graceUntil)
        => currentTime >= graceUntil
            && CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(oldPosition, newPosition, parentChanged);

    public static bool ShouldPsychicShieldUseUnanchoredWorldPlacement() => true;

    public static bool ShouldPsychicShieldSnapToGrid() => false;

    public static bool ShouldOffsetPsychicShieldSpriteWithoutMovingCollision() => false;

    public static Vector2 GetPsychicShieldVisualOffset(Direction direction) => Vector2.Zero;

    public static float GetPsychicShieldAlpha(FixedPoint2 remainingIntegrity, FixedPoint2 maxIntegrity)
    {
        if (maxIntegrity <= FixedPoint2.Zero)
            return 0f;

        return Math.Clamp(remainingIntegrity.Float() / maxIntegrity.Float(), 0f, 1f);
    }

    public static IEnumerable<Vector2> GetPsychicShieldOffsets(Direction direction)
        => [CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(direction)];

    public static Vector2 GetPsychicShieldCenterOffset(Direction direction)
    {
        const float nearEdgeDistance = 0.5f;
        float centerDistance = nearEdgeDistance + PsychicShieldHalfThickness;
        return direction switch
        {
            Direction.North => new(0, centerDistance), Direction.South => new(0, -centerDistance),
            Direction.East  => new(centerDistance, 0), Direction.West  => new(-centerDistance, 0),
            _               => new(0, centerDistance)
        };
    }

    public static Vector2 GetPsychicShieldFrozenProjectilePosition(Vector2 shieldPosition,
        Vector2 projectilePosition,
        Direction direction)
    {
        float faceDistance = PsychicShieldHalfThickness + PsychicShieldProjectileStopOffset;
        return direction switch
        {
            Direction.North => new(Math.Clamp(projectilePosition.X, shieldPosition.X - PsychicShieldHalfWidth,
                    shieldPosition.X + PsychicShieldHalfWidth),
                shieldPosition.Y + faceDistance),
            Direction.South => new(Math.Clamp(projectilePosition.X, shieldPosition.X - PsychicShieldHalfWidth,
                    shieldPosition.X + PsychicShieldHalfWidth),
                shieldPosition.Y - faceDistance),
            Direction.East => new(shieldPosition.X + faceDistance,
                Math.Clamp(projectilePosition.Y, shieldPosition.Y - PsychicShieldHalfWidth,
                    shieldPosition.Y + PsychicShieldHalfWidth)),
            Direction.West => new(shieldPosition.X - faceDistance,
                Math.Clamp(projectilePosition.Y, shieldPosition.Y - PsychicShieldHalfWidth,
                    shieldPosition.Y + PsychicShieldHalfWidth)),
            _ => projectilePosition
        };
    }

    private static Vector2 GetPsychicShieldObstructionCheckOffset(Direction direction)
    {
        return direction switch
        {
            Direction.North => Vector2.UnitY, Direction.South => -Vector2.UnitY, Direction.East => Vector2.UnitX,
            Direction.West  => -Vector2.UnitX, _              => Vector2.UnitY
        };
    }


    public static IEnumerable<Vector2i> GetPsychicCrushAffectedOffsets(int completedPulses)
    {
        int radius = CMUXenoWarlockSystem.GetPsychicCrushAreaRadius(completedPulses);
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (Math.Abs(x) + Math.Abs(y) <= radius)
                    yield return new(x, y);
            }
        }
    }

    public static IEnumerable<Vector2i> GetPsychicCrushWarningOffsets(int completedPulses)
    {
        int radius = CMUXenoWarlockSystem.GetPsychicCrushAreaRadius(completedPulses);
        if (radius == 0)
        {
            yield return Vector2i.Zero;
            yield break;
        }

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (Math.Abs(x) + Math.Abs(y) == radius)
                    yield return new(x, y);
            }
        }
    }

    public static IEnumerable<Vector2i> GetPsychicShieldBlastOffsets(Direction direction)
    {
        for (var depth = 1; depth <= 2; depth++)
        {
            for (int lateral = -1; lateral <= 1; lateral++)
            {
                yield return direction switch
                {
                    Direction.North => new(lateral, depth), Direction.South => new(lateral, -depth),
                    Direction.East  => new(depth, lateral), Direction.West  => new(-depth, lateral),
                    _               => new(lateral, depth)
                };
            }
        }
    }

    public static Vector2 ReflectProjectileVelocity(Vector2 velocity, Direction shieldDirection)
    {
        return shieldDirection switch
        {
            Direction.North or Direction.South => new(velocity.X, -velocity.Y),
            Direction.East or Direction.West   => new(-velocity.X, velocity.Y), _ => -velocity
        };
    }

    public static bool IsProjectileIncomingFromFront(Vector2 velocity, Direction shieldDirection)
    {
        if (velocity.LengthSquared() <= 0f)
            return false;

        // The shield blocks a projectile whose velocity points into the shield's front face,
        // i.e. opposite to the shield's outward normal. Dot product < 0 means the angle between
        // the projectile's velocity and the shield normal is greater than 90 degrees, which is
        // the same 90 degree acceptance cone as the old angle math but without dependency on
        // whichever radian convention Direction.ToAngle / Vector2.ToWorldAngle happen to use.
        Vector2 shieldNormal = shieldDirection.ToVec();
        return Vector2.Dot(velocity, shieldNormal) < 0f;
    }

    public static FixedPoint2 GetPlasmaTransferAmount(FixedPoint2 requested,
        FixedPoint2 donorPlasma,
        FixedPoint2 targetPlasma,
        FixedPoint2 targetMaxPlasma)
    {
        if (requested <= FixedPoint2.Zero || donorPlasma <= FixedPoint2.Zero || targetMaxPlasma <= targetPlasma)
            return FixedPoint2.Zero;

        FixedPoint2 targetMissing = targetMaxPlasma - targetPlasma;
        return FixedPoint2.Min(requested, FixedPoint2.Min(donorPlasma, targetMissing));
    }
}
