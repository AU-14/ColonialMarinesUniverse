using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Barricade.Components;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Invisibility;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.ActionBlocker;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory.Events;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Ape;

public sealed partial class ApeLeapSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedBroadphaseSystem _broadphase = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCLagCompensationSystem _rmcLagCompensation = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private SharedDirectionalAttackBlockSystem _directionalBlock = default!;
    [Dependency] private DamageableSystem _damagable = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        SubscribeAllEvent<ApeLeapPredictedHitEvent>(OnPredictedHit);

        SubscribeLocalEvent<ApeLeapComponent, ApeLeapActionEvent>(OnApeLeapAction);
        SubscribeLocalEvent<ApeLeapComponent, ApeLeapDoAfterEvent>(OnApeLeapDoAfter);
        SubscribeLocalEvent<ApeLeapComponent, MeleeHitEvent>(OnApeLeapMelee);
        SubscribeLocalEvent<ApeLeapComponent, RMCMeleeUserGetRangeEvent>(OnApeLeapingMeleeGetRange);

        SubscribeLocalEvent<ApeLeapingComponent, StartCollideEvent>(OnApeLeapingDoHit);
        SubscribeLocalEvent<ApeLeapingComponent, ComponentRemove>(OnApeLeapingRemove);
        SubscribeLocalEvent<ApeLeapingComponent, PhysicsSleepEvent>(OnApeLeapingPhysicsSleep);
        SubscribeLocalEvent<ApeLeapingComponent, StartPullAttemptEvent>(OnApeLeapingStartPullAttempt);
        SubscribeLocalEvent<ApeLeapingComponent, PullAttemptEvent>(OnApeLeapingPullAttempt);
    }

    private void OnPredictedHit(ApeLeapPredictedHitEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ent)
            return;

        if (!TryComp(ent, out ApeLeapingComponent? leaping))
            return;

        if (GetEntity(msg.Target) is not { Valid: true } target)
            return;

        if (_net.IsServer)
        {
            if (!HasComp<ApeLeapComponent>(ent) || !leaping.Running)
                return;

            _rmcLagCompensation.SetLastRealTick(args.SenderSession.UserId, msg.LastRealTick);
            if (!_rmcLagCompensation.Collides(target, ent, args.SenderSession))
                return;
        }

        ApplyLeapingHitEffects((ent, leaping), target);
    }

    private void OnApeLeapAction(Entity<ApeLeapComponent> ape, ref ApeLeapActionEvent args)
    {
        if (args.Handled)
            return;

        var attempt = new ApeLeapAttemptEvent();
        RaiseLocalEvent(ape, ref attempt);

        if (attempt.Cancelled)
            return;

        args.Handled = true;

        var ev = new ApeLeapDoAfterEvent(GetNetCoordinates(args.Target));
        var doAfter = new DoAfterArgs(EntityManager, ape, ape.Comp.Delay, ev, ape)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = FixedPoint2.New(10)
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnApeLeapDoAfter(Entity<ApeLeapComponent> ape, ref ApeLeapDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-leap-cancelled"), ape, ape);
            return;
        }

        if (!_physicsQuery.TryGetComponent(ape, out var physics))
            return;

        if (EnsureComp<ApeLeapingComponent>(ape, out var leaping))
            return;

        args.Handled = true;

        leaping.KnockdownRequiresInvisibility = ape.Comp.KnockdownRequiresInvisibility;
        leaping.DestroyObjects = ape.Comp.DestroyObjects;
        leaping.MoveDelayTime = ape.Comp.MoveDelayTime;
        leaping.Damage = ape.Comp.Damage;
        leaping.HitEffect = ape.Comp.HitEffect;
        leaping.TargetJitterTime = ape.Comp.TargetJitterTime;
        leaping.TargetCameraShakeStrength = ape.Comp.TargetCameraShakeStrength;
        leaping.IgnoredCollisionGroupLarge = ape.Comp.IgnoredCollisionGroupLarge;
        leaping.IgnoredCollisionGroupSmall = ape.Comp.IgnoredCollisionGroupSmall;

        _rmcPulling.TryStopAllPullsFromAndOn(ape);

        var origin = _transform.GetMapCoordinates(ape);
        var target = _transform.ToMapCoordinates(args.TargetCoords);
        var direction = target.Position - origin.Position;

        if (direction == Vector2.Zero)
            return;

        var length = direction.Length();
        var distance = Math.Clamp(length, 0.1f, ape.Comp.Range.Float());
        direction *= distance / length;
        var impulse = direction.Normalized() * ape.Comp.Strength * physics.Mass;

        leaping.Origin = _transform.GetMoverCoordinates(ape);
        leaping.ParalyzeTime = ape.Comp.KnockdownTime;
        leaping.LeapSound = ape.Comp.LeapSound;
        leaping.LeapEndTime = _timing.CurTime + TimeSpan.FromSeconds(direction.Length() / ape.Comp.Strength);

        _obstacleSlamming.MakeImmune(ape, 0.5f);
        _physics.ApplyLinearImpulse(ape, impulse, body: physics);
        _physics.SetBodyStatus(ape, physics, BodyStatus.InAir);

        if (TryComp(ape, out FixturesComponent? fixtures))
        {
            var collisionGroup = (int) leaping.IgnoredCollisionGroupSmall;
            if (_size.TryGetSize(ape, out var size) && size > RMCSizes.SmallXeno)
                collisionGroup = (int) leaping.IgnoredCollisionGroupLarge;

            var fixture = fixtures.Fixtures.First();
            _physics.SetCollisionMask(ape, fixture.Key, fixture.Value, fixture.Value.CollisionMask ^ collisionGroup);
        }

        //Handle close-range or same-tile leaps
        foreach (var ent in _physics.GetContactingEntities(ape.Owner, physics))
        {
            if (ApplyLeapingHitEffects((ape, leaping), ent))
                return;
        }
    }

    private void OnApeLeapMelee(Entity<ApeLeapComponent> ape, ref MeleeHitEvent args)
    {
        if (!ape.Comp.UnrootOnMelee)
            return;

        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var entity in args.HitEntities)
        {
            if (TryComp<SlowedDownComponent>(ape, out var root) && root.SprintSpeedModifier == 0f)
            {
                RemComp<SlowedDownComponent>(ape);
                _movementSpeed.RefreshMovementSpeedModifiers(ape);
            }

            ape.Comp.LastHit = null;
            ape.Comp.LastHitAt = null;
            Dirty(ape);
            break;
        }
    }

    private void OnApeLeapingMeleeGetRange(Entity<ApeLeapComponent> ent, ref RMCMeleeUserGetRangeEvent args)
    {
        if (ent.Comp.LastHit == null ||
            ent.Comp.LastHit != args.Target ||
            _timing.CurTime > ent.Comp.LastHitAt + ent.Comp.MoveDelayTime)
        {
            return;
        }

        args.Range = ent.Comp.LastHitRange;
    }

    private void OnApeLeapingDoHit(Entity<ApeLeapingComponent> ape, ref StartCollideEvent args)
    {
        ApplyLeapingHitEffects(ape, args.OtherEntity);
    }

    private void OnApeLeapingRemove(Entity<ApeLeapingComponent> ent, ref ComponentRemove args)
    {
        var ev = new ApeLeapStoppedEvent();
        RaiseLocalEvent(ent, ref ev);

        StopLeap(ent);
    }

    private void OnApeLeapingPhysicsSleep(Entity<ApeLeapingComponent> ent, ref PhysicsSleepEvent args)
    {
        StopLeap(ent);
    }

    private void OnApeLeapingStartPullAttempt(Entity<ApeLeapingComponent> ent, ref StartPullAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnApeLeapingPullAttempt(Entity<ApeLeapingComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private bool IsValidLeapHit(Entity<ApeLeapingComponent> xeno, EntityUid target)
    {
        if (xeno.Comp.KnockedDown)
            return false;

        if (xeno.Comp.DestroyObjects && TryComp<XenoLeapDestroyOnPassComponent>(target, out var destroy))
        {
            if (_net.IsServer)
            {
                for (var i = 0; i < destroy.Amount; i++)
                {
                    if (destroy.SpawnPrototype != null)
                        SpawnAtPosition(destroy.SpawnPrototype, target.ToCoordinates());
                }

                QueueDel(target);
            }

            _physics.SetCanCollide(target, false, force: true);
            return false;
        }

        if ((HasComp<XenoParasiteComponent>(target) ||
            !HasComp<MobStateComponent>(target)) &&
            !HasComp<RMCLeapProtectionComponent>(target))
        {
            return false;
        }

        if (_standing.IsDown(target))
            return false;

        if (HasComp<LeapIncapacitatedComponent>(target))
            return false;

        if (_size.TryGetSize(target, out var size) && size >= RMCSizes.Big)
            return false;

        if (size == RMCSizes.VerySmallXeno)
            return false;

        return true;
    }

    private bool ApplyLeapingHitEffects(Entity<ApeLeapingComponent> xeno, EntityUid target)
    {
        if (!IsValidLeapHit(xeno, target))
            return false;

        var leapEv = new ApeLeapHitAttempt(xeno.Owner);
        RaiseLocalEvent(target, ref leapEv);

        if (leapEv.Cancelled)
        {
            xeno.Comp.KnockedDown = true;
            StopLeap(xeno);
            Dirty(xeno);
            return true;
        }

        if (!HasComp<MobStateComponent>(target) || _mobState.IsIncapacitated(target))
            return false;

        xeno.Comp.KnockedDown = true;
        Dirty(xeno);

        if (TryComp(xeno, out ApeLeapComponent? leap))
        {
            leap.LastHit = target;
            leap.LastHitAt = _timing.CurTime;
            Dirty(xeno, leap);
        }

        if (_physicsQuery.TryGetComponent(xeno, out var physics))
        {
            _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);

            if (physics.Awake)
                _broadphase.RegenerateContacts((xeno.Owner, physics));
        }

        if (!xeno.Comp.KnockdownRequiresInvisibility || HasComp<XenoActiveInvisibleComponent>(xeno))
        {
            var victim = EnsureComp<LeapIncapacitatedComponent>(target);
            victim.RecoverAt = _timing.CurTime + xeno.Comp.ParalyzeTime;
            Dirty(target, victim);

            _stun.TrySlowdown(xeno, xeno.Comp.MoveDelayTime, true, 0f, 0f);

            if (_net.IsServer)
                _stun.TryParalyze(target, xeno.Comp.ParalyzeTime, true);
        }

        if (xeno.Comp.HitEffect != null)
        {
            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.HitEffect, target.ToCoordinates());
        }

        var damage = _damagable.TryChangeDamage(target, xeno.Comp.Damage, origin: xeno, tool: xeno);
        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(target, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
        }

        _jitter.DoJitter(target, xeno.Comp.TargetJitterTime, false);
        _cameraShake.ShakeCamera(target, 2, xeno.Comp.TargetCameraShakeStrength);

        var ev = new ApeLeapHitEvent(xeno, target);
        RaiseLocalEvent(xeno, ev);

        if (!xeno.Comp.PlayedSound && _net.IsServer)
        {
            xeno.Comp.PlayedSound = true;
            _audio.PlayPvs(xeno.Comp.LeapSound, xeno);
        }

        if (_net.IsClient)
        {
            var predictedEv = new ApeLeapPredictedHitEvent(GetNetEntity(target), _rmcLagCompensation.GetLastRealTick(null));
            RaiseNetworkEvent(predictedEv);
            if (_timing.InPrediction && _timing.IsFirstTimePredicted)
            {
                RaisePredictiveEvent(predictedEv);
            }
        }

        StopLeap(xeno);
        return true;
    }

    private void StopLeap(Entity<ApeLeapingComponent> leaping)
    {
        if (_physicsQuery.TryGetComponent(leaping, out var physics))
        {
            _physics.SetLinearVelocity(leaping, Vector2.Zero, body: physics);
            _physics.SetBodyStatus(leaping, physics, BodyStatus.OnGround);
        }

        if (_fixturesQuery.TryGetComponent(leaping, out var fixtures))
        {
            var collisionGroup = (int)leaping.Comp.IgnoredCollisionGroupSmall;
            if (_size.TryGetSize(leaping, out var size) && size > RMCSizes.SmallXeno)
                collisionGroup = (int)leaping.Comp.IgnoredCollisionGroupLarge;

            if (size >= RMCSizes.SmallXeno)
            {
                var fixture = fixtures.Fixtures.First();
                _physics.SetCollisionMask(leaping, fixture.Key, fixture.Value, fixture.Value.CollisionMask | collisionGroup);
            }
        }

        RemCompDeferred<ApeLeapingComponent>(leaping);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var leaping = EntityQueryEnumerator<ApeLeapingComponent>();
        while (leaping.MoveNext(out var uid, out var comp))
        {
            if (time < comp.LeapEndTime)
                continue;

            StopLeap((uid, comp));
        }

        if (_net.IsClient)
            return;

        var incapacitated = EntityQueryEnumerator<LeapIncapacitatedComponent>();
        while (incapacitated.MoveNext(out var uid, out var victim))
        {
            if (victim.RecoverAt > time)
                continue;

            RemCompDeferred<LeapIncapacitatedComponent>(uid);
            _blindable.UpdateIsBlind(uid);
            _actionBlocker.UpdateCanMove(uid);
        }
    }
}

[ByRefEvent]
public record struct ApeLeapHitAttempt(EntityUid Leaper, bool Cancelled = false);

[ByRefEvent]
public record struct ApeLeapStoppedEvent;
