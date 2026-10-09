using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool _localRouteSearched;

    private bool LocalDetour(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination,
        Queue<EntityCoordinates> output, bool stalled = true)
    {
        var now = _timing.CurTime;
        if (_localRouteSearched || now < agent.NextLocalDetour ||
            !TrySquadCoordinates(Transform(uid).Coordinates, out var start) ||
            !TrySquadCoordinates(destination, out destination) || start.EntityId != destination.EntityId ||
            !_transform.InRange(start, destination, 8) || !BodyFits(uid, start) || !ValidOrderPoint(uid, destination))
            return false;
        _localRouteSearched = true;
        agent.NextLocalDetour = now + TimeSpan.FromSeconds(3);
        // Half-tile nodes can pass either side of a streetlight instead of treating its
        // occupied tile centre as a whole wall. Every edge still sweeps the real body.
        const float step = 0.5f;
        var origin = new Vector2(MathF.Floor(Math.Min(start.X, destination.X) / step) * step - 2,
            MathF.Floor(Math.Min(start.Y, destination.Y) / step) * step - 2);
        var size = (int) MathF.Ceiling(Math.Max(Math.Abs(start.X - destination.X), Math.Abs(start.Y - destination.Y)) / step) + 10;
        int Cell(EntityCoordinates point) => (int) MathF.Floor((point.Y - origin.Y) / step) * size +
            (int) MathF.Floor((point.X - origin.X) / step);
        var first = Cell(start);
        var last = Cell(destination);
        if (first == last)
            return false;
        var toward = Vector2.Normalize(destination.Position - start.Position);
        var avoid = start.Position + toward * 0.8f;
        var path = CMUTacticalRoute.Find(size, first, last, Walkable, _ => 0,
            (a, b) => TraversablePassage(uid, Point(a), Point(b),
                a == first || b == last ? AgentBodyRadius : RouteClearance), out _, 384);
        if (path == null)
            return false;
        output.Clear();
        foreach (var cell in path.Skip(1))
            output.Enqueue(Point(cell));
        agent.MoveProgressDestination = null;
        agent.MoveProgressAt = now;
        agent.LocalDetours++;
        agent.TrafficDecision = "local-obstacle-detour";
        _steering.Unregister(uid);
        return true;

        EntityCoordinates Point(int cell) => cell == first ? start : cell == last ? destination :
            new EntityCoordinates(start.EntityId, origin + new Vector2(cell % size + 0.5f, cell / size + 0.5f) * step);
        bool Walkable(int cell)
        {
            var point = Point(cell);
            // A physical stall forbids reusing the same immediate approach for this
            // bounded search. This is a route preference, never collision immunity.
            return (cell == last || !stalled || Vector2.DistanceSquared(point.Position, avoid) > 0.3f * 0.3f) &&
                ValidOrderPoint(uid, point) && (agent.OrderedDestination != null ||
                    agent.Home is { } home && _transform.InRange(home, point, agent.LeashRange));
        }
    }

    private bool WaitForSquad(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.OrderRally is not { } rally || now < agent.NextCohesionWait ||
            agent.RushTarget != null || now - agent.LastHit < TimeSpan.FromSeconds(2))
            return false;
        var start = Transform(uid).Coordinates;
        var lagging = false;
        var ownDistance = Vector2.Distance(_transform.ToMapCoordinates(start).Position, _transform.ToMapCoordinates(rally).Position);
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !SameSquad(uid, agent, other, buddy) || !_mobs.IsAlive(other) ||
                HasComp<ActorComponent>(other) || buddy.OrderedDestination == null ||
                buddy.OrderRally is not { } otherRally || !_transform.InRange(rally, otherRally, 1) ||
                buddy.OrderBlockedSince is { } blocked && now - blocked > TimeSpan.FromSeconds(12) ||
                _transform.InRange(start, Transform(other).Coordinates, 8))
                continue;
            var otherDistance = Vector2.Distance(_transform.GetWorldPosition(other), _transform.ToMapCoordinates(rally).Position);
            if (otherDistance > ownDistance + 5)
                lagging = true;
        }
        if (!lagging)
        {
            agent.CohesionWaitSince = null;
            return false;
        }
        agent.CohesionWaitSince ??= now;
        if (now - agent.CohesionWaitSince >= TimeSpan.FromSeconds(4))
        {
            // An unreachable member cannot deadlock the entire squad. The member keeps
            // its own bounded reroute attempts and exposes blocked status to the admin.
            agent.CohesionWaitSince = null;
            agent.NextCohesionWait = now + TimeSpan.FromSeconds(2);
            return false;
        }
        agent.MoveProgressAt = now;
        agent.TrafficDecision = "waiting-for-squad";
        _steering.Unregister(uid);
        return true;
    }
}
