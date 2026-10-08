using System.Diagnostics;
using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool BuildTacticalRoute(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination)
    {
        agent.Route.Clear();
        agent.RouteDestination = null;
        var start = Transform(uid).Coordinates;
        if (start.EntityId != destination.EntityId || !TryComp<CMUExpeditionMapComponent>(start.EntityId, out var expedition))
            return false;
        var plan = expedition.Plan;
        var first = plan.Index((int) start.X, (int) start.Y);
        var last = plan.Index((int) destination.X, (int) destination.Y);
        if (first < 0 || last < 0 || first >= plan.Size * plan.Size || last >= plan.Size * plan.Size)
            return false;
        var started = Stopwatch.GetTimestamp();
        var danger = new Dictionary<int, float>();
        var passages = new Dictionary<(int, int), bool>();
        var route = CMUTacticalRoute.Find(plan.Size, first, last, Walkable, Danger, Passage, out var expanded);
        agent.LastRouteCells = expanded;
        agent.LastRouteMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (route == null)
            return false;
        // The initial partial cell also needs clearance; the grid search only checked centre-to-centre segments.
        if (route.Count > 1 && !ClearLane(uid, start, Coordinates(route[1]), 0.35f))
            return false;
        for (var index = 1; index < route.Count - 1; index++)
            agent.Route.Enqueue(Coordinates(route[index]));
        agent.Route.Enqueue(destination);
        agent.RouteDestination = destination;
        return true;

        EntityCoordinates Coordinates(int cell) => new(start.EntityId, new Vector2(cell % plan.Size + 0.5f, cell / plan.Size + 0.5f));
        bool Walkable(int cell)
        {
            var point = Coordinates(cell);
            if (plan.Terrain[cell] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff ||
                !BodyFits(uid, point) || agent.Home is not { } home ||
                !_transform.InRange(home, point, agent.LeashRange) || !_transform.InRange(start, point, 16) || GrenadeDanger(point))
                return false;
            foreach (var fire in plan.FirePockets)
            {
                if (Math.Abs(point.X - fire.X - 0.5f) <= 3 && Math.Abs(point.Y - fire.Y - 0.5f) <= 3)
                    return false;
            }
            return true;
        }
        float Danger(int cell)
        {
            if (danger.TryGetValue(cell, out var cost))
                return cost;
            var point = Coordinates(cell);
            cost = 0;
            if (agent.LastSeen is { } threat && _timing.CurTime < agent.ForgetAt &&
                RayClear(uid, _transform.ToMapCoordinates(point), _transform.ToMapCoordinates(threat)))
                cost += (2 + agent.Stress * 3 + (1 - agent.Aggression)) * agent.LearnedDangerCost;
            foreach (var other in agent.VisibleThreats)
            {
                if (agent.LastSeen is { } current && _transform.InRange(current, other, 1))
                    continue;
                if (RayClear(uid, _transform.ToMapCoordinates(point), _transform.ToMapCoordinates(other)))
                    cost += 2;
            }
            danger[cell] = cost;
            return cost;
        }
        bool Passage(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!passages.TryGetValue(key, out var clear))
                passages[key] = clear = ClearLane(uid, Coordinates(a), Coordinates(b), 0.35f);
            return clear;
        }
    }

    private EntityCoordinates? FlankPosition(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.LastSeen is not { } threat || agent.Home is not { } home)
            return null;
        var start = Transform(uid).Coordinates;
        if (!TryComp<CMUExpeditionMapComponent>(start.EntityId, out var expedition))
            return null;
        var localThreat = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(threat));
        var original = start.Position - localThreat.Position;
        if (original.LengthSquared() < 4)
            return null;
        var side = Vector2.Normalize(new Vector2(-original.Y, original.X));
        foreach (var offset in new[] { 6f, -6f, 8f, -8f, 4f, -4f })
        {
            var point = start.Offset(side * offset - Vector2.Normalize(original) * 2);
            point = new EntityCoordinates(point.EntityId, new Vector2(MathF.Floor(point.X) + 0.5f, MathF.Floor(point.Y) + 0.5f));
            if (!_transform.InRange(home, point, agent.LeashRange) || Reserved(uid, point) || GrenadeDanger(point) ||
                point.X < 1 || point.Y < 1 || point.X >= expedition.Plan.Size - 1 || point.Y >= expedition.Plan.Size - 1)
                continue;
            var cell = expedition.Plan.Index((int) point.X, (int) point.Y);
            var angle = Vector2.Dot(Vector2.Normalize(original), Vector2.Normalize(point.Position - localThreat.Position));
            if (angle > 0.8f || !_transform.InRange(point, threat, agent.FireRange) ||
                !BodyFits(uid, point) ||
                expedition.Plan.Terrain[cell] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff ||
                !FiringLaneClear(uid, point, threat))
                continue;
            return point;
        }
        return null;
    }
}
