using System.Numerics;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool WaitForSquadOrdnance(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.RushTarget != null || GrenadeDanger(Transform(uid).Coordinates) || now - agent.LastHit < TimeSpan.FromSeconds(.5) ||
            agent.Action != null || agent.Treatment != null || !OptionalDecisionReady(agent) || PlanFor(agent) is not { } plan)
            return false;
        foreach (var member in plan.Members)
        {
            var buddy = Comp<CMUExpeditionAgentComponent>(member);
            if (member == uid || buddy.GrenadeReservationUntil <= now || buddy.GrenadeTarget is not { } landing)
                continue;
            var next = agent.CoverDestination ?? agent.SpacingDestination ?? agent.ContactDestination;
            if (next == null && agent.OrderRoute.TryPeek(out var waypoint))
                next = waypoint;
            if (next is not { } destination || Transform(destination.EntityId).MapID != Transform(landing.EntityId).MapID ||
                SegmentDistance(_transform.ToMapCoordinates(landing).Position, _transform.GetWorldPosition(uid),
                    _transform.ToMapCoordinates(destination).Position) > (buddy.SmokeGrenade ? 3 : 6))
                continue;
            // Keep firing from the current safe stance while the thrower completes the action.
            _steering.Unregister(uid);
            agent.MoveProgressAt = now;
            Decision(agent, "waiting-for-ordnance", buddy.SmokeGrenade ? "do-not-enter-screen" : "reserved-blast-area");
            return true;
        }
        return false;
    }

    private bool YieldSpecialistLane(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (PlanFor(agent) is not { } plan || agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null ||
            agent.RushTarget != null || CommittedMovement(agent) || !OptionalDecisionReady(agent) ||
            HasCoverCommitment(uid, agent, now) || now < agent.NextDutyMove)
            return false;
        var start = Transform(uid).Coordinates;
        var position = _transform.GetWorldPosition(uid);
        foreach (var member in plan.Members)
        {
            var rocket = Comp<CMUExpeditionAgentComponent>(member);
            if (member == uid || !rocket.AntiVehicle || now < rocket.NextRocket || rocket.Target is not { } target ||
                !Visible(member, target, rocket.FireRange) || !HasReadyRocket(member) ||
                !RocketOpportunity(member, rocket, target, Transform(target).Coordinates) ||
                !_transform.InRange(start, Transform(member).Coordinates, 9))
                continue;
            var muzzle = _transform.GetWorldPosition(member);
            var impact = _transform.GetWorldPosition(target);
            var delta = impact - muzzle;
            if (delta.LengthSquared() < 1)
                continue;
            var direction = Vector2.Normalize(delta);
            var back = muzzle - direction * 2;
            if (SegmentDistance(position, back, impact) > 1.2f)
                continue;
            agent.NextDutyMove = now + TimeSpan.FromSeconds(3);
            var side = new Vector2(-direction.Y, direction.X);
            foreach (var offset in new[] { 1.5f, -1.5f, 2.25f, -2.25f })
            {
                var point = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(start).Offset(side * offset));
                if (!ValidOrderPoint(uid, point) || Reserved(uid, point) || !TraversablePassage(uid, start, point) ||
                    ExposureScore(uid, agent, point) > ExposureScore(uid, agent, start) ||
                    !TryReserveManeuver(uid, agent, now))
                    continue;
                Decision(agent, "clear-launch-lane", "supporting-rocketeer", 1);
                BeginMove(uid, agent, point, CMUExpeditionAgentState.Reposition, now);
                return true;
            }
        }
        return false;
    }
}
