using System.Linq;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool BorrowSquadRoute(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination)
    {
        if (PlanFor(agent) is not { } plan || agent.OrderRally is not { } rally)
            return false;
        foreach (var other in plan.Members)
        {
            var buddy = Comp<CMUExpeditionAgentComponent>(other);
            if (other == uid || !CanOrderSquadMember(other) || buddy.OrderRally is not { } otherRally || !_transform.InRange(rally, otherRally, 1) ||
                buddy.OrderRoute.Count < 2 || buddy.OrderBlocked)
                continue;
            var route = buddy.OrderRoute.Take(96).ToArray();
            var join = Array.FindIndex(route, point => _transform.InRange(Transform(uid).Coordinates, point, 4) &&
                RoutePassage(uid, Transform(uid).Coordinates, point));
            if (join < 0 || !RoutePassage(uid, route[^1], destination))
                continue;
            var previous = Transform(uid).Coordinates;
            var valid = true;
            for (var index = join; index < route.Length; index++)
            {
                if (!RoutePassage(uid, previous, route[index]))
                {
                    valid = false;
                    break;
                }
                previous = route[index];
            }
            if (!valid)
                continue;
            foreach (var point in route.Skip(join))
                agent.OrderRoute.Enqueue(point);
            agent.OrderRoute.Enqueue(destination);
            agent.MoveProgressAt = _timing.CurTime;
            agent.OrderBlocked = false;
            Decision(agent, "squad-route", "reused-and-revalidated-corridor");
            return true;
        }
        return false;
    }

    private bool RecoverStraggler(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now, out bool searched)
    {
        searched = false;
        if (now < agent.NextRegroupRoute || agent.OrderBlockedSince is not { } blocked || now - blocked < TimeSpan.FromSeconds(6) ||
            PlanFor(agent) is not { } plan || plan.Leader is not { } leader || leader == uid ||
            agent.Target != null || agent.TravelGoal != null || agent.OrderedDestination is not { } destination)
            return false;
        var leaderPoint = Transform(leader).Coordinates;
        if (!_transform.InRange(Transform(uid).Coordinates, leaderPoint, 24))
            return false;
        foreach (var candidate in NearbySquadPositions(leaderPoint, 2)
                     .Where(point => ValidOrderPoint(uid, point) && !Reserved(uid, point)).Take(1))
        {
            searched = true;
            agent.NextRegroupRoute = now + TimeSpan.FromSeconds(12);
            if (!BuildTacticalRoute(uid, agent, candidate, ordered: true))
                continue;
            agent.OrderRoute.Clear();
            foreach (var point in agent.Route)
                agent.OrderRoute.Enqueue(point);
            agent.Route.Clear();
            agent.RouteDestination = null;
            agent.OrderBlockedSince = null;
            agent.OrderBlocked = false;
            agent.NextOrderRoute = now + TimeSpan.FromSeconds(1);
            // The order's final destination stays intact; rebuild its final leg after regrouping.
            Decision(agent, "regroup-route", "straggler-joining-leader");
            return true;
        }
        return false;
    }
}
