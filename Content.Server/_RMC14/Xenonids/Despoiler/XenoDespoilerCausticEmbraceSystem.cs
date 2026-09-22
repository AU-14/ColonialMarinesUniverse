using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Physics; // CMU14
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Shared.CMU;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Xenonids.Despoiler;

public sealed partial class XenoDespoilerCausticEmbraceSystem : EntitySystem
{
    private const float TileHalfExtent = 0.5f;
    private const float UnobstructedRangeBuffer = 1f;

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private XenoDespoilerCatalyzeFlagSystem _catalyze = default!;
    [Dependency] private XenoDespoilerAcidSystem _acid = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<XenoDespoilerLingeringAcidComponent> _lingeringQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;

    public override void Initialize()
    {
        _lingeringQuery = GetEntityQuery<XenoDespoilerLingeringAcidComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        SubscribeLocalEvent<XenoDespoilerComponent, XenoDespoilerCausticEmbraceActionEvent>(OnUse);
        SubscribeLocalEvent<XenoDespoilerCausticEmbraceLeapingComponent, StartCollideEvent>(OnCausticEmbraceCollide);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerCausticEmbraceLeapingComponent>();

        while (query.MoveNext(out var uid, out var leaping))
        {
            if (time < leaping.LeapEndTime)
                continue;

            if (leaping.Empowered)
            {
                StopLeap((uid, leaping));
            }
            else
            {
                FinishLeap(uid, leaping);
            }
        }
    }

    private void OnUse(EntityUid uid, XenoDespoilerComponent comp, XenoDespoilerCausticEmbraceActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<XenoDespoilerCausticEmbraceActionComponent>(args.Action, out var action))
            return;

        var ownerXform = Transform(uid);
        var ownerMap = _xform.ToMapCoordinates(ownerXform.Coordinates);
        var targetMap = _xform.ToMapCoordinates(args.Target);
        if (ownerMap.MapId != targetMap.MapId)
            return;

        var approach = targetMap.Position - ownerMap.Position;
        var dist = approach.Length();
        if (dist < 0.01f)
            return;

        var direction = approach.Normalized();

            if (_catalyze.IsEmpowered(uid, comp))
            {
                if (!CanEmpoweredLunge(uid, action, args, dist, out var victim))
                    return;

                if (!_rmcActions.TryUseAction(args))
                    return;

                StartLeap(
                    uid,
                    args.Action,
                    action,
                    direction,
                    dist,
                    victim.Value,
                    empowered: true);

                _catalyze.TakeEmpowerment(uid, comp);
                args.Handled = true;
                return;
            }

        /*var landing = ownerXform.Coordinates.Offset(direction * action.NormalRange);
        var landingMap = _xform.ToMapCoordinates(landing);
        var landingDistance = (landingMap.Position - ownerMap.Position).Length();

        // CMU14: the default interaction mask cannot see barricades, so pounces phased through cadelines
        if (_rmcMap.IsTileBlocked(landing, CollisionGroup.Impassable | CollisionGroup.BarricadeImpassable) ||
            !_interaction.InRangeUnobstructed(uid, landing,
                range: landingDistance + UnobstructedRangeBuffer,
                collisionMask: CollisionGroup.Impassable
                | CollisionGroup.InteractImpassable
                | CollisionGroup.BarricadeImpassable))
        {
            _popup.PopupEntity(Loc.GetString("rmc-despoiler-pounce-blocked"), uid, uid);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        _xform.SetCoordinates(uid, landing);

        if (action.PounceSound is { } sound)
            _audio.PlayPvs(sound, uid);

        SpawnSplashAroundExceptBack(uid, action, landing, direction);

        args.Handled = true;*/

        if (!_physicsQuery.TryGetComponent(uid, out var physics))
            return;

