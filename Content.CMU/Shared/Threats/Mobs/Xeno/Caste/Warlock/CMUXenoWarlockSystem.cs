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
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using CMUDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;

public enum CMUXenoPsychicBlastMode : byte
{
    Blast,
    Lance
}

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
    public FixedPoint2 PsychicBlastCost = 75;

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
    public CMUXenoPsychicBlastMode PsychicBlastMode = CMUXenoPsychicBlastMode.Blast;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicBlastProjectileId = "CMUXenoPsychicBlastProjectile";

    [DataField, AutoNetworkedField]
    public float PsychicBlastProjectileSpeed = 28f;

    [DataField, AutoNetworkedField]
    public float PsychicBlastRadius = 1.25f;

    [DataField, AutoNetworkedField]
    public float PsychicBlastRange = 7f;

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
    public float PsychicCrushChannelSpeedMultiplier = 0.3f;

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicCrushCooldown = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushDetonateId = "CMUXenoPsychicCrushHard";

    public EntityUid? PsychicCrushOrb;

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicCrushOrbId = "CMUXenoPsychicCrushOrb";

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicCrushOwnerSlowDuration = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public FixedPoint2 PsychicCrushPulseCost = FixedPoint2.New(CMUXenoWarlockSystem.PsychicCrushPlasmaPerPulse);

    [DataField, AutoNetworkedField]
    public TimeSpan PsychicCrushPulseInterval = TimeSpan.FromSeconds(1.75);

    public int PsychicCrushPulses;

    [DataField, AutoNetworkedField]
    public SoundSpecifier
        PsychicCrushPulseSound = new SoundPathSpecifier("/Audio/_CMU14/Xeno/Warlock/woosh_swoosh.ogg");

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
    public EntProtoId PsychicLanceChannelParticleId = "CMUXenoWarlockLanceParticles";

    [DataField, AutoNetworkedField]
    public EntProtoId PsychicLanceProjectileId = "CMUXenoPsychicLanceProjectile";

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

    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;

    [DataField, AutoNetworkedField]
    public Vector2 Velocity;

    [DataField, AutoNetworkedField]
    public EntityUid? Weapon;
}

