using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.Stomp;

public sealed partial class XenoStompSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedHitLocationSystem _hitLocation = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedInteractionSystem _interact = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoStompComponent, XenoStompActionEvent>(OnXenoStompAction);
        SubscribeLocalEvent<XenoStompComponent, XenoStompDoAfterEvent>(OnXenoStompDoAfter);
        SubscribeLocalEvent<XenoStompComponent, XenoDirectionalStompActionEvent>(OnXenoDirectionalStompAction);
    }

    private readonly HashSet<Entity<MobStateComponent>> _receivers = new();

    private void OnXenoStompAction(Entity<XenoStompComponent> xeno, ref XenoStompActionEvent args)
    {
        var attemptEv = new XenoStompAttemptEvent();
        RaiseLocalEvent(xeno, ref attemptEv);

        if (attemptEv.Cancelled)
            return;

        if (_mobState.IsDead(xeno))
            return;

        if (!_xenoPlasma.HasPlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;
        var ev = new XenoStompDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.Delay, ev, xeno)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoStompDoAfter(Entity<XenoStompComponent> xeno, ref XenoStompDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            foreach (var action in _rmcActions.GetActionsWithEvent<XenoStompActionEvent>(xeno))
            {
                _actions.ClearCooldown(action.AsNullable());
            }

            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        if (!TryComp(xeno, out TransformComponent? xform))
            return;

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        if (xeno.Comp.Directional)
        {
            var facing = _transform.GetWorldRotation(xform);
            DoDirectionalStomp(xeno, xform, facing);
        }
        else
            DoCircularStomp(xeno, xform);

        if (_net.IsServer && xeno.Comp.SelfEffect is not null)
            SpawnAttachedTo(xeno.Comp.SelfEffect, xeno.Owner.ToCoordinates());
    }

    private void OnXenoDirectionalStompAction(Entity<XenoStompComponent> xeno, ref XenoDirectionalStompActionEvent args)
    {
        if (args.Handled)
            return;

        if (!xeno.Comp.Directional)
            return;

        var attemptEv = new XenoStompAttemptEvent();
        RaiseLocalEvent(xeno, ref attemptEv);

        if (attemptEv.Cancelled)
            return;

        if (_mobState.IsDead(xeno))
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        if (!TryComp(xeno, out TransformComponent? xform))
            return;

        args.Handled = true;

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        var origin = _transform.GetMapCoordinates(xeno);
        var targetMap = _transform.ToMapCoordinates(args.Target);
        var direction = (targetMap.Position - origin.Position).ToWorldAngle();

        DoDirectionalStomp(xeno, xform, direction);

        if (_net.IsServer && xeno.Comp.SelfEffect is not null)
            SpawnAttachedTo(xeno.Comp.SelfEffect, xeno.Owner.ToCoordinates());
    }

    private void DoCircularStomp(Entity<XenoStompComponent> xeno, TransformComponent xform)
    {
        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, xeno.Comp.Range, _receivers);

        using var targetingSuppression = _hitLocation.SuppressBodyZoneTargeting(xeno.Owner);
        foreach (var receiver in _receivers)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, receiver))
                continue;

            if (xeno.Comp.SlowBigInsteadOfStun && _size.TryGetSize(receiver, out var size) && size >= RMCSizes.Big)
                _slow.TrySlowdown(receiver, xeno.Comp.DebuffsHurtXenosMore ? _xeno.TryApplyXenoDebuffMultiplier(receiver, xeno.Comp.ParalyzeTime)
                    : xeno.Comp.ParalyzeTime, true);
            else if (!xeno.Comp.ParalyzeUnderOnly)
                _stun.TryParalyze(receiver, xeno.Comp.DebuffsHurtXenosMore ? _xeno.TryApplyXenoDebuffMultiplier(receiver, xeno.Comp.ParalyzeTime)
                    : xeno.Comp.ParalyzeTime, true);

            if (xeno.Comp.Slows)
                _slow.TrySuperSlowdown(receiver, xeno.Comp.SlowTime, true);

            if (xform.Coordinates.TryDistance(EntityManager, receiver.Owner.ToCoordinates(), out var distance) && distance <= xeno.Comp.ShortRange)
            {
                if (!_standing.IsDown(receiver))
                    continue;

                var damage = _damageable.TryChangeDamage(receiver, _xeno.TryApplyXenoSlashDamageMultiplier(receiver, xeno.Comp.Damage), origin: xeno, tool: xeno);
                if (damage?.GetTotal() > FixedPoint2.Zero)
                {
                    var filter = Filter.Pvs(receiver, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
                    _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { receiver }, filter);
                }

                if (xeno.Comp.ParalyzeUnderOnly && _size.TryGetSize(receiver, out size) && size < RMCSizes.Big)
                    _stun.TryParalyze(receiver, xeno.Comp.DebuffsHurtXenosMore ? _xeno.TryApplyXenoDebuffMultiplier(receiver, xeno.Comp.ParalyzeTime)
                    : xeno.Comp.ParalyzeTime, true);
            }
        }
    }

    private void DoDirectionalStomp(Entity<XenoStompComponent> xeno, TransformComponent xform, Angle direction)
    {
        var origin = _transform.GetMapCoordinates(xeno);
        var halfAngle = new Angle(xeno.Comp.DirectionalAngle.Theta / 2);
        var range = xeno.Comp.DirectionalRange;

        if (_net.IsClient)
            return;

        // Spawn tile effects in the cone area.
        if (xeno.Comp.DirectionalTileEffect is { } tileEffect &&
            _transform.GetGrid(xeno.Owner) is { } gridId &&
            TryComp<MapGridComponent>(gridId, out var grid))
        {
            var tileRange = (int) MathF.Ceiling(range);
            var center = _map.CoordinatesToTile(gridId, grid, origin);

            for (var x = -tileRange; x <= tileRange; x++)
            {
                for (var y = -tileRange; y <= tileRange; y++)
                {
                    var tilePos = center + new Vector2i(x, y);
                    var worldPos = _map.GridTileToWorld(gridId, grid, tilePos).Position;
                    var diff = worldPos - origin.Position;
                    if (diff.Length() > range || diff.Length() < 0.1f)
                        continue;

                    var angleDiff = Angle.ShortestDistance(direction, diff.ToWorldAngle());
                    if (Math.Abs(angleDiff.Theta) > halfAngle.Theta)
                        continue;

                    var tileCenter = _map.GridTileToLocal(gridId, grid, tilePos);
                    SpawnAtPosition(tileEffect, tileCenter);
                }
            }
        }

        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(origin, range, _receivers);

        using var targetingSuppression = _hitLocation.SuppressBodyZoneTargeting(xeno.Owner);
        foreach (var ent in _receivers)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, ent))
                continue;

            // Cone check: is the entity within the arc?
            var entMap = _transform.GetMapCoordinates(ent);
            var toEnt = entMap.Position - origin.Position;
            if (toEnt.Length() < 0.1f)
                continue;

            var angleDiff = Angle.ShortestDistance(direction, toEnt.ToWorldAngle());
            if (Math.Abs(angleDiff.Theta) > halfAngle.Theta)
                continue;

            var damage = _damageable.TryChangeDamage(ent, _xeno.TryApplyXenoSlashDamageMultiplier(ent, xeno.Comp.Damage), origin: xeno, tool: xeno);
            if (damage?.GetTotal() > FixedPoint2.Zero)
            {
                var filter = Filter.Pvs(ent, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { ent }, filter);
            }

            _stun.TryParalyze(ent, xeno.Comp.DebuffsHurtXenosMore
                ? _xeno.TryApplyXenoDebuffMultiplier(ent, xeno.Comp.ParalyzeTime)
                : xeno.Comp.ParalyzeTime, true);

            if (xeno.Comp.Slows)
                _slow.TrySuperSlowdown(ent, xeno.Comp.SlowTime, true);

            if (xeno.Comp.KnockBackDistance > 0)
            {
                _rmcPulling.TryStopAllPullsFromAndOn(ent);
                _obstacleSlamming.MakeImmune(ent);
                _size.KnockBack(ent, origin, xeno.Comp.KnockBackDistance, xeno.Comp.KnockBackDistance, 10);
            }

            if (xeno.Comp.ScreenShakeStrength > 0)
                _cameraShake.ShakeCamera(ent, 6, xeno.Comp.ScreenShakeStrength);
        }
    }
}
