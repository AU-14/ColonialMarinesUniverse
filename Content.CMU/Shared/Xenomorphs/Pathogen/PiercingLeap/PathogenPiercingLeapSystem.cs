using System.Linq;  
using System.Numerics;  
using Content.Shared._RMC14.Movement;  
using Content.Shared._RMC14.Pulling;  
using Content.Shared._RMC14.Stun;  
using Content.Shared._RMC14.Xenonids;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared._RMC14.Xenonids.Plasma;  
using Content.Shared.Coordinates;  
using Content.Shared.Damage.Systems;  
using Content.Shared.DoAfter;  
using Content.Shared.Effects;  
using Content.Shared.FixedPoint;  
using Content.Shared.Mobs.Components;  
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Pulling.Events;  
using Content.Shared.Popups;  
using Content.Shared.Standing;  
using Robust.Shared.Audio.Systems;  
using Robust.Shared.Map;  
using Robust.Shared.Network;  
using Robust.Shared.Physics;  
using Robust.Shared.Physics.Components;  
using Robust.Shared.Physics.Systems;  
using Robust.Shared.Player;  
using Robust.Shared.Timing;  
  
namespace Content.CMU.Shared.Xenomorphs.Pathogen.PierceLeap;  
  
public sealed partial class PathogenPiercingLeapSystem : EntitySystem  
{  
    [Dependency] private readonly SharedAudioSystem _audio = default!;  
    [Dependency] private readonly SharedBroadphaseSystem _broadphase = default!;  
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;  
    [Dependency] private readonly DamageableSystem _damageable = default!;  
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;  
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;  
    [Dependency] private readonly EntityLookupSystem _lookup = default!;  
    [Dependency] private readonly MobStateSystem _mobState = default!;  
    [Dependency] private readonly INetManager _net = default!;  
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;  
    [Dependency] private readonly SharedPopupSystem _popup = default!;  
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;  
    [Dependency] private readonly RMCSizeStunSystem _size = default!;  
    [Dependency] private readonly StandingStateSystem _standing = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
    [Dependency] private readonly SharedTransformSystem _transform = default!;  
    [Dependency] private readonly XenoSystem _xeno = default!;  
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;  
  
    private EntityQuery<PhysicsComponent> _physicsQuery;  
    private EntityQuery<FixturesComponent> _fixturesQuery;  
  
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();  
  
    public override void Initialize()  
    {  
        UpdatesAfter.Add(typeof(SharedPhysicsSystem));  
  
        _physicsQuery = GetEntityQuery<PhysicsComponent>();  
        _fixturesQuery = GetEntityQuery<FixturesComponent>();  
  
        SubscribeLocalEvent<PathogenPierceLeapComponent, PathogenPierceLeapActionEvent>(OnLeapAction);  
        SubscribeLocalEvent<PathogenPierceLeapComponent, PathogenPierceLeapDoAfterEvent>(OnLeapDoAfter);  
  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, AttemptMobCollideEvent>(OnLeapCollide);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, AttemptMobTargetCollideEvent>(OnLeapTargetCollide);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, ComponentRemove>(OnLeapingRemove);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, PhysicsSleepEvent>(OnLeapingPhysicsSleep);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, StartPullAttemptEvent>(OnLeapingStartPullAttempt);  
        SubscribeLocalEvent<PathogenPierceLeapingComponent, PullAttemptEvent>(OnLeapingPullAttempt);  
    }  
  
    // Pass through mobs instead of stopping on the first hit like the standard leap.  
    private void OnLeapCollide(Entity<PathogenPierceLeapingComponent> xeno, ref AttemptMobCollideEvent args)  
    {  
        args.Cancelled = true;  
    }  
  
    private void OnLeapTargetCollide(Entity<PathogenPierceLeapingComponent> xeno, ref AttemptMobTargetCollideEvent args)  
    {  
        args.Cancelled = true;  
    }  
  
    private void OnLeapAction(Entity<PathogenPierceLeapComponent> xeno, ref PathogenPierceLeapActionEvent args)  
    {  
        if (args.Handled)  
            return;  
  
        if (xeno.Comp.PlasmaCost > FixedPoint2.Zero &&  
            !_xenoPlasma.HasPlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))  
        {  
            return;  
        }  
  
        args.Handled = true;  
  