public sealed partial class CMUXenoPsychicBlastActionEvent : WorldTargetActionEvent;
public sealed partial class CMUXenoPsychicCrushActionEvent : WorldTargetActionEvent;
public sealed partial class CMUXenoPsychicShieldActionEvent : InstantActionEvent;

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
    public CMUXenoPsychicBlastMode Mode;

    [DataField]
    public NetCoordinates TargetCoordinates;

    public CMUXenoPsychicBlastDoAfterEvent(NetCoordinates targetCoordinates, CMUXenoPsychicBlastMode mode)
    {
        TargetCoordinates = targetCoordinates;
        Mode = mode;
    }

    public override DoAfterEvent Clone() => new CMUXenoPsychicBlastDoAfterEvent(TargetCoordinates, Mode);
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

    [DataField, AutoNetworkedField]
    public CMUXenoPsychicBlastMode Mode = CMUXenoPsychicBlastMode.Blast;

    [DataField, AutoNetworkedField]
    public float Radius = 1.25f;

    [DataField, AutoNetworkedField]
    public TimeSpan Slow = TimeSpan.FromSeconds(1.5);

    public bool Triggered;
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
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private XenoProjectileSystem _xenoProjectile = default!;
    private static readonly FixedPoint2 PsychicCrushVehicleDamageMultiplier = FixedPoint2.New(0.5f);
    private static readonly FixedPoint2 PsychicCrushMechDamageMultiplier = FixedPoint2.New(2.3f);
    public const string PsychicBlastFireSoundPath = "/Audio/_CMU14/Xeno/Warlock/volkite_4.ogg";
    public const string PsychicBlastImpactSoundPath = "/Audio/_CMU14/Xeno/Warlock/EMPulse.ogg";
    public const int PsychicCrushBaseDamage = 65;
    public const int PsychicCrushDamagePerPulse = 35;
    public const int PsychicCrushMaxAreaRadius = 2;
    public const int PsychicCrushMaxPulses = 5;
    public const int PsychicCrushPlasmaPerPulse = 40;
    public const float PsychicCrushTargetRangeValue = 9f;
    public const float PsychicBlastKnockbackSpeed = 8f;
    public const int PsychicShieldIntegrityValue = 650;
    public const int PsychicShieldMaxFrozenProjectilesValue = 10;
    public const int PsychicShieldPlasmaCost = 200;
    public const float PsychicShieldHalfThickness = 0.5f;
    public const float PsychicShieldHalfWidth = 1.5f;
    public const float PsychicShieldProjectileStopOffset = 0.1f;
    public const float PsychicShieldReflectionSpreadDegrees = 80f;

    private const float WarlockDirectedParticleVelocity = 7f;
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
        SubscribeLocalEvent<CMUXenoWarlockComponent, MoveEvent>(OnWarlockMove);
        SubscribeLocalEvent<CMUXenoWarlockComponent, StunnedEvent>(OnWarlockStunned);
        SubscribeLocalEvent<CMUXenoWarlockComponent, KnockedDownEvent>(OnWarlockKnockedDown);
        SubscribeLocalEvent<CMUXenoWarlockComponent, MobStateChangedEvent>(OnWarlockMobStateChanged);

        SubscribeLocalEvent<CMUXenoWarlockChannelingComponent, RefreshMovementSpeedModifiersEvent>(
            OnChannelingRefreshSpeed);
        SubscribeLocalEvent<CMUXenoPsychicShieldRootComponent, RefreshMovementSpeedModifiersEvent>(
            OnPsychicShieldRootRefreshSpeed);
        SubscribeLocalEvent<CMUXenoPsychicBlastProjectileComponent, ProjectileHitEvent>(OnPsychicBlastProjectileHit);
        SubscribeLocalEvent<CMUXenoPsychicBlastProjectileComponent, ProjectileFixedDistanceStopEvent>(
            OnPsychicBlastProjectileFixedDistanceStop);
        SubscribeLocalEvent<CMUXenoPsychicBlastProjectileComponent, PreventCollideEvent>(
            OnPsychicBlastProjectilePreventCollide);
        SubscribeLocalEvent<CMUXenoPsychicShieldSegmentComponent, PreventCollideEvent>(
            OnShieldProjectilePreventCollide);
        SubscribeLocalEvent<CMUXenoPsychicShieldSegmentComponent, ProjectileReflectAttemptEvent>(
            OnShieldProjectileReflectAttempt);
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

            if (warlock.PsychicShieldSegments.Count > 0 && time >= warlock.PsychicShieldExpiresAt)
                EndPsychicShield(ent, false, false);
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
        if (args.Handled
            || warlock.Comp.PsychicBlastChanneling
            || !_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null), warlock.Comp.PsychicBlastCost))
            return;

        args.Handled = true;
        warlock.Comp.PsychicBlastChanneling = true;
        warlock.Comp.PsychicBlastTarget = args.Target;
        StartWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicBlast);
        StartWarlockChannelParticles(warlock, CMUXenoWarlockChannelKind.PsychicBlast, args.Target,
            warlock.Comp.PsychicBlastMode);
        SetActionToggled<CMUXenoPsychicBlastActionEvent>(warlock, true);

        var ev = new CMUXenoPsychicBlastDoAfterEvent(GetNetCoordinates(args.Target), warlock.Comp.PsychicBlastMode);
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
        FirePsychicBlastProjectile(warlock, target, args.Mode);
    }

    private void FirePsychicBlastProjectile(Entity<CMUXenoWarlockComponent> warlock,
        EntityCoordinates target,
        CMUXenoPsychicBlastMode mode)
    {
        MapCoordinates origin = _transform.GetMapCoordinates(warlock);
        var targetMap = _transform.ToMapCoordinates(target);
        if (origin.MapId != targetMap.MapId)
            return;

        Vector2 direction = targetMap.Position - origin.Position;
        if (direction.LengthSquared() <= 0f)
            return;

        float distance = Math.Min(direction.Length(), warlock.Comp.PsychicBlastRange);
        EntProtoId projectileId = mode == CMUXenoPsychicBlastMode.Lance
            ? warlock.Comp.PsychicLanceProjectileId
            : warlock.Comp.PsychicBlastProjectileId;

        bool shot = _xenoProjectile.TryShoot(warlock,
            target,
            FixedPoint2.Zero,
            projectileId,
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
            Vector2 direction = CMUXenoWarlockSystem.GetPsychicBlastKnockbackDirection(mapCoords.Position,
                _transform.GetMapCoordinates(target).Position,
                projectileVelocity);
            if (direction != Vector2.Zero)
            {
                _throwing.TryThrow(target, direction, projectile.Comp.KnockbackSpeed, shooter, animated: false,
                    playSound: false, compensateFriction: true);
            }
        }
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

        if (!CanKeepPsychicCrushTarget(warlock, args.Target))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-warlock-psychic-crush-invalid-target"), warlock, warlock,
                PopupType.SmallCaution);
            return;
        }

        StartPsychicCrushWindup(warlock, args.Target, args.Action);
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

        if (warlock.Comp.PsychicShieldSegments.Count > 0)
        {
            if (!_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null), warlock.Comp.PsychicShieldCost))
                return;

            DetonatePsychicShield(warlock);
            args.Handled = true;
            return;
        }

        if (_timing.CurTime < warlock.Comp.NextPsychicShieldAt)
            return;

        if (!CanStartPsychicShield(warlock))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-warlock-psychic-shield-obstructed"),
                warlock,
                warlock,
                PopupType.SmallCaution);
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null), warlock.Comp.PsychicShieldCost)) return;

        StartPsychicShield(warlock);
        args.Handled = true;
    }

    private bool CanStartPsychicShield(Entity<CMUXenoWarlockComponent> warlock)
    {
        Direction direction = _transform.GetWorldRotation(warlock).GetCardinalDir();
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
        StartWarlockChannelParticles(warlock, CMUXenoWarlockChannelKind.PsychicCrush, target,
            warlock.Comp.PsychicBlastMode);
        SetActionToggled<CMUXenoPsychicCrushActionEvent>(warlock, true);

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

        if (!_doAfter.TryStartDoAfter(doAfter))
            ResolvePsychicCrush(warlock, true, true);
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

        if (!_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null), warlock.Comp.PsychicCrushPulseCost))
        {
            StopPsychicCrush(warlock, true);
            return;
        }

        warlock.Comp.PsychicCrushPulses++;
        warlock.Comp.NextPsychicCrushPulseAt = _timing.CurTime + warlock.Comp.PsychicCrushPulseInterval;
        SpawnPsychicCrushWarnings(warlock, warlock.Comp.PsychicCrushPulses);
        _audio.PlayPredicted(warlock.Comp.PsychicCrushPulseSound,
            warlock.Comp.PsychicCrushTarget,
            warlock,
            AudioParams.Default.WithVolume(-2f + warlock.Comp.PsychicCrushPulses));

        if (CMUXenoWarlockSystem.HasPsychicCrushReachedMaxRange(warlock.Comp.PsychicCrushPulses))
        {
            warlock.Comp.PsychicCrushPulses = PsychicCrushMaxPulses;
            TriggerPsychicCrush(warlock);
        }
    }

    private void TriggerPsychicCrush(Entity<CMUXenoWarlockComponent> warlock)
    {
        if (!CMUXenoWarlockSystem.CanTriggerPsychicCrush(warlock.Comp.PsychicCrushPulses))
            return;

        int pulses = CMUXenoWarlockSystem.GetPsychicCrushResolvedPulses(warlock.Comp.PsychicCrushPulses);

        if (!CanKeepPsychicCrushTarget(warlock, warlock.Comp.PsychicCrushTarget)
            || !_xenoPlasma.TryRemovePlasmaPopup((warlock.Owner, null),
                CMUXenoWarlockSystem.GetPsychicCrushCost(pulses)))
        {
            StopPsychicCrush(warlock, true);
            return;
        }

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

        _audio.PlayPredicted(warlock.Comp.PsychicCrushTriggerSound, warlock.Comp.PsychicCrushTarget, warlock);
        SpawnPsychicCrushEndEffect(warlock, detonated);
        SpawnPsychicCrushBlur(warlock, areaPulses);
        ApplyPsychicCrushDamage(warlock, areaPulses,
            CMUXenoWarlockSystem.GetPsychicCrushResolvedPulses(warlock.Comp.PsychicCrushPulses));
        _slow.TrySlowdown(warlock.Owner, warlock.Comp.PsychicCrushOwnerSlowDuration, ignoreDurationModifier: true);
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

        _affected.Clear();
        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(areaPulses))
        {
            EntityCoordinates coords = warlock.Comp.PsychicCrushTarget.Offset(new(offset.X, offset.Y));
            var mapCoords = _transform.ToMapCoordinates(coords);
            foreach ((EntityUid target, MobStateComponent state) in _lookup.GetEntitiesInRange<MobStateComponent>(
                mapCoords, 0.45f))
            {
                if (target == warlock.Owner
                    || _mobState.IsDead(target, state)
                    || !_xeno.CanAbilityAttackTarget(warlock.Owner, target))
                    continue;

                if (!_affected.Add(target))
                    continue;

                _damageable.TryChangeDamage(target, mobDamage, origin: warlock, tool: warlock);
                _daze.TryDaze(target, CMUXenoWarlockSystem.GetPsychicCrushStaggerDuration(damagePulses), true,
                    stutter: true);
                _slow.TrySlowdown(target, CMUXenoWarlockSystem.GetPsychicCrushSlowDuration(damagePulses),
                    ignoreDurationModifier: true);
            }
        }

        var vehicleDamage = new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = damageAmount }
        };

        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(areaPulses))
        {
            EntityCoordinates coords = warlock.Comp.PsychicCrushTarget.Offset(new(offset.X, offset.Y));
            var mapCoords = _transform.ToMapCoordinates(coords);
            foreach ((EntityUid target, DamageableComponent _) in _lookup.GetEntitiesInRange<DamageableComponent>(
                mapCoords, 0.45f))
            {
                if (_affected.Contains(target))
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
    }

    private void FinishPsychicCrush(Entity<CMUXenoWarlockComponent> warlock, bool setCooldown)
    {
        warlock.Comp.PsychicCrushChanneling = false;
        warlock.Comp.PsychicCrushWindingUp = false;
        warlock.Comp.PsychicCrushPulses = 0;
        warlock.Comp.NextPsychicCrushPulseAt = TimeSpan.Zero;
        SetActionToggled<CMUXenoPsychicCrushActionEvent>(warlock, false);
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

    private void SpawnPsychicCrushBlur(Entity<CMUXenoWarlockComponent> warlock, int areaPulses)
    {
        if (!CMUXenoWarlockSystem.ShouldSpawnPsychicCrushTileBlur(true))
            return;

        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(areaPulses))
        {
            Spawn(warlock.Comp.PsychicCrushBlurId, warlock.Comp.PsychicCrushTarget.Offset(new(offset.X, offset.Y)));
        }
    }

    private void StartWarlockChannelParticles(Entity<CMUXenoWarlockComponent> warlock,
        CMUXenoWarlockChannelKind kind,
        EntityCoordinates target,
        CMUXenoPsychicBlastMode mode)
    {
        if (CMUXenoWarlockSystem.GetWarlockChannelParticle(warlock.Comp, kind) != null)
            return;

        EntProtoId prototype = CMUXenoWarlockSystem.GetWarlockChannelParticlePrototype(warlock.Comp, kind, mode);
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

    private void StartPsychicShield(Entity<CMUXenoWarlockComponent> warlock)
    {
        warlock.Comp.PsychicShieldDirection = _transform.GetWorldRotation(warlock).GetCardinalDir();
        warlock.Comp.PsychicShieldIntegrityRemaining = warlock.Comp.PsychicShieldIntegrity;
        warlock.Comp.PsychicShieldExpiresAt = _timing.CurTime + warlock.Comp.PsychicShieldDuration;
        warlock.Comp.PsychicShieldMoveCancelGraceUntil = _timing.CurTime + warlock.Comp.PsychicShieldMoveCancelGrace;
        _audio.PlayPredicted(warlock.Comp.PsychicShieldStartSound, warlock, warlock);
        StartWarlockChannelEffect(warlock, CMUXenoWarlockChannelKind.PsychicShield);
        SetActionToggled<CMUXenoPsychicShieldActionEvent>(warlock, true);
        EnsureComp<CMUXenoPsychicShieldRootComponent>(warlock);
        _movement.RefreshMovementSpeedModifiers(warlock);
        if (TryComp(warlock, out PhysicsComponent? physics))
            _physics.SetLinearVelocity(warlock, Vector2.Zero, body: physics);

        EntityCoordinates origin = _transform.GetMoverCoordinates(warlock);
        EntityUid shield = Spawn(warlock.Comp.PsychicShieldVisualId,
            origin.Offset(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(warlock.Comp.PsychicShieldDirection)));
        _transform.SetWorldRotationNoLerp(shield, warlock.Comp.PsychicShieldDirection.ToAngle());
        _rmcSprite.SetOffset(shield,
            CMUXenoWarlockSystem.GetPsychicShieldVisualOffset(warlock.Comp.PsychicShieldDirection));

        var comp = EnsureComp<CMUXenoPsychicShieldSegmentComponent>(shield);
        comp.Warlock = warlock;
        comp.Direction = warlock.Comp.PsychicShieldDirection;
        Dirty(shield, comp);
        warlock.Comp.PsychicShieldSegments.Add(shield);
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
        SetActionToggled<CMUXenoPsychicShieldActionEvent>(warlock, false);
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

        foreach (EntityUid projectile in warlock.Comp.FrozenProjectiles)
        {
            if (!TryComp(projectile, out CMUXenoFrozenProjectileComponent? frozen))
                continue;

            Vector2 reflected = CMUXenoWarlockSystem.ReflectProjectileVelocity(frozen.Velocity,
                warlock.Comp.PsychicShieldDirection,
                _random.NextAngle(-Angle.FromDegrees(PsychicShieldReflectionSpreadDegrees / 2f),
                    Angle.FromDegrees(PsychicShieldReflectionSpreadDegrees / 2f)));
            if (TryComp(projectile, out PhysicsComponent? physics))
                RestoreFrozenProjectilePhysics(projectile, frozen, reflected, physics);

            RemComp<CMUXenoFrozenProjectileComponent>(projectile);

            Angle projectileAngle = Angle.Zero;
            if (TryComp(projectile, out ProjectileComponent? projectileComp))
            {
                projectileComp.Shooter = warlock;
                projectileComp.Weapon = warlock;
                projectileComp.IgnoreShooter = false;
                projectileComp.DeleteOnCollide = frozen.DeleteOnCollide;
                projectileComp.ProjectileSpent = false;
                Dirty(projectile, projectileComp);
                projectileAngle = projectileComp.Angle;
            }

            RestoreFrozenProjectileDeleteOnCollide(projectile, frozen);
            _transform.SetWorldRotationNoLerp(projectile, reflected.ToWorldAngle() + projectileAngle);
            ResetShieldProjectilePrediction(projectile);
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

    private bool TryFreezeShieldProjectile(Entity<CMUXenoPsychicShieldSegmentComponent> segment,
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
        foreach (Vector2i offset in CMUXenoWarlockSystem.GetPsychicCrushWarningOffsets(pulse))
        {
            EntityUid warning = Spawn(warlock.Comp.PsychicCrushWarningId,
                warlock.Comp.PsychicCrushTarget.Offset(new(offset.X, offset.Y)));
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

    public static TimeSpan GetPsychicCrushPulseInterval() => TimeSpan.FromSeconds(1.75);

    public static TimeSpan GetPsychicCrushWindupDuration() => TimeSpan.FromSeconds(0.8);

    public static TimeSpan GetPsychicCrushChannelDuration()
        => CMUXenoWarlockSystem.GetPsychicCrushChannelDuration(CMUXenoWarlockSystem.GetPsychicCrushPulseInterval());

    public static TimeSpan GetPsychicCrushChannelDuration(TimeSpan pulseInterval)
        => pulseInterval * PsychicCrushMaxAreaRadius;

    public static bool ShouldPsychicCrushCancellationResolve() => true;

    public static bool ShouldPsychicCrushInterruptionResolve()
        => CMUXenoWarlockSystem.ShouldPsychicCrushCancellationResolve();

    public static bool ShouldSpawnPsychicCrushTileBlur(bool detonated) => true;

    public static string GetPsychicCrushBlurPrototype() => "CMUXenoPsychicCrushBlur";

    public static TimeSpan GetPsychicCrushBlurDuration() => TimeSpan.FromSeconds(1);

    public static TimeSpan GetPsychicCrushOwnerSlowDuration() => TimeSpan.FromSeconds(1);

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

    public static TimeSpan GetPsychicCrushStaggerDuration(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        return TimeSpan.FromSeconds(2 * pulses);
    }

    public static TimeSpan GetPsychicCrushSlowDuration(int completedPulses)
    {
        int pulses = Math.Clamp(completedPulses, 0, PsychicCrushMaxPulses);
        return TimeSpan.FromSeconds(3 * pulses);
    }

    public static TimeSpan GetPsychicBlastChargeDuration() => TimeSpan.FromSeconds(1);

    public static string GetPsychicBlastBeamPrototype(CMUXenoPsychicBlastMode mode)
    {
        return mode switch
        {
            CMUXenoPsychicBlastMode.Lance => "CMUXenoPsychicLanceProjectile", _ => "CMUXenoPsychicBlastProjectile"
        };
    }

    public static bool ShouldShowWarlockChannelEffect(CMUXenoWarlockChannelKind kind)
        => kind is CMUXenoWarlockChannelKind.PsychicCrush
            or CMUXenoWarlockChannelKind.PsychicBlast
            or CMUXenoWarlockChannelKind.PsychicShield;

    public static string GetWarlockChannelColor(CMUXenoWarlockChannelKind kind, CMUXenoPsychicBlastMode mode)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast when mode == CMUXenoPsychicBlastMode.Lance => "#CB0166",
            CMUXenoWarlockChannelKind.PsychicBlast => "#970f0f",
            CMUXenoWarlockChannelKind.PsychicCrush => "#6a59b3",
            CMUXenoWarlockChannelKind.PsychicShield => "#5999b3", _ => "#ffffff"
        };
    }

    public static bool ShouldSpawnWarlockChannelStream(CMUXenoWarlockChannelKind kind) => false;

    public static string
        GetWarlockChannelParticlePrototype(CMUXenoWarlockChannelKind kind, CMUXenoPsychicBlastMode mode)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast when mode == CMUXenoPsychicBlastMode.Lance =>
                "CMUXenoWarlockLanceParticles",
            CMUXenoWarlockChannelKind.PsychicBlast => "CMUXenoWarlockBlastParticles",
            CMUXenoWarlockChannelKind.PsychicCrush => "CMUXenoWarlockCrushParticles",
            _                                      => "CMUXenoWarlockCrushParticles"
        };
    }

    private static EntProtoId GetWarlockChannelParticlePrototype(CMUXenoWarlockComponent warlock,
        CMUXenoWarlockChannelKind kind,
        CMUXenoPsychicBlastMode mode)
    {
        return kind switch
        {
            CMUXenoWarlockChannelKind.PsychicBlast when mode == CMUXenoPsychicBlastMode.Lance =>
                warlock.PsychicLanceChannelParticleId,
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
            CMUXenoWarlockParticleEffect.PsychicLanceCharge => new("#CB0166",
                300,
                30,
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
            or CMUXenoWarlockParticleEffect.PsychicLanceCharge
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

    public static string GetWarlockChannelLightPrototype(CMUXenoWarlockChannelKind kind, CMUXenoPsychicBlastMode mode)
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

    public static FixedPoint2 GetPsychicShieldDetonationCost() => FixedPoint2.New(PsychicShieldPlasmaCost);

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
        => CMUXenoWarlockSystem.ReflectProjectileVelocity(velocity, shieldDirection, Angle.Zero);

    public static Vector2 ReflectProjectileVelocity(Vector2 velocity, Direction shieldDirection, Angle spread)
    {
        Vector2 reflected = shieldDirection switch
        {
            Direction.North or Direction.South => new(velocity.X, -velocity.Y),
            Direction.East or Direction.West   => new(-velocity.X, velocity.Y), _ => -velocity
        };

        return spread.RotateVec(reflected);
    }

    public static float GetPsychicShieldReflectionSpreadDegrees() => PsychicShieldReflectionSpreadDegrees;

    public static bool IsProjectileIncomingFromFront(Vector2 velocity, Direction shieldDirection)
    {
        if (velocity.LengthSquared() <= 0f)
            return false;

        Angle angle = velocity.ToWorldAngle();
        var shieldAngle = shieldDirection.ToAngle();
        Angle diff = Angle.ShortestDistance(angle, shieldAngle + Math.PI);
        return Math.Abs(diff.Theta) <= Math.PI / 2;
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
