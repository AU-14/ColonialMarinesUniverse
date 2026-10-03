using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics; // CMU14
using Content.Shared.Popups; // CMU14
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Physics; // CMU14
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Lunge;

public sealed partial class XenoLungeSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ThrownItemSystem _thrownItem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private StatusEffectQuerySystem _statusEffects = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private SharedRMCLagCompensationSystem _rmcLagCompensation = default!;
    [Dependency] private RMCObstacleSlammingSystem _rmcObstacleSlamming = default!;
    [Dependency] private XenoLeapSystem _leap = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedPopupSystem _popup = default!; // CMU14

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();

        SubscribeAllEvent<XenoLungePredictedHitEvent>(OnPredictedHit);

        SubscribeLocalEvent<XenoLungeComponent, XenoLungeActionEvent>(OnXenoLungeAction);
        SubscribeLocalEvent<XenoLungeComponent, MeleeAttackAttemptEvent>(OnAttackAttempt);

        SubscribeLocalEvent<XenoActiveLungeComponent, ThrowDoHitEvent>(OnXenoLungeHit);
        SubscribeLocalEvent<XenoActiveLungeComponent, LandEvent>(OnXenoLungeLand);

        SubscribeLocalEvent<RMCLungeProtectionComponent, XenoLungeHitAttempt>(OnXenoLungeHitAttempt);

        SubscribeLocalEvent<XenoLungeStunnedComponent, PullStoppedMessage>(OnXenoLungeStunnedPullStopped);
    }

    private void OnPredictedHit(XenoLungePredictedHitEvent msg, EntitySessionEventArgs args)
    {
        // CMU14: saved predictive hit events must also rebuild local effects on replay.
        // if (_net.IsClient)
        //     return;

        if (args.SenderSession.AttachedEntity is not { } ent)
            return;

        if (!TryComp(ent, out XenoActiveLungeComponent? lunging))
            return;

        if (GetEntity(msg.Target) is not { Valid: true } target)
            return;

        if (!lunging.Running)
            return;

        // CMU14
        // if (lunging.Target != target)
        //     return;

        if (_net.IsServer && !_rmcLagCompensation.ValidatePredictedHit(target, ent, args.SenderSession, msg.LastRealTick, msg.Substep)) // CMU14
            return;

        ApplyLungeHitEffects((ent, lunging), target, true, false);
    }

    private void OnXenoLungeAction(Entity<XenoLungeComponent> xeno, ref XenoLungeActionEvent args)
    {
        if (args.Entity is not { } target)
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return;

        if (args.Handled)
            return;

        var attempt = new XenoLungeAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);

        if (attempt.Cancelled)
            return;

        args.Handled = true;

        _rmcPulling.TryStopAllPullsFromAndOn(xeno);

        var origin = _transform.GetMapCoordinates(xeno);
        // CMU14: direction and obstruction checks use map space on translated/rotated grids.
        var targetCoords = _transform.ToMapCoordinates(_rmcLagCompensation.GetCoordinates(target, xeno));
        var diff = targetCoords.Position - origin.Position;
        if (origin.MapId != targetCoords.MapId || diff.LengthSquared() < 0.0001f)
            return;
        diff = diff.Normalized() * xeno.Comp.Range;

        // CMU14: lunges must not cross barricade lines; the throw only stops on a
        // direct fixture hit, so check the path up front. Barbed wire keeps its block.
        var ray = new CollisionRay(origin.Position, diff.Normalized(), (int) CollisionGroup.BarricadeImpassable);
        foreach (var result in _physics.IntersectRayWithPredicate(origin.MapId, ray, diff.Length(), e => !Transform(e).Anchored))
        {
            if (TryComp(result.HitEntity, out RMCLeapProtectionComponent? protection) &&
                _leap.AttemptBlockLeap(result.HitEntity, protection.StunDuration, protection.BlockSound, xeno, _transform.GetMoverCoordinates(xeno), protection.FullProtection))
                return;

            _popup.PopupClient(Loc.GetString("cmu-xeno-dash-blocked"), xeno, xeno);
            return;
        }

        var active = EnsureComp<XenoActiveLungeComponent>(xeno);
        active.Origin = origin;
        active.Charge = diff;
        active.Target = target;
        active.TargetCoordinates = targetCoords; // CMU14
        active.HitResolved = false; // CMU14
        active.Range = xeno.Comp.Range;
        active.StunTime = xeno.Comp.StunTime;
        Dirty(xeno);

        _rmcObstacleSlamming.MakeImmune(xeno, 0.5f);
        _throwing.TryThrow(xeno, diff, 30, animated: false);

        if (!_physicsQuery.TryGetComponent(xeno, out var physics))
            return;

        // Handle close-range or same-tile lunges
        foreach (var ent in _physics.GetContactingEntities(xeno.Owner, physics))
        {
            if (ent != target)
                continue;

            if (ApplyLungeHitEffects(xeno.Owner, ent, true))
                return;
        }
    }

    private void OnAttackAttempt(Entity<XenoLungeComponent> ent, ref MeleeAttackAttemptEvent args)
    {
        var netAttacker = GetNetEntity(ent);
        if (!TryComp(GetEntity(args.Target), out XenoLungeStunnedComponent? stunned) ||
            netAttacker != stunned.Stunner)
        {
            return;
        }

        switch (args.Attack)
        {
            case DisarmAttackEvent disarm:
                args.Attack = new LightAttackEvent(disarm.Target, netAttacker, disarm.Coordinates);
                break;
        }
    }

    private void OnXenoLungeHit(Entity<XenoActiveLungeComponent> xeno, ref ThrowDoHitEvent args)
    {
        if (!_mobState.IsAlive(xeno) || HasComp<StunnedComponent>(xeno))
        {
            RemCompDeferred<XenoActiveLungeComponent>(xeno);
            return;
        }

        ApplyLungeHitEffects(xeno.AsNullable(), args.Target, true);
    }

    // CMU14 method: a missed landing cleans up the lunge; it is not a hit on the aimed target.
    private void OnXenoLungeLand(Entity<XenoActiveLungeComponent> ent, ref LandEvent args)
    {
        StopLunge(ent);
        RemCompDeferred<XenoActiveLungeComponent>(ent);
    }

    // CMU14 method: consume the actual impact before synchronous landing callbacks.
    private bool ApplyLungeHitEffects(Entity<XenoActiveLungeComponent?> xeno, EntityUid targetId, bool stopThrow, bool predicted = true)
    {
        if (!Resolve(xeno, ref xeno.Comp, false) || !xeno.Comp.Running || xeno.Comp.HitResolved)
            return false;

        if (TerminatingOrDeleted(targetId) || _mobState.IsDead(targetId))
            return false;

        var hitCoordinates = _transform.GetMapCoordinates(targetId);
        if (hitCoordinates.MapId != _transform.GetMapCoordinates(xeno).MapId)
            return false;

        xeno.Comp.HitResolved = true;
        Dirty(xeno.Owner, xeno.Comp);

        if (_physicsQuery.TryGetComponent(xeno, out var physics) &&
            _thrownItemQuery.TryGetComponent(xeno, out var thrown))
        {
            _thrownItem.LandComponent(xeno, thrown, physics, true);

            if (stopThrow)
                _thrownItem.StopThrow(xeno, thrown);
        }

        var ev = new XenoLungeHitAttempt(xeno);
        RaiseLocalEvent(targetId, ref ev);

        if (ev.Cancelled)
            return true;

        if (!_xeno.CanAbilityAttackTarget(xeno, targetId) ||
            (_size.TryGetSize(targetId, out var size) && size >= RMCSizes.Big) ||
            (TryComp<XenoComponent>(targetId, out var xenoComp) && xenoComp.Tier >= 2)) //Fails if big or tier 2 or more
        {
            return true;
        }

        if (_net.IsServer)
        {
            var stunTime = _xeno.TryApplyXenoDebuffMultiplier(targetId, xeno.Comp.StunTime);
            _stun.TryParalyze(targetId, stunTime, true);

            var stunned = EnsureComp<XenoLungeStunnedComponent>(targetId);
            stunned.ExpireAt = _timing.CurTime + stunTime;
            stunned.Stunner = GetNetEntity(xeno);
            Dirty(targetId, stunned);
        }

        if (TryComp(xeno, out MeleeWeaponComponent? melee))
        {
            melee.NextAttack = _timing.CurTime;
            Dirty(xeno, melee);
        }

        if (_net.IsClient && predicted && _timing.IsFirstTimePredicted)
        {
            var predictedEv = new XenoLungePredictedHitEvent(GetNetEntity(targetId), _rmcLagCompensation.GetLastRealTick(null), _rmcLagCompensation.GetClientSubstep());
            if (_timing.InPrediction)
                RaisePredictiveEvent(predictedEv);
            else
                RaiseNetworkEvent(predictedEv);
        }

        StopLunge(xeno);

        // Correct the attacker toward the actual hit, never move an interceptor to the
        // originally selected target's location.
        var coordinates = _transform.GetMapCoordinates(xeno);
        if (hitCoordinates.MapId == coordinates.MapId &&
            !hitCoordinates.InRange(coordinates, 1.25f))
        {
            var distance = hitCoordinates.Position - coordinates.Position;
            var length = distance.Length();
            var newPosition = coordinates.Offset(((float) (length - 1.25) / length) * distance);
            _transform.SetMapCoordinates(xeno, newPosition);
        }

        _pulling.TryStartPull(xeno, targetId);
        RemCompDeferred<XenoActiveLungeComponent>(xeno);
        return true;
    }

    private void OnXenoLungeStunnedPullStopped(Entity<XenoLungeStunnedComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        var clearParalysis = false;
        foreach (var effect in ent.Comp.Effects)
        {
            if (effect.Id is "Stun" or "KnockedDown")
            {
                clearParalysis = true;
                continue;
            }

            _statusEffects.TryRemoveStatusEffect(ent, effect);
        }

        if (clearParalysis)
            _stun.TryClearStunAndKnockdown(ent);

        RemCompDeferred<XenoLungeStunnedComponent>(ent.Owner);
    }

    private void OnXenoLungeHitAttempt(Entity<RMCLungeProtectionComponent> ent, ref XenoLungeHitAttempt args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp(args.Lunging, out XenoActiveLungeComponent? lunging))
            return;

        args.Cancelled = _leap.AttemptBlockLeap(ent.Owner, ent.Comp.StunDuration,ent.Comp.BlockSound, args.Lunging, _transform.ToCoordinates(lunging.Origin), ent.Comp.FullProtection);
    }

    private void StopLunge(EntityUid lunging)
    {
        if (!_physicsQuery.TryGetComponent(lunging, out var physics))
            return;

        _physics.SetLinearVelocity(lunging, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(lunging, physics, BodyStatus.OnGround);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var stunnedQuery = EntityQueryEnumerator<XenoLungeStunnedComponent>();
        while (stunnedQuery.MoveNext(out var uid, out var stunned))
        {
            if (time < stunned.ExpireAt)
                continue;

            RemCompDeferred<XenoLungeStunnedComponent>(uid);
        }

        // var activeLungeQuery = EntityQueryEnumerator<XenoActiveLungeComponent>();
        // while (activeLungeQuery.MoveNext(out var uid, out var comp))
        // {
        //     if (!TryComp(uid, out ThrownItemComponent? thrown))
        //     {
        //         RemCompDeferred<XenoActiveLungeComponent>(uid);
        //         continue;
        //     }
        //
        //     if (comp.Origin.MapId != comp.TargetCoordinates.MapId)
        //     {
        //         _thrownItem.StopThrow(uid, thrown);
        //         continue;
        //     }
        //
        //     var coords = _transform.GetMapCoordinates(uid);
        //     var range = (comp.Origin.Position - comp.TargetCoordinates.Position).Length();
        //     if (!comp.Origin.InRange(coords, range))
        //     {
        //         if (!_pulling.IsPulling(uid))
        //             ApplyLungeHitEffects((uid, comp), comp.Target, true);
        //     }
        // }
    }
}

[ByRefEvent]
public record struct XenoLungeHitAttempt(EntityUid Lunging, bool Cancelled = false);
