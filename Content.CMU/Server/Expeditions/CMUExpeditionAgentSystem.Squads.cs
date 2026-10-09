using System.Linq;
using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool _orderRouteSearched;

    public bool CanOrderSquadMember(EntityUid uid) => !HasComp<ActorComponent>(uid) && _mobs.IsAlive(uid);

    public int SpawnSquad(EntityCoordinates center, int count, string variant, out int squad)
    {
        squad = 0;
        if (count is < 1 or > 12 || !IsSquadVariant(variant) || !TrySquadCoordinates(center, out center))
            return 0;

        _bodyClearCache.Clear();
        _groundCache.Clear();
        var mapUid = Transform(center.EntityId).MapUid;
        var occupied = new List<EntityCoordinates>();
        var bodies = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (bodies.MoveNext(out _, out var transform))
            if (transform.MapUid == mapUid)
                occupied.Add(_transform.ToCoordinates(center.EntityId, _transform.ToMapCoordinates(transform.Coordinates)));

        var positions = new List<EntityCoordinates>();
        foreach (var point in NearbySquadPositions(center, 10))
        {
            if (!ValidOrderPoint(center.EntityId, point) || occupied.Any(body => _transform.InRange(body, point, 1.5f)))
                continue;
            positions.Add(point);
            occupied.Add(point);
            if (positions.Count == count)
                break;
        }
        if (positions.Count == 0)
            return 0;

        TryComp<CMUExpeditionMapComponent>(center.EntityId, out var map);
        squad = map?.NextSquad ?? 1;
        // Include manually spawned squads when allocating a map-local identifier.
        var existing = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (existing.MoveNext(out var member, out var transform))
            if (transform.MapUid == mapUid)
                squad = Math.Max(squad, member.Squad + 1);
        if (map != null)
            map.NextSquad = squad + 1;
        var composition = SquadPresets[variant];
        for (var i = 0; i < positions.Count; i++)
        {
            var prototype = composition[i % composition.Length];
            var uid = Spawn(prototype, positions[i]);
            var agent = Comp<CMUExpeditionAgentComponent>(uid);
            agent.Squad = squad;
            agent.Home = positions[i];
            agent.NextThink = _timing.CurTime + TimeSpan.FromSeconds(i * 0.02);
        }
        if (map != null)
            map.GuardsSpawned = true;
        return positions.Count;
    }

    private static IEnumerable<EntityCoordinates> NearbySquadPositions(EntityCoordinates center, int radius)
    {
        yield return center;
        for (var ring = 1; ring <= radius; ring++)
        for (var y = -ring; y <= ring; y++)
        for (var x = -ring; x <= ring; x++)
            if (Math.Abs(x) == ring || Math.Abs(y) == ring)
                yield return center.Offset(new Vector2(x, y));
    }

    private bool ValidOrderPoint(EntityUid uid, EntityCoordinates point)
    {
        return GroundSafe(point) && BodyFits(uid, point);
    }

    public bool OrderSquadPoint(EntityUid uid, EntityCoordinates center, string action, List<EntityCoordinates> reserved, Direction? facing = null)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) || !CanOrderSquadMember(uid) ||
            !TrySquadCoordinates(center, out center) || Transform(uid).MapUid != Transform(center.EntityId).MapUid ||
            action == "patrol-add" && agent.PatrolPoints.Count >= 8)
            return false;
        foreach (var point in NearbySquadPositions(center, 4))
        {
            if (reserved.Any(other => _transform.InRange(other, point, 1.5f)) || !ValidOrderPoint(uid, point))
                continue;
            if (action == "patrol-add")
                agent.PatrolPoints.Add(point);
            else if (!OrderPosition(uid, point, action == "guard", facing))
                continue;
            if (action != "patrol-add")
                agent.OrderRally = center;
            reserved.Add(point);
            return true;
        }
        return false;
    }

    public bool OrderPatrol(EntityUid uid, CMUExpeditionAgentComponent agent, string action)
    {
        if (!CanOrderSquadMember(uid) || action == "patrol-start" && agent.PatrolPoints.Count < 2)
            return false;
        ResetOrders(uid, agent);
        agent.Patrolling = action == "patrol-start";
        agent.PatrolIndex = 0;
        agent.OrderedDestination = agent.Patrolling ? agent.PatrolPoints[0] : null;
        agent.Entrench = false;
        agent.Home = Transform(uid).Coordinates;
        if (action == "patrol-clear")
            agent.PatrolPoints.Clear();
        return true;
    }

    private bool FollowOrders(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.OrderedDestination is not { } destination)
            return false;
        var start = Transform(uid).Coordinates;
        if (WaitForSquad(uid, agent, now))
            return true;
        if (_transform.InRange(start, destination, 0.5f))
        {
            agent.Home = destination;
            agent.OrderRoute.Clear();
            agent.OrderBlocked = false;
            agent.OrderBlockedSince = null;
            agent.OrderedDestination = null;
            ClearTraffic(agent);
            _steering.Unregister(uid);
            if (agent.Patrolling && agent.PatrolPoints.Count >= 2)
            {
                agent.PatrolIndex = (agent.PatrolIndex + 1) % agent.PatrolPoints.Count;
                agent.OrderedDestination = agent.PatrolPoints[agent.PatrolIndex];
                agent.NextOrderRoute = now + TimeSpan.FromSeconds(1);
            }
            return true;
        }
        if (now < agent.NextOrderRoute)
        {
            _steering.Unregister(uid);
            return true;
        }
        if (agent.OrderRoute.Count == 0)
        {
            // Admin routes can span the map. Spread their larger searches across frames.
            if (_orderRouteSearched)
            {
                _steering.Unregister(uid);
                return true;
            }
            _orderRouteSearched = true;
            if (!BuildTacticalRoute(uid, agent, destination, ordered: true))
            {
                BlockOrder();
                return true;
            }
            foreach (var point in agent.Route)
                agent.OrderRoute.Enqueue(point);
            agent.Route.Clear();
            agent.RouteDestination = null;
            agent.OrderBlocked = false;
        }
        AdvanceRoute(uid, agent.OrderRoute, start);
        if (!agent.OrderRoute.TryPeek(out var next))
            return true;
        UpdateMoveProgress(agent, start, next, now);
        if (agent.LastOrderProgressPosition is not { } progress || !_transform.InRange(start, progress, 0.5f))
        {
            agent.LastOrderProgressPosition = start;
            agent.OrderBlockedSince = null;
        }
        if (!WaitingAtDoor(uid, agent) && (now - agent.MoveProgressAt >= TimeSpan.FromSeconds(2) ||
            TryComp<NPCSteeringComponent>(uid, out var steering) && steering.Status == SteeringStatus.NoPath) ||
            !RoutePassage(uid, start, next))
        {
            var detour = new Queue<EntityCoordinates>();
            if (LocalDetour(uid, agent, next, detour))
            {
                var remaining = agent.OrderRoute.Skip(1).ToArray();
                agent.OrderRoute.Clear();
                foreach (var point in detour.Concat(remaining))
                    agent.OrderRoute.Enqueue(point);
                return true;
            }
            var delta = next.Position - start.Position;
            if (delta.LengthSquared() > 0.01f)
            {
                agent.TrafficBlockedPoint = start.Offset(Vector2.Normalize(delta) * Math.Min(1, delta.Length()));
                agent.AvoidTrafficUntil = now + TimeSpan.FromSeconds(6);
            }
            BlockOrder(retrySoon: true);
            return true;
        }
        // The combat leash follows travel progress; contact interrupts orders near this position.
        agent.Home = start;
        Move(uid, next, routeWaypoint: agent.OrderRoute.Count > 1, validated: true);
        return true;

        void BlockOrder(bool retrySoon = false)
        {
            agent.OrderRoute.Clear();
            agent.OrderBlocked = true;
            agent.OrderBlockedSince ??= now;
            agent.OrderFailures++;
            agent.NextOrderRoute = now + TimeSpan.FromSeconds(retrySoon ? 0.5 : 3);
            _steering.Unregister(uid);
        }
    }
}
