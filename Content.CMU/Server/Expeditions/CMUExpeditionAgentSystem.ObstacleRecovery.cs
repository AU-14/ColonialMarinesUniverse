using System.Linq;
using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool _localRouteSearched;

    private bool LocalDetour(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination,
        Queue<EntityCoordinates> output, bool stalled = true, bool allowVaults = true)
    {
        var now = _timing.CurTime;
        if (_localRouteSearched || now < agent.NextLocalDetour ||
            !TrySquadCoordinates(Transform(uid).Coordinates, out var start) ||
            !TrySquadCoordinates(destination, out destination) || start.EntityId != destination.EntityId ||
            !_transform.InRange(start, destination, 8) || !BodyFits(uid, start) || !RoutePoint(uid, destination))
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
        var allowVault = false;
        var path = CMUTacticalRoute.Find(size, first, last, Walkable,
            cell => KnownDangerCost(agent, Point(cell)), Passage, out _, 384);
        if (path == null && allowVaults)
        {
            allowVault = true;
            path = CMUTacticalRoute.Find(size, first, last, Walkable,
                cell => KnownDangerCost(agent, Point(cell)), Passage, out _, 384);
        }
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
                RoutePoint(uid, point, allowVault) &&
                (!agent.AutoPatrol || agent.AutoPatrolAnchor is not { } anchor ||
                    _transform.InRange(anchor, point, agent.LeashRange)) &&
                (agent.TrafficBlockedPoint is not { } blocked || now >= agent.AvoidTrafficUntil ||
                    !_transform.InRange(point, blocked, 0.8f)) && (agent.OrderedDestination != null ||
                    agent.Home is { } home && _transform.InRange(home, point, agent.LeashRange));
        }
        bool Passage(int a, int b) => RoutePassage(uid, Point(a), Point(b),
            a == first || b == last ? AgentBodyRadius : RouteClearance, allowVault) &&
            KnownDangerPassage(uid, agent, Point(a), Point(b));
    }

    private static void ResetTravelCohesion(CMUExpeditionAgentComponent agent)
    {
        agent.CohesionWaitSince = null;
        agent.NextCohesionWait = TimeSpan.Zero;
        agent.CohesionDestination = null;
        agent.CohesionAdvanceOrigin = null;
        agent.CohesionWaitExhausted = false;
    }

    private bool WaitForSquad(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var start = Transform(uid).Coordinates;
        if (agent.CohesionDestination != agent.OrderedDestination)
        {
            ResetTravelCohesion(agent);
            agent.CohesionDestination = agent.OrderedDestination;
            agent.CohesionAdvanceOrigin = start;
        }
        if (agent.OrderRally is not { } rally || agent.OrderedDestination == null || now < agent.NextCohesionWait ||
            agent.Target != null || agent.RushTarget != null || now < agent.IncomingFireUntil || now < agent.SuppressedUntil ||
            now - agent.LastHit < TimeSpan.FromSeconds(2) || agent.WaitingForDoor != null ||
            agent.TrafficYieldPoint != null || agent.TrafficYieldTo != null ||
            agent.CohesionAdvanceOrigin is not { } origin || _transform.InRange(start, origin, 4))
        {
            if (agent.CohesionWaitSince != null)
                agent.CohesionWaitExhausted = true;
            agent.CohesionWaitSince = null;
            return false;
        }
        var map = Transform(uid).MapID;
        if (!Exists(rally.EntityId) || Transform(rally.EntityId).MapID != map)
            return false;
        var rallyPosition = _transform.ToMapCoordinates(rally).Position;
        var separated = false;
        var lagging = false;
        var ownDistance = Vector2.Distance(_transform.ToMapCoordinates(start).Position, rallyPosition);
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !SameSquad(uid, agent, other, buddy) || !_mobs.IsAlive(other) ||
                HasComp<ActorComponent>(other) || Transform(other).MapID != map || buddy.OrderedDestination == null ||
                buddy.OrderRally is not { } otherRally || !_transform.InRange(rally, otherRally, 1) ||
                _transform.InRange(start, Transform(other).Coordinates, 8))
                continue;
            var otherDistance = Vector2.Distance(_transform.GetWorldPosition(other), rallyPosition);
            if (otherDistance <= ownDistance + 5)
                continue;
            separated = true;
            // A member who is fighting, treating or blocked cannot close the gap just
            // because the front stops. Only wait for an active approach to this order.
            if (!buddy.OrderBlocked && buddy.Action == null && buddy.Treatment == null && buddy.TreatmentMedicine == null &&
                buddy.PendingWeapon == null && buddy.Target == null && buddy.RushTarget == null &&
                buddy.WaitingForDoor == null && buddy.TrafficYieldTo == null && buddy.LastDamage < buddy.RetreatDamage &&
                TryComp<NPCSteeringComponent>(other, out var steering) && steering.Status == SteeringStatus.Moving)
                lagging = true;
        }
        if (!separated)
        {
            if (agent.CohesionWaitSince != null || agent.CohesionWaitExhausted)
            {
                agent.CohesionAdvanceOrigin = start;
                agent.NextCohesionWait = now + TimeSpan.FromSeconds(8);
            }
            agent.CohesionWaitSince = null;
            agent.CohesionWaitExhausted = false;
            return false;
        }
        if (!lagging || agent.CohesionWaitExhausted || NearSquadDoorway(uid))
        {
            if (agent.CohesionWaitSince != null)
                agent.CohesionWaitExhausted = true;
            agent.CohesionWaitSince = null;
            return false;
        }
        agent.CohesionWaitSince ??= now;
        if (now - agent.CohesionWaitSince >= TimeSpan.FromSeconds(2))
        {
            // One brief pause per separation episode. A still-distant member must not
            // impose a repeating stop/advance cycle on everyone ahead of the bottleneck.
            agent.CohesionWaitSince = null;
            agent.CohesionWaitExhausted = true;
            return false;
        }
        agent.MoveProgressAt = now;
        agent.TrafficDecision = "waiting-for-squad";
        _steering.Unregister(uid);
        return true;
    }
}
