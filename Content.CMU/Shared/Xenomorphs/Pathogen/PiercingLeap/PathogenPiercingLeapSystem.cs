using System.Numerics;  
using Content.Shared._RMC14.Actions;  
using Content.Shared._RMC14.Areas;  
using Content.Shared._RMC14.Emote;  
using Content.Shared._RMC14.Map;  
using Content.Shared._RMC14.Pulling;  
using Content.Shared._RMC14.Stun;  
using Content.Shared._RMC14.Xenonids;  
using Content.Shared.ActionBlocker;  
using Content.Shared.Actions;  
using Content.Shared.Coordinates;  
using Content.Shared.Damage.Systems;  
using Content.Shared.DoAfter;  
using Content.Shared.Effects;  
using Content.Shared.FixedPoint;  
using Content.Shared.Interaction;  
using Content.Shared.Interaction.Events;  
using Content.Shared.Jittering;  
using Content.Shared.Maps;  
using Content.Shared.Mobs;  
using Content.Shared.Mobs.Components;  
using Content.Shared.Mobs.Systems;  
using Content.Shared.Movement.Events;  
using Content.Shared.Movement.Pulling.Events;  
using Content.Shared.Popups;  
using Content.Shared.Throwing;  
using Robust.Shared.Audio.Systems;  
using Robust.Shared.Map.Components;  
using Robust.Shared.Network;  
using Robust.Shared.Physics;  
using Robust.Shared.Physics.Components;  
using Robust.Shared.Physics.Events;  
using Robust.Shared.Physics.Systems;  
using Robust.Shared.Player;  
using Robust.Shared.Timing;  
  
namespace Content.CMU.Shared.Xenomorphs.Pathogen.PierceLeap;  
  
public sealed partial class PathogenPierceLeapSystem : EntitySystem  
{  
    [Dependency] private readonly INetManager _net = default!;  
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;  
    [Dependency] private readonly AreaSystem _area = default!;  
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;  
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;  
    [Dependency] private readonly TurfSystem _turf = default!;  
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;  
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = default!;  
    [Dependency] private readonly SharedTransformSystem _transform = default!;  
    [Dependency] private readonly MobStateSystem _mob = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
    [Dependency] private readonly SharedMapSystem _map = default!;  
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;  
    [Dependency] private readonly DamageableSystem _damage = default!;  
    [Dependency] private readonly RMCSizeStunSystem _size = default!;  
    [Dependency] private readonly SharedAudioSystem _audio = default!;  
    [Dependency] private readonly SharedPopupSystem _popup = default!;  
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;  
    [Dependency] private readonly SharedActionsSystem _actions = default!;  
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;  
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;  
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;  
    [Dependency] private readonly XenoSystem _xeno = default!;  
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;  
  
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();  
  
    public override void Initialize()  
    {  
        SubscribeLocalEvent<PathogenPierceLeapComponent, PathogenPierceLeapActionEvent>(OnLeapAction);  
        SubscribeLocalEvent<PathogenPierceLeapComponent, PathogenPierceLeapDoAfterEvent>(OnLeapDoAfter);  
  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, ComponentInit>(OnLeapingInit);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, ComponentRemove>(OnLeapingRemove);  
  
        // Cancel every form of interaction while airborne.  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, AttemptMobCollideEvent>(OnLeapCollide);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, AttemptMobTargetCollideEvent>(OnLeapTargetCollide);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, ChangeDirectionAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, UpdateCanMoveAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, UseAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, InteractionAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, DropAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, AttackAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, PushAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, PullAttemptEvent>(OnAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, ThrowAttemptEvent>(OnAttempt);  
    }  
  
    // Cancelling these two is what makes the leaper pass through mobs  
    private void OnLeapCollide(Entity<PathogenPierceLeapingComponent> xeno, ref AttemptMobCollideEvent args)  
    {  
        args.Cancelled = true;  
    }  
  
    private void OnLeapTargetCollide(Entity<PathogenPierceLeapingComponent> xeno, ref AttemptMobTargetCollideEvent args)  
    {  
        args.Cancelled = true;  
    }  
  
    private void OnAttempt(Entity<PathogenPierceLeapingComponent> xeno, ref CancellableEntityEventArgs args)  
    {  
        args.Cancel();  
    }  
  