        var ev = new PathogenPierceLeapDoAfterEvent(GetNetCoordinates(args.Target));  
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.WindupTime, ev, xeno)  
        {  
            BreakOnMove = true,  
            BreakOnDamage = true,  
            DamageThreshold = FixedPoint2.New(10),  
            Hidden = true,  
        };  
  
        _doAfter.TryStartDoAfter(doAfter);  
    }  
  
    private void OnLeapDoAfter(Entity<PathogenPierceLeapComponent> xeno, ref PathogenPierceLeapDoAfterEvent args)  
    {  
        if (args.Handled)  
            return;  
  
        if (args.Cancelled)  
        {  
            _popup.PopupEntity(Loc.GetString("cm-xeno-leap-cancelled"), xeno, xeno);  
            return;  
        }  
  
        if (!_physicsQuery.TryGetComponent(xeno, out var physics))  
            return;  
  
        if (HasComp<PathogenPierceLeapingComponent>(xeno))  
            return;  
  
        if (xeno.Comp.PlasmaCost > FixedPoint2.Zero &&  
            !_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))  
        {  
            return;  
        }  
  
        _rmcPulling.TryStopAllPullsFromAndOn(xeno);  
  
        var origin = _transform.GetMapCoordinates(xeno);  
        var target = _transform.ToMapCoordinates(args.Coordinates);  
        var direction = target.Position - origin.Position;  
  
        if (direction == Vector2.Zero ||  
            !float.IsFinite(direction.Length() / xeno.Comp.Strength))  
        {  
            return;  
        }  
  
        var length = direction.Length();  
        var distance = Math.Clamp(length, 0.1f, xeno.Comp.Range.Float());  
        direction *= distance / length;  
  
        var impulse = direction.Normalized() * xeno.Comp.Strength * physics.Mass;  
  
        var leaping = EnsureComp<PathogenPierceLeapingComponent>(xeno);  
        args.Handled = true;  
  
        leaping.Damage = xeno.Comp.Damage;  
        leaping.StrikeDelay = xeno.Comp.StrikeDelay;  
        leaping.PathWidth = xeno.Comp.PathWidth;  
        leaping.HitEffect = xeno.Comp.HitEffect;  
        leaping.HitSound = xeno.Comp.HitSound;  
        leaping.TagEffect = xeno.Comp.TagEffect;  
        leaping.IgnoredCollisionGroupLarge = xeno.Comp.IgnoredCollisionGroupLarge;  
        leaping.IgnoredCollisionGroupSmall = xeno.Comp.IgnoredCollisionGroupSmall;  
  
        leaping.Origin = _transform.GetMoverCoordinates(xeno);  
        leaping.Destination = origin.Offset(direction);  
        leaping.Direction = direction.Normalized();  
        leaping.LeapEndTime = _timing.CurTime + TimeSpan.FromSeconds(direction.Length() / xeno.Comp.Strength);  
  
        _physics.ResetDynamics(xeno, physics);  
        _physics.ApplyLinearImpulse(xeno, impulse, body: physics);  
        _physics.SetBodyStatus(xeno, physics, BodyStatus.InAir);  
  
        if (_fixturesQuery.TryGetComponent(xeno, out var fixtures))  
        {  
            var collisionGroup = (int) leaping.IgnoredCollisionGroupSmall;  
            if (_size.TryGetSize(xeno, out var size) && size > RMCSizes.SmallXeno)  
                collisionGroup = (int) leaping.IgnoredCollisionGroupLarge;  
  
            var fixture = fixtures.Fixtures.First();  
            _physics.SetCollisionMask(xeno, fixture.Key, fixture.Value, fixture.Value.CollisionMask ^ collisionGroup);  
        }  
  
        if (xeno.Comp.LeapSound != null)  
            _audio.PlayPredicted(xeno.Comp.LeapSound, xeno, xeno);  
    }  
  
    private void OnLeapingRemove(Entity<PathogenPierceLeapingComponent> ent, ref ComponentRemove args)  
    {  
        StopLeap(ent);  
    }  
  
    private void OnLeapingPhysicsSleep(Entity<PathogenPierceLeapingComponent> ent, ref PhysicsSleepEvent args)  
    {  
        StopLeap(ent);  
    }  
  
    private void OnLeapingStartPullAttempt(Entity<PathogenPierceLeapingComponent> ent, ref StartPullAttemptEvent args)  
    {  
        args.Cancel();  
    }  
  
    private void OnLeapingPullAttempt(Entity<PathogenPierceLeapingComponent> ent, ref PullAttemptEvent args)  
    {  
        args.Cancelled = true;  
    }  
  

    private void TagTargetsInPath(Entity<PathogenPierceLeapingComponent> xeno)  
    {  
        _nearby.Clear();  
        _lookup.GetEntitiesInRange(_transform.GetMoverCoordinates(xeno), xeno.Comp.PathWidth, _nearby);  
  
        foreach (var ent in _nearby)  
        {  
            if (ent.Owner == xeno.Owner)  
                continue;  
  
            if (xeno.Comp.Tagged.Contains(ent.Owner))  
                continue;  
  
            if (!_xeno.CanAbilityAttackTarget(xeno, ent.Owner))  
                continue;  
  
            if (_standing.IsDown(ent.Owner))  
                continue;  
  
            xeno.Comp.Tagged.Add(ent.Owner);  
  
            if (_net.IsServer && xeno.Comp.TagEffect != null)  
                SpawnAttachedTo(xeno.Comp.TagEffect, ent.Owner.ToCoordinates());  
        }  
    }  
  
    private void ArmTaggedTargets(Entity<PathogenPierceLeapingComponent> xeno)  
    {  
        if (!_net.IsServer)  
            return;  
  
        foreach (var tagged in xeno.Comp.Tagged)  
        {  
            if (!Exists(tagged))  
                continue;  
  
            var tag = EnsureComp<PathogenPierceTaggedComponent>(tagged);  
            if (tag.StrikeAt != null)  
                continue;  
  
            tag.Damage = xeno.Comp.Damage;  
            tag.StrikeAt = _timing.CurTime + xeno.Comp.StrikeDelay;  
            tag.Origin = xeno.Owner;  
            tag.HitEffect = xeno.Comp.HitEffect;  
            tag.HitSound = xeno.Comp.HitSound;  
            Dirty(tagged, tag);  
        }  
    }  
  
    private void StopLeap(Entity<PathogenPierceLeapingComponent> leaping)  
    {  
        if (_physicsQuery.TryGetComponent(leaping, out var physics))  
        {  
            _physics.SetLinearVelocity(leaping, Vector2.Zero, body: physics);  
            _physics.SetBodyStatus(leaping, physics, BodyStatus.OnGround);  
        }  
  
        if (_fixturesQuery.TryGetComponent(leaping, out var fixtures))  
        {  
            var collisionGroup = (int) leaping.Comp.IgnoredCollisionGroupSmall;  
            if (_size.TryGetSize(leaping, out var size) && size > RMCSizes.SmallXeno)  
                collisionGroup = (int) leaping.Comp.IgnoredCollisionGroupLarge;  
  
            var fixture = fixtures.Fixtures.First();  
            _physics.SetCollisionMask(leaping, fixture.Key, fixture.Value, fixture.Value.CollisionMask | collisionGroup);  
        }  
  
        ArmTaggedTargets(leaping);  
        RemCompDeferred<PathogenPierceLeapingComponent>(leaping);  
    }  
  
    public override void Update(float frameTime)  
    {  
        var time = _timing.CurTime;  
  
        var leaping = EntityQueryEnumerator<PathogenPierceLeapingComponent>();  
        while (leaping.MoveNext(out var uid, out var comp))  
        {  
            var ent = (uid, comp);  
  
            TagTargetsInPath(ent);  
  
            var coordinates = _transform.GetMapCoordinates(uid);  
            if (coordinates.MapId == comp.Destination.MapId &&  
                Vector2.Dot(coordinates.Position - comp.Destination.Position, comp.Direction) >= 0)  
            {  
                _transform.SetMapCoordinates(uid, comp.Destination);  
                StopLeap(ent);  
                continue;  
            }  
  
            if (time < comp.LeapEndTime)  
                continue;  
  
            StopLeap(ent);  
        }  
  
        if (_net.IsClient)  
            return;  
  
        var tagged = EntityQueryEnumerator<PathogenPierceTaggedComponent>();  
        while (tagged.MoveNext(out var uid, out var tag))  
        {  
            if (tag.StrikeAt == null || tag.StrikeAt > time)  
                continue;  
  
            RemCompDeferred<PathogenPierceTaggedComponent>(uid);  
  
            if (!Exists(uid))  
                continue;  
  
            var damage = _damageable.TryChangeDamage(uid, tag.Damage, origin: tag.Origin, tool: tag.Origin);  
            if (damage?.GetTotal() > FixedPoint2.Zero)  
            {  
                var filter = Filter.Pvs(uid, entityManager: EntityManager)  
                    .RemoveWhereAttachedEntity(o => o == tag.Origin);  
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { uid }, filter);  
            }  
  
            if (tag.HitEffect != null)  
                SpawnAttachedTo(tag.HitEffect, uid.ToCoordinates());  
  
            if (tag.HitSound != null)  
                _audio.PlayPvs(tag.HitSound, uid);  
        }  
    }  
}
