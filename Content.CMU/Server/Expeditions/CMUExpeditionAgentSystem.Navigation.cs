using System.Diagnostics;
using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void InvestigateContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates contact, TimeSpan now, bool visible = false)
    {
        var start = Transform(uid).Coordinates;
        var localContact = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(contact));
        var delta = localContact.Position - start.Position;
        var distance = delta.Length();
        var stopRange = visible ? Math.Max(1, WeaponFireRange(uid, agent) - 1.5f) :
            agent.LastContactWasMelee ? agent.MeleeStandoffRange : 2.5f;
        if (distance <= stopRange)
        {
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Watch;
            agent.InvestigationDestination = null;
            agent.InvestigationContact = null;
            agent.Route.Clear();
            agent.RouteDestination = null;
            if (agent.ContactFromRadio)
                agent.RadioDecision = "watching-reported-area";
            return;
        }
        if (agent.InvestigationDestination is { } previous && !_transform.InRange(start, previous, 0.6f) &&
            (now < agent.NextInvestigation || agent.InvestigationContact is { } oldContact && _transform.InRange(oldContact, contact, 2)))
        {
            agent.State = CMUExpeditionAgentState.Investigate;
            Move(uid, previous);
            CheckFailure();
            return;
        }
        if (now < agent.NextInvestigation)
        {
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Watch;
            return;
        }
        agent.NextInvestigation = now + TimeSpan.FromSeconds(1);
        agent.InvestigationDestination = null;
        // Reports can be farther away than the 16-tile tactical search. Advance in bounded
        // steps and spread responders, without ever tracking the unseen target's current body.
        var ideal = start.Offset(Vector2.Normalize(delta) * Math.Min(8, distance - stopRange));
        foreach (var candidate in NearbySquadPositions(ideal, 2))
        {
            if (!ValidOrderPoint(uid, candidate) || Reserved(uid, candidate) || agent.Home is not { } home ||
                agent.LastContactWasMelee && _transform.InRange(candidate, contact, agent.MeleeStandoffRange) ||
                !_transform.InRange(home, candidate, agent.LeashRange) || _transform.InRange(start, candidate, 0.75f) ||
                agent.FailedPosition is { } failed && now < agent.AvoidPositionUntil + TimeSpan.FromSeconds(3) && _transform.InRange(candidate, failed, 1.4f))
                continue;
            agent.InvestigationDestination = candidate;
            agent.InvestigationContact = contact;
            agent.State = CMUExpeditionAgentState.Investigate;
            Move(uid, candidate);
            CheckFailure();
            return;
        }
        _steering.Unregister(uid);
        agent.State = CMUExpeditionAgentState.Watch;
        if (agent.ContactFromRadio)
            agent.RadioDecision = "support-route-blocked";

        void CheckFailure()
        {
            if (agent.State != CMUExpeditionAgentState.Watch)
            {
                if (agent.ContactFromRadio)
                    agent.RadioDecision = "advancing-to-report";
                return;
            }
            agent.InvestigationDestination = null;
            agent.NextInvestigation = now + TimeSpan.FromSeconds(1);
            if (agent.ContactFromRadio)
                agent.RadioDecision = "support-route-blocked";
        }
    }

    private bool BuildTacticalRoute(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination, bool ordered = false)
    {
        agent.Route.Clear();
        agent.RouteDestination = null;
        if (!TrySquadCoordinates(Transform(uid).Coordinates, out var start) ||
            !TrySquadCoordinates(destination, out destination) || start.EntityId != destination.EntityId)
            return false;
        // A local coordinate window also supports ordinary grids with negative tile indices.
        // No terrain array or expedition component is required for routing.
        var origin = new Vector2i((int) MathF.Floor(Math.Min(start.X, destination.X)) - 16,
            (int) MathF.Floor(Math.Min(start.Y, destination.Y)) - 16);
        var size = (int) MathF.Ceiling(Math.Max(Math.Abs(destination.X - start.X), Math.Abs(destination.Y - start.Y))) + 34;
        if (size > 1024)
            return false;
        var first = ((int) MathF.Floor(start.Y) - origin.Y) * size + (int) MathF.Floor(start.X) - origin.X;
        var last = ((int) MathF.Floor(destination.Y) - origin.Y) * size + (int) MathF.Floor(destination.X) - origin.X;
        var started = Stopwatch.GetTimestamp();
        var danger = new Dictionary<int, float>();
        var passages = new Dictionary<(int, int), bool>();
        var route = CMUTacticalRoute.Find(size, first, last, Walkable, Danger, Passage, out var expanded, ordered ? 2048 : 256);
        agent.LastRouteCells = expanded;
        agent.LastRouteMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (route == null)
            return false;
        if (!BodyFits(uid, destination))
            return false;
        // Skip unnecessary cell-centre stops along straight, traversable corridors. Keep obstacle
        // corners as waypoints instead of steering left/right at every tile in a forest.
        var previous = start;
        for (var index = 1; index < route.Count; index++)
        {
            var furthest = Math.Min(index + 5, route.Count - 1);
            // Validate the actual start and final sub-tile endpoint, not only cell centres.
            while (furthest >= index && (!TraversablePassage(uid, previous, Waypoint(furthest)) ||
                !ClearLane(uid, previous, Waypoint(furthest), 0.35f, movement: true)))
                furthest--;
            if (furthest < index)
            {
                agent.Route.Clear();
                return false;
            }
            previous = Waypoint(furthest);
            agent.Route.Enqueue(previous);
            index = furthest;
        }
        if (route.Count == 1)
        {
            if (!_transform.InRange(start, destination, ArrivalRange) &&
                (!TraversablePassage(uid, start, destination) || !ClearLane(uid, start, destination, 0.35f, movement: true)))
                return false;
            agent.Route.Enqueue(destination);
        }
        agent.RouteDestination = destination;
        agent.MoveProgressPosition = start;
        agent.MoveProgressAt = _timing.CurTime;
        return true;

        EntityCoordinates Coordinates(int cell) => new(start.EntityId, new Vector2(origin.X + cell % size + 0.5f, origin.Y + cell / size + 0.5f));
        EntityCoordinates Waypoint(int index) => index == route.Count - 1 ? destination : Coordinates(route[index]);
        bool Walkable(int cell)
        {
            var point = Coordinates(cell);
            if (!ValidOrderPoint(uid, point) ||
                !ordered && (agent.Home is not { } home || !_transform.InRange(home, point, agent.LeashRange) || !_transform.InRange(start, point, 16)))
                return false;
            return true;
        }
        float Danger(int cell)
        {
            if (ordered)
                return 0;
            if (danger.TryGetValue(cell, out var cost))
                return cost;
            var point = Coordinates(cell);
            cost = 0;
            if (agent.LastSeen is { } threat && _timing.CurTime < agent.ForgetAt &&
                RayClear(uid, _transform.ToMapCoordinates(point), _transform.ToMapCoordinates(threat)))
            {
                cost += (2 + agent.Stress * 3 + (1 - agent.Aggression)) * agent.LearnedDangerCost;
            }
            foreach (var other in agent.VisibleThreats)
            {
                // Exposure remains costly, but a large group must not turn every route into
                // an unbounded detour around the same overlapping firing lanes.
                if (cost >= 6)
                    break;
                if (agent.LastSeen is { } current && _transform.InRange(current, other, 1))
                    continue;
                if (RayClear(uid, _transform.ToMapCoordinates(point), _transform.ToMapCoordinates(other)))
                    cost += 2;
            }
            cost = Math.Min(6, cost);
            // Closing to rifle range accepts some exposure; a radio snapshot carries less certainty.
            if (agent.State == CMUExpeditionAgentState.Investigate)
                cost *= agent.ContactFromRadio ? 0.15f : 0.3f;
            danger[cell] = cost;
            return cost;
        }
        bool Passage(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!passages.TryGetValue(key, out var clear))
                passages[key] = clear = ClearLane(uid, Coordinates(a), Coordinates(b), 0.35f, movement: true);
            return clear;
        }
    }

    private EntityCoordinates? FlankPosition(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.LastSeen is not { } threat || agent.Home is not { } home)
            return null;
        var start = Transform(uid).Coordinates;
        var localThreat = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(threat));
        var original = start.Position - localThreat.Position;
        if (original.LengthSquared() < 4)
            return null;
        var side = Vector2.Normalize(new Vector2(-original.Y, original.X));
        foreach (var offset in new[] { 6f, -6f, 8f, -8f, 4f, -4f })
        {
            var point = start.Offset(side * offset - Vector2.Normalize(original) * 2);
            point = new EntityCoordinates(point.EntityId, new Vector2(MathF.Floor(point.X) + 0.5f, MathF.Floor(point.Y) + 0.5f));
            if (!_transform.InRange(home, point, agent.LeashRange) || Reserved(uid, point) || !ValidOrderPoint(uid, point))
                continue;
            var angle = Vector2.Dot(Vector2.Normalize(original), Vector2.Normalize(point.Position - localThreat.Position));
            if (angle > 0.8f || !_transform.InRange(point, threat, agent.FireRange) ||
                !FiringLaneClear(uid, point, threat))
                continue;
            return point;
        }
        return null;
    }
}
