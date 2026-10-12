using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

/// <summary>
/// Clawed Sweep, built on the directional cone of <c>XenoStompSystem</c>: telegraph the cone, wait out a
/// short do-after, then rend, knock down and disarm every non-allied living mob in it.
/// </summary>
public sealed partial class CMUClawedSweepSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMUWendigoAllySystem _ally = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private CMUWendigoPounceSystem _pounce = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private readonly HashSet<Entity<MobStateComponent>> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUClawedSweepComponent, CMUClawedSweepActionEvent>(OnSweepAction);
        SubscribeLocalEvent<CMUClawedSweepComponent, CMUClawedSweepDoAfterEvent>(OnSweepDoAfter);
    }

    private void OnSweepAction(Entity<CMUClawedSweepComponent> ent, ref CMUClawedSweepActionEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent))
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        var diff = target.Position - origin.Position;
        var direction = target.MapId == origin.MapId && diff.LengthSquared() > 0.01f
            ? diff.ToWorldAngle()
            : _transform.GetWorldRotation(ent);

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, new CMUClawedSweepDoAfterEvent(direction), ent)
        {
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            _audio.PlayPvs(ent.Comp.WindupSound, ent);
            SpawnTelegraph(ent, direction);
        }
    }

    private void OnSweepDoAfter(Entity<CMUClawedSweepComponent> ent, ref CMUClawedSweepDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            foreach (var action in _rmcActions.GetActionsWithEvent<CMUClawedSweepActionEvent>(ent))
            {
                _actions.ClearCooldown(action.AsNullable());
            }

            return;
        }

        PerformSweep(ent.AsNullable(), args.Direction);
    }

    /// <summary>
    /// Applies the sweep immediately (no telegraph, no delay) in the cone facing <paramref name="direction"/>.
    /// Server-only. Returns the mobs that were hit.
    /// </summary>
    public List<EntityUid> PerformSweep(Entity<CMUClawedSweepComponent?> user, Angle direction)
    {
        var hit = new List<EntityUid>();
        if (_net.IsClient || !Resolve(user, ref user.Comp, false))
            return hit;

        var comp = user.Comp;
        var uid = user.Owner;
        var origin = _transform.GetMapCoordinates(uid);
        var halfAngle = comp.ConeAngle.Theta / 2;

        _audio.PlayPvs(comp.SweepSound, uid);

        _receivers.Clear();
        _lookup.GetEntitiesInRange(origin, comp.Range, _receivers);

        foreach (var receiver in _receivers)
        {
            if (!CanSweep(uid, receiver))
                continue;

            var receiverMap = _transform.GetMapCoordinates(receiver);
            var toTarget = receiverMap.Position - origin.Position;
            if (toTarget.Length() < 0.1f)
                continue;

            var angleDiff = Angle.ShortestDistance(direction, toTarget.ToWorldAngle());
            if (Math.Abs(angleDiff.Theta) > halfAngle)
                continue;

            if (IsBlockedByObstacle(origin, receiverMap, uid))
                continue;

            hit.Add(receiver);
        }

        foreach (var target in hit)
        {
            var damage = _damageable.TryChangeDamage(
                target,
                _xeno.TryApplyXenoSlashDamageMultiplier(target, comp.Damage),
                origin: uid,
                tool: uid,
                impact: DamageImpact.XenoRendingSlash(comp.RendingTier));

            if (damage?.GetTotal() > FixedPoint2.Zero)
            {
                var filter = Filter.Pvs(target, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == uid);
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
            }

            // No force: Yautja and other stun-resistant mobs resist as usual.
            _stun.TryParalyze(target, _xeno.TryApplyXenoDebuffMultiplier(target, comp.KnockdownTime), true);

            if (comp.DropHeldItems && !TerminatingOrDeleted(target))
                _pounce.DropHeldItems(uid, target);
        }

        return hit;
    }

    private bool CanSweep(EntityUid user, EntityUid target)
    {
        if (target == user || _mobState.IsDead(target))
            return false;

        if (HasComp<DevouredComponent>(target) || HasComp<XenoNestedComponent>(target))
            return false;

        return !_ally.IsWendigoAlly(user, target);
    }

    private void SpawnTelegraph(Entity<CMUClawedSweepComponent> ent, Angle direction)
    {
        if (ent.Comp.TelegraphEffect is not { } effect ||
            _transform.GetGrid(ent.Owner) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? grid))
        {
            return;
        }

        var origin = _transform.GetMapCoordinates(ent);
        var range = ent.Comp.Range;
        var halfAngle = ent.Comp.ConeAngle.Theta / 2;
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
                if (Math.Abs(angleDiff.Theta) > halfAngle)
                    continue;

                if (IsBlockedByObstacle(origin, new MapCoordinates(worldPos, origin.MapId), ent.Owner))
                    continue;

                SpawnAtPosition(effect, _map.GridTileToLocal(gridId, grid, tilePos));
            }
        }
    }

    // Same barricade line-of-sight ray as XenoStompSystem.IsBlockedByObstacle (private there).
    private bool IsBlockedByObstacle(MapCoordinates origin, MapCoordinates target, EntityUid ignore)
    {
        if (origin.MapId != target.MapId)
            return true;

        var diff = target.Position - origin.Position;
        var distance = diff.Length();
        if (distance < 0.1f)
            return false;

        var mask = CollisionGroup.Impassable | CollisionGroup.InteractImpassable | CollisionGroup.BarricadeImpassable;
        var ray = new CollisionRay(origin.Position, diff.Normalized(), (int) mask);
        foreach (var _ in _physics.IntersectRay(origin.MapId, ray, distance, ignore, returnOnFirstHit: true))
        {
            return true;
        }

        return false;
    }
}