        if (HasComp<XenoDespoilerCausticEmbraceLeapingComponent>(uid))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        StartLeap(uid, args.Action, action, direction, dist);
        args.Handled = true;
    }

    private void StartLeap(
        EntityUid uid,
        EntityUid actionEntity,
        XenoDespoilerCausticEmbraceActionComponent action,
        Vector2 direction,
        float distance,
        EntityUid? victim = null,
        bool empowered = false)
    {
        if (!_physicsQuery.TryGetComponent(uid, out var physics))
            return;

        distance = Math.Clamp(
            distance,
            0.1f,
            empowered ? action.EmpoweredRange : action.NormalRange);

        var leaping = EnsureComp<XenoDespoilerCausticEmbraceLeapingComponent>(uid);

        leaping.Action = actionEntity;
        leaping.Victim = victim;
        leaping.Direction = direction;
        leaping.Empowered = empowered;

        leaping.Destination = _xform.ToCoordinates(
            _xform.GetMapCoordinates(uid).Offset(direction * distance));

        var leapDuration = TimeSpan.FromSeconds(distance / action.LeapStrength);

        leaping.LeapEndTime = _timing.CurTime + leapDuration;

        var velocity = direction * (distance / (float)leapDuration.TotalSeconds);

        _physics.ResetDynamics(uid, physics);
        _physics.SetLinearVelocity(uid, velocity, body: physics);
        _physics.SetBodyStatus(uid, physics, BodyStatus.InAir);

        if (action.PounceSound is { } sound)
            _audio.PlayPvs(sound, uid);
    }

    private void StopLeap(Entity<XenoDespoilerCausticEmbraceLeapingComponent> leaping)
{
    if (_physicsQuery.TryGetComponent(leaping, out var physics))
    {
        _physics.SetLinearVelocity(leaping, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(leaping, physics, BodyStatus.OnGround);
    }

    RemCompDeferred<XenoDespoilerCausticEmbraceLeapingComponent>(leaping);
}

    private void OnCausticEmbraceCollide(
        Entity<XenoDespoilerCausticEmbraceLeapingComponent> leaping,
        ref StartCollideEvent args)
    {
        if (leaping.Comp.Empowered)
        {
            if (leaping.Comp.Victim != args.OtherEntity)
                return;

            FinishEmpoweredLeap(leaping.Owner, leaping.Comp);
            return;
        }

        if (_hive.FromSameHive(leaping.Owner, args.OtherEntity))
            return;

        FinishLeap(leaping.Owner, leaping.Comp);
    }

    private void FinishLeap(EntityUid uid,
        XenoDespoilerCausticEmbraceLeapingComponent leaping)
    {
        var action = leaping.Action;

        if (!TryComp<XenoDespoilerCausticEmbraceActionComponent>(action, out var caustic))
        {
            StopLeap((uid, leaping));
            return;
        }

        var landing = Transform(uid).Coordinates;

        SpawnSplashAroundExceptBack(
            uid,
            caustic,
            landing,
            leaping.Direction);

        StopLeap((uid, leaping));
    }

    private void FinishEmpoweredLeap(
        EntityUid uid,
        XenoDespoilerCausticEmbraceLeapingComponent leaping)
    {
        if (leaping.Victim is not { } victim ||
            !XenoDespoilerVictims.IsValidVictim(EntityManager, victim, uid))
        {
            StopLeap((uid, leaping));
            return;
        }

        if (!TryComp<XenoDespoilerCausticEmbraceActionComponent>(
                leaping.Action,
                out var action))
        {
            StopLeap((uid, leaping));
            return;
        }

        ExecuteEmpoweredLunge(uid, action, victim, leaping);
    }

    private static Vector2i SnapDirectionToTile(Vector2 dir)
    {
        return new Vector2i(Math.Sign(MathF.Round(dir.X)), Math.Sign(MathF.Round(dir.Y)));
    }

    private void SpawnSplashAroundExceptBack(EntityUid caster,
        XenoDespoilerCausticEmbraceActionComponent action,
        EntityCoordinates center,
        Vector2 forward)
    {
        var back = SnapDirectionToTile(-forward);

        var centerMap = _xform.ToMapCoordinates(center);
        var hits = _lookup.GetEntitiesIntersecting(centerMap.MapId,
            Box2.CenteredAround(centerMap.Position, new Vector2(action.SplashScanSize, action.SplashScanSize)));

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                if (dx == back.X && dy == back.Y)
                    continue;

                var tile = center.Offset(new Vector2(dx, dy));
                var tileMap = _xform.ToMapCoordinates(tile);

                var telegraph = Spawn(action.TelegraphProto, tile);
                _hive.SetSameHive(caster, telegraph);

                foreach (var ent in hits)
                {
                    if (!XenoDespoilerVictims.IsValidVictim(EntityManager, ent, caster))
                        continue;

                    var entPos = _xform.ToMapCoordinates(Transform(ent).Coordinates).Position;
                    if (Math.Abs(entPos.X - tileMap.Position.X) > TileHalfExtent) continue;
                    if (Math.Abs(entPos.Y - tileMap.Position.Y) > TileHalfExtent) continue;

                    _damageable.TryChangeDamage(ent, action.SplashDamage, ignoreResistances: false, origin: caster);
                }

                if (_random.Prob(action.LingeringAcidChance))
                {
                    var puddle = Spawn(action.LingeringAcidProto, tile);
                    _hive.SetSameHive(caster, puddle);
                    if (_lingeringQuery.TryComp(puddle, out var puddleComp))
                    {
                        puddleComp.Caster = caster;
                        Dirty(puddle, puddleComp);
                    }
                }
            }
        }
    }

    private bool CanEmpoweredLunge(EntityUid uid,
        XenoDespoilerCausticEmbraceActionComponent action,
        XenoDespoilerCausticEmbraceActionEvent args,
        float dist,
        [NotNullWhen(true)] out EntityUid? victim)
    {
        victim = null;
        if (dist > action.EmpoweredRange)
        {
            _popup.PopupEntity(Loc.GetString("rmc-despoiler-pounce-out-of-range"), uid, uid);
            return false;
        }

        victim = FindEmpoweredVictim(uid, args);
        if (victim is null)
        {
            _popup.PopupEntity(Loc.GetString("rmc-despoiler-caustic-no-target"), uid, uid);
            return false;
        }

        // CMU14: same mask as the normal pounce, barricades must block empowered lunges too
        if (!_interaction.InRangeUnobstructed(uid, victim.Value,
                range: action.EmpoweredRange + UnobstructedRangeBuffer,
                collisionMask: CollisionGroup.Impassable
                | CollisionGroup.InteractImpassable
                | CollisionGroup.BarricadeImpassable))
        {
            _popup.PopupEntity(Loc.GetString("rmc-despoiler-pounce-blocked"), uid, uid);
            victim = null;
            return false;
        }

        return true;
    }

    /*private void ExecuteEmpoweredLunge(EntityUid uid,
        XenoDespoilerCausticEmbraceActionComponent action,
        EntityUid victim)
    {
        _xform.SetCoordinates(uid, Transform(victim).Coordinates);

        if (action.PounceSound is { } sound)
            _audio.PlayPvs(sound, uid);

        _damageable.TryChangeDamage(victim, action.EmpoweredDamage, ignoreResistances: false, origin: uid);
        _acid.ApplyAcid(victim, uid, enhance: true);

        _stun.TryParalyze(victim, action.EmpoweredWeakenDuration, true);
    }*/

    private void ExecuteEmpoweredLunge(
        EntityUid uid,
        XenoDespoilerCausticEmbraceActionComponent action,
        EntityUid victim,
        XenoDespoilerCausticEmbraceLeapingComponent leaping)
    {
        _damageable.TryChangeDamage(
            victim,
            action.EmpoweredDamage,
            ignoreResistances: false,
            origin: uid);

        _acid.ApplyAcid(victim, uid, enhance: true);

        _stun.TryParalyze(
            victim,
            action.EmpoweredWeakenDuration,
            true);

        StopLeap((uid, leaping));
    }

    private EntityUid? FindEmpoweredVictim(EntityUid caster, XenoDespoilerCausticEmbraceActionEvent args)
    {
        if (args.Entity is { } target && XenoDespoilerVictims.IsValidVictim(EntityManager, target, caster))
            return target;

        var landingMap = _xform.ToMapCoordinates(args.Target);
        foreach (var ent in _lookup.GetEntitiesIntersecting(landingMap.MapId,
                     Box2.CenteredAround(landingMap.Position, new Vector2(1f, 1f))))
        {
            if (XenoDespoilerVictims.IsValidVictim(EntityManager, ent, caster))
                return ent;
        }

        return null;
    }
}
