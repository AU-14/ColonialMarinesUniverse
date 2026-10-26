using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem
{
    [Dependency] private XenoRestSystem _rest = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedXenoConstructionSystem _construction = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedMapSystem _map = default!;
    private static readonly Vector2[] WorkSides = [Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY];

    private void InitializeBehavior()
    {
        SubscribeLocalEvent<CMUSwarmAgentComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<CMUSwarmAgentComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnSpeed(Entity<CMUSwarmAgentComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Slow)
            args.ModifySpeed(0.55f, 0.55f);
    }

    private void OnDamage(Entity<CMUSwarmAgentComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;
        Wake(ent, ent.Comp);
        ent.Comp.PauseUntil = TimeSpan.Zero;
        ent.Comp.NextSense = TimeSpan.Zero;
    }

    private void SetSlow(EntityUid member, CMUSwarmAgentComponent agent, bool slow)
    {
        if (agent.Slow == slow)
            return;
        agent.Slow = slow;
        _speed.RefreshMovementSpeedModifiers(member);
    }

    private void Wake(EntityUid member, CMUSwarmAgentComponent agent)
    {
        if (HasComp<XenoRestingComponent>(member))
            _rest.TryRestAction(member, ignoreCooldown: true);
        agent.WakeAt = TimeSpan.Zero;
        agent.RestAt = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(60, 91));
    }

    private EntityUid? OrderAnchor(CMUSwarmAgentComponent agent, EntityUid? fallback)
    {
        if (agent.Order is not { } order)
            return fallback;
        if (order.Kind == CMUSwarmOrderKind.Follow)
            return order.Follow;
        if (order.Flags.Count > 0)
            return order.Flags[Math.Clamp(agent.Waypoint, 0, order.Flags.Count - 1)];
        return agent.Home?.EntityId;
    }

    private void ThinkOrder(EntityUid member, CMUSwarmAgentComponent agent, EntityUid anchor,
        CMUSwarmControllerComponent controller, int index)
    {
        RemComp<NPCMeleeCombatComponent>(member);
        var kind = agent.Order?.Kind ?? CMUSwarmOrderKind.Guard;
        SetSlow(member, agent, kind is CMUSwarmOrderKind.Idle or CMUSwarmOrderKind.Roam);
        if (Transform(member).MapUid != Transform(anchor).MapUid || HasComp<CMUSwarmTraversalComponent>(member))
        {
            WakeIfResting(member, agent);
            if (TraverseToAnchor(member, anchor))
                return;
        }
        if (kind is CMUSwarmOrderKind.Idle or CMUSwarmOrderKind.Roam)
        {
            Roam(member, agent, kind == CMUSwarmOrderKind.Roam
                ? new EntityCoordinates(anchor, Vector2.Zero) : agent.Home!.Value);
            return;
        }

        WakeIfResting(member, agent);
        if (TraverseToAnchor(member, anchor))
            return;
        if (kind is CMUSwarmOrderKind.Build or CMUSwarmOrderKind.Weed && agent.Order is { } work)
        {
            Work(member, agent, anchor, work);
            return;
        }
        if (kind == CMUSwarmOrderKind.Patrol && agent.Order is { } patrol)
        {
            if (agent.PauseUntil > _timing.CurTime)
            {
                _steering.Unregister(member);
                return;
            }
            if (Within(member, anchor, 1.2f))
            {
                _steering.Unregister(member);
                agent.Waypoint = (agent.Waypoint + 1) % patrol.Flags.Count;
                agent.PauseUntil = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(2, 6));
                return;
            }
        }
        // Stable spacing avoids orbiting. Arrival unregisters the mover until the goal moves.
        var slot = agent.Order == null ? index : agent.FormationSlot;
        var angle = slot * 2.39996323f;
        var radius = controller.FormationRadius * MathF.Sqrt((slot + 0.5f) / 15f);
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        var destination = new EntityCoordinates(anchor, offset);
        if (kind == CMUSwarmOrderKind.Patrol ||
            !_interaction.InRangeUnobstructed(_transform.GetMapCoordinates(anchor), _transform.ToMapCoordinates(destination), radius + 0.1f))
            destination = new EntityCoordinates(anchor, Vector2.Zero);
        MoveTo(member, destination, 0.35f);
    }

    private void Roam(EntityUid member, CMUSwarmAgentComponent agent, EntityCoordinates home)
    {
        var now = _timing.CurTime;
        if (agent.RestAt == TimeSpan.Zero)
            agent.RestAt = now + TimeSpan.FromSeconds(_random.Next(60, 91));
        if (HasComp<XenoRestingComponent>(member))
        {
            if (agent.WakeAt > now)
                return;
            Wake(member, agent);
        }
        if (now >= agent.RestAt)
        {
            _steering.Unregister(member);
            agent.Destination = null;
            if (_rest.TryRestAction(member))
                agent.WakeAt = now + TimeSpan.FromSeconds(_random.Next(20, 41));
            return;
        }
        if (agent.PauseUntil > now)
        {
            _steering.Unregister(member);
            return;
        }
        if (agent.Destination is { } destination)
        {
            if (Reached(member, destination, 0.7f) || now >= agent.MoveUntil ||
                TryComp<NPCSteeringComponent>(member, out var failed) && failed.Status == SteeringStatus.NoPath)
            {
                _steering.Unregister(member);
                agent.Destination = null;
                agent.PauseUntil = now + TimeSpan.FromSeconds(_random.Next(8, 21));
                return;
            }
            MoveTo(member, destination, 0.5f);
            return;
        }

        // Four bounded samples per journey, never a tile sweep or per-tick reroll.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var candidate = home.Offset(new Vector2(_random.NextFloat(-10f, 10f), _random.NextFloat(-7f, 7f)));
            if (!HasFloor(candidate) || Reached(member, candidate, 2) || !_interaction.InRangeUnobstructed(
                    _transform.ToMapCoordinates(home), _transform.ToMapCoordinates(candidate), 13))
                continue;
            agent.Destination = candidate;
            agent.MoveUntil = now + TimeSpan.FromSeconds(30);
            MoveTo(member, candidate, 0.5f);
            return;
        }
        agent.PauseUntil = now + TimeSpan.FromSeconds(5);
    }

    private bool HasFloor(EntityCoordinates coordinates)
    {
        return _transform.GetGrid(coordinates) is { } grid && TryComp<MapGridComponent>(grid, out var tiles) &&
               _map.TryGetTileRef(grid, tiles, _map.CoordinatesToTile(grid, tiles, coordinates), out var tile) && !tile.Tile.IsEmpty;
    }

    private void WakeIfResting(EntityUid member, CMUSwarmAgentComponent agent)
    {
        if (HasComp<XenoRestingComponent>(member))
            Wake(member, agent);
    }

    private bool Reached(EntityUid member, EntityCoordinates destination, float range) =>
        Transform(member).Coordinates.TryDistance(EntityManager, _transform, destination, out var distance) && distance <= range;

    private void MoveTo(EntityUid member, EntityCoordinates destination, float range)
    {
        if (Reached(member, destination, range))
        {
            _steering.Unregister(member);
            return;
        }
        if (TryComp<NPCSteeringComponent>(member, out var current))
        {
            if (current.Status == SteeringStatus.NoPath)
                _steering.Unregister(member);
            else if (current.Coordinates == destination)
                return;
        }
        var steering = _steering.Register(member, destination);
        ConfigureSteering(steering);
        steering.Range = range;
    }

    private bool WorkRunning(EntityUid member)
    {
        if (!TryComp<DoAfterComponent>(member, out var afters))
            return false;
        foreach (var after in afters.DoAfters.Values)
        {
            if (after.Args.Event is XenoSecreteStructureDoAfterEvent && _doAfter.IsRunning(after.Id))
                return true;
        }
        return false;
    }

    private void CancelWork(EntityUid member, CMUSwarmAgentComponent agent)
    {
        if (TryComp<DoAfterComponent>(member, out var afters))
        {
            foreach (var after in afters.DoAfters.Values)
            {
                if (after.Args.Event is XenoSecreteStructureDoAfterEvent && _doAfter.IsRunning(after.Id))
                    _doAfter.Cancel(after.Id);
            }
        }
        if (agent.Order is { } order && order.Worker == member)
            order.Worker = null;
    }

    private void Work(EntityUid member, CMUSwarmAgentComponent agent, EntityUid flag, CMUSwarmOrder order)
    {
        if (order.Complete || agent.NextWork > _timing.CurTime)
        {
            _steering.Unregister(member);
            return;
        }
        if (order.Worker is { } worker && worker != member && !TerminatingOrDeleted(worker) && _mobState.IsAlive(worker))
        {
            _steering.Unregister(member);
            return;
        }
        order.Worker = member;
        if (WorkRunning(member))
            return;
        var position = Transform(flag).Coordinates.SnapToGrid(EntityManager);
        EntProtoId prototype = order.Kind == CMUSwarmOrderKind.Weed ? "XenoWeedsSource" : order.Structure switch
        {
            CMUSwarmStructure.Membrane => "WallXenoMembrane",
            CMUSwarmStructure.Door => "DoorXenoResin",
            _ => "WallXenoResin",
        };
        using (var existing = _rmcMap.GetAnchoredEntitiesEnumerator<MetaDataComponent>(position))
        {
            while (existing.MoveNext(out var entity))
            {
                if (MetaData(entity).EntityPrototype?.ID == prototype.Id)
                {
                    order.Complete = true;
                    _steering.Unregister(member);
                    return;
                }
            }
        }
        // Build from an accessible side, not an arbitrary east tile which may be inside a wall.
        var stand = position;
        if (order.Kind == CMUSwarmOrderKind.Build && agent.Destination is { } approach)
            stand = approach;
        else if (order.Kind == CMUSwarmOrderKind.Build)
        {
            var best = float.MaxValue;
            foreach (var offset in WorkSides)
            {
                var candidate = position.Offset(offset);
                if (!HasFloor(candidate) || !_interaction.InRangeUnobstructed(
                        _transform.ToMapCoordinates(position), _transform.ToMapCoordinates(candidate), 1.1f) ||
                    !Transform(member).Coordinates.TryDistance(EntityManager, _transform, candidate, out var distance) || distance >= best)
                    continue;
                stand = candidate;
                best = distance;
            }
            if (best == float.MaxValue)
            {
                _steering.Unregister(member);
                order.Worker = null;
                agent.NextWork = _timing.CurTime + TimeSpan.FromSeconds(5);
                return;
            }
            agent.Destination = stand;
        }
        if (!Reached(member, stand, 0.6f))
        {
            MoveTo(member, stand, 0.4f);
            if (agent.MoveUntil == TimeSpan.Zero)
                agent.MoveUntil = _timing.CurTime + TimeSpan.FromSeconds(20);
            if (agent.MoveUntil <= _timing.CurTime)
            {
                order.Worker = null;
                agent.Destination = null;
                agent.MoveUntil = TimeSpan.Zero;
                agent.NextWork = _timing.CurTime + TimeSpan.FromSeconds(10);
            }
            return;
        }
        _steering.Unregister(member);
        agent.MoveUntil = TimeSpan.Zero;
        agent.NextWork = _timing.CurTime + TimeSpan.FromSeconds(5);
        if (order.Kind == CMUSwarmOrderKind.Weed)
        {
            foreach (var action in _rmcActions.GetActionsWithEvent<XenoPlantWeedsActionEvent>(member))
            {
                if (action.Comp.Enabled && !(action.Comp.Cooldown?.End > _timing.CurTime))
                    _actions.PerformAction(member, action);
                break;
            }
        }
        else if (_construction.TryChooseSwarmStructure(member, prototype))
        {
            foreach (var action in _rmcActions.GetActionsWithEvent<XenoSecreteStructureActionEvent>(member))
            {
                if (action.Comp.Enabled && !(action.Comp.Cooldown?.End > _timing.CurTime))
                    _actions.PerformAction(member, action, new XenoSecreteStructureActionEvent { Target = position });
                break;
            }
        }
        if (!WorkRunning(member))
            order.Worker = null;
    }
}