    private void OnLeapAction(Entity<PathogenPierceLeapComponent> xeno, ref PathogenPierceLeapActionEvent args)  
    {  
        if (args.Handled)  
            return;  
  
        var target = _transform.ToMapCoordinates(args.Target);  
  
        if (target.MapId == MapId.Nullspace)  
            return;  
  
        if (!_interaction.InRangeUnobstructed(xeno.Owner, args.Target, xeno.Comp.Range))  
            return;  
  
        if (!_area.CanLeap(xeno, target))  
            return;  
  
        args.Handled = true;  
  
        _jitter.DoJitter(xeno, xeno.Comp.WindupTime, true, 80, 8, true);  
        _emote.TryEmoteWithChat(xeno, xeno.Comp.Emote);  
  
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.WindupTime,  
            new PathogenPierceLeapDoAfterEvent(), xeno, xeno, args)  
        {  
            BreakOnMove = true,  
            BreakOnDamage = true,  
            BreakOnRest = true,  
            BreakOnWeightlessMove = false,  
            NeedHand = false,  
        };  
  
        _doAfter.TryStartDoAfter(doAfter);  
    }  
  
    private void OnLeapDoAfter(Entity<PathogenPierceLeapComponent> xeno, ref PathogenPierceLeapDoAfterEvent args)  
    {  
        if (args.Cancelled || args.Handled)  
            return;  
  
        if (args.Args.Target is not { } target)  
            return;  
  
        args.Handled = true;  
  
        var origin = _transform.GetMoverCoordinates(xeno);  
        var targetMap = _transform.ToMapCoordinates(target);  
        var direction = targetMap.Position - _transform.GetMapCoordinates(xeno).Position;  
        var length = direction.Length();  
  
        if (length > xeno.Comp.Range)  
        {  
            direction = Vector2.Normalize(direction) * xeno.Comp.Range;  
            length = xeno.Comp.Range;  
        }  
  
        var leaping = EnsureComp<PathogenPierceLeapingComponent>(xeno);  
        leaping.Origin = origin;  
        leaping.LeapEndTime = _timing.CurTime + TimeSpan.FromSeconds(length / xeno.Comp.Strength);  
        leaping.Damage = xeno.Comp.Damage;  
        leaping.StrikeDelay = xeno.Comp.StrikeDelay;  
        leaping.PathWidth = xeno.Comp.PathWidth;  
        leaping.HitEffect = xeno.Comp.HitEffect;  
        leaping.HitSound = xeno.Comp.HitSound;  
        Dirty(xeno, leaping);  
  
        _rmcPulling.TryStopAllPullsFromAndOn(xeno);  
  
        // same as XenoLeap.  
        if (TryComp<PhysicsComponent>(xeno, out var physics))  
        {  
            var impulse = direction * xeno.Comp.Strength * physics.Mass;  
            _physics.ApplyLinearImpulse(xeno, impulse, body: physics);  
            _physics.SetBodyStatus(xeno, physics, BodyStatus.InAir);  
        }  
  
    
        if (TryComp<FixturesComponent>(xeno, out var fixtures))  
        {  
            var collisionGroup = (int) xeno.Comp.IgnoredCollisionGroupSmall;  
            if (_size.TryGetSize(xeno, out var size) && size > RMCSizes.SmallXeno)  
                collisionGroup = (int) xeno.Comp.IgnoredCollisionGroupLarge;  
  
            var fixture = fixtures.Fixtures.First();  
            _physics.SetCollisionMask(xeno, fixture.Key, fixture.Value,  
                fixture.Value.CollisionMask ^ collisionGroup);  
        }  
  
        _audio.PlayPredicted(xeno.Comp.LeapSound, xeno, xeno);  
        _rotateToFace.TryFaceCoordinates(xeno, targetMap.Position);  
        SetCooldown(xeno);  
    }  
  
    // Runs every tick while airborne: collect valid targets within PathWidth  
    // of the leaper's current position into the Tagged set.  
    private void TagTargetsInPath(Entity<PathogenPierceLeapingComponent> xeno)  
    {  
        _mobs.Clear();  
        _entityLookup.GetEntitiesInRange(Transform(xeno).Coordinates,  
            xeno.Comp.PathWidth, _mobs, LookupFlags.Uncontained);  
  
        foreach (var mob in _mobs)  
        {  
            if (mob.Owner == xeno.Owner)  
                continue;  
  
            if (xeno.Comp.Tagged.Contains(mob.Owner))  
                continue;  
  
            if (!_xeno.CanAbilityAttackTarget(xeno, mob.Owner))  
                continue;  
  
            xeno.Comp.Tagged.Add(mob.Owner);  
            SpawnAttachedTo(xeno.Comp.TagEffect, mob.Owner.ToCoordinates());  
        }  
    }  
  
    // Leap end restore physics, then arm a delayed strike on every tagged mob.  
    private void StopLeap(Entity<PathogenPierceLeapingComponent> xeno)  
    {  
        if (TryComp<PhysicsComponent>(xeno, out var physics))  
        {  
            _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);  
            _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);  
        }  
  
        if (TryComp<FixturesComponent>(xeno, out var fixtures))  
        {  
            var collisionGroup = (int) xeno.Comp.IgnoredCollisionGroupSmall;  
            if (_size.TryGetSize(xeno, out var size) && size > RMCSizes.SmallXeno)  
                collisionGroup = (int) xeno.Comp.IgnoredCollisionGroupLarge;  
  
            var fixture = fixtures.Fixtures.First();  
            _physics.SetCollisionMask(xeno, fixture.Key, fixture.Value,  
                fixture.Value.CollisionMask | collisionGroup);  
        }  
  
        if (_net.IsServer)  
        {  
            var strikeAt = _timing.CurTime + xeno.Comp.StrikeDelay;  
            foreach (var tagged in xeno.Comp.Tagged)  
            {  
                if (!Exists(tagged))  
                    continue;  
  
                var tag = EnsureComp<PathogenPierceTaggedComponent>(tagged);  
                tag.Damage = xeno.Comp.Damage;  
                tag.StrikeAt = strikeAt;  
                tag.Origin = xeno.Owner;  
                tag.HitEffect = xeno.Comp.HitEffect;  
                tag.HitSound = xeno.Comp.HitSound;  
                Dirty(tagged, tag);  
            }  
        }  
  
        RemComp<PathogenPierceLeapingComponent>(xeno);  
    }  
  
    public override void Update(float frameTime)  
    {  
        var time = _timing.CurTime;  
  
        var leaping = EntityQueryEnumerator<PathogenPierceLeapingComponent>();  
        while (leaping.MoveNext(out var uid, out var comp))  
        {  
            if (_mob.IsDead(uid))  
            {  
                RemCompDeferred<PathogenPierceLeapingComponent>(uid);  
                continue;  
            }  
  
            if (time < comp.LeapEndTime)  
            {  
                TagTargetsInPath((uid, comp));  
                continue;  
            }  
  
            StopLeap((uid, comp));  
        }  
  
        if (_net.IsClient)  
            return;  
  
        var tagged = EntityQueryEnumerator<PathogenPierceTaggedComponent>();  
        while (tagged.MoveNext(out var uid, out var tag))  
        {  
            if (time < tag.StrikeAt)  
                continue;  
  
            RemCompDeferred<PathogenPierceTaggedComponent>(uid);  
  
            var damage = _damage.TryChangeDamage(uid, tag.Damage, origin: tag.Origin);  
            if (damage?.GetTotal() > FixedPoint2.Zero)  
            {  
                var filter = Filter.Pvs(uid, entityManager: EntityManager);  
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { uid }, filter);  
            }  
  
            SpawnAttachedTo(tag.HitEffect, uid.ToCoordinates());  
            _audio.PlayPvs(tag.HitSound, uid);  
        }  
    }  
  
    private void SetCooldown(Entity<PathogenPierceLeapComponent> xeno)  
    {  
        foreach (var (actionId, action) in _rmcActions.GetActionsWithEvent<PathogenPierceLeapActionEvent>(xeno))  
        {  
            _actions.SetCooldown(actionId, xeno.Comp.Cooldown);  
            break;  
        }  
    }  
  
    private void OnLeapingInit(Entity<PathogenPierceLeapingComponent> xeno, ref ComponentInit args)  
    {  
        var actions = _actions.GetActions(xeno);  
        foreach (var action in actions)  
        {  
            _actions.SetEnabled(action.AsNullable(), false);  
        }  
  
        _blocker.UpdateCanMove(xeno);  
    }  
  
    private void OnLeapingRemove(Entity<PathogenPierceLeapingComponent> xeno, ref ComponentRemove args)  
    {  
        // Safety net  
        if (TryComp<PhysicsComponent>(xeno, out var physics))  
        {  
            _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);  
            _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);  
        }  
  
        if (TryComp<FixturesComponent>(xeno, out var fixtures))  
        {  
            var fixture = fixtures.Fixtures.First();  
            // Restore all groups  
            _physics.SetCollisionMask(xeno, fixture.Key, fixture.Value, -1);  
        }  
  
        var actions = _actions.GetActions(xeno);  
        foreach (var action in actions)  
        {  
            _actions.SetEnabled(action.AsNullable(), true);  
        }  
  
        _blocker.UpdateCanMove(xeno);  
    }  
}
