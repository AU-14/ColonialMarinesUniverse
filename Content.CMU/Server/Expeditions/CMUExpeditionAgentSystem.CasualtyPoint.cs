using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool ApproachCasualty(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var start = Transform(uid).Coordinates;
        var patient = _transform.ToCoordinates(start.EntityId,
            _transform.ToMapCoordinates(Transform(agent.Casualty!.Value).Coordinates));
        if (_transform.InRange(start, patient, 1.2f))
            return StartPlanMove(uid, agent, start, now);

        // Reach the casualty from outside the dangerous impact point. The 1.2 m
        // offset plus steering's arrival tolerance remains within the 1.5 m grab range.
        // Every approach still passes the ordinary geometry and danger route checks.
        var away = Vector2.Normalize(start.Position - patient.Position);
        var side = new Vector2(-away.Y, away.X);
        foreach (var degrees in new[] { 0, 45, -45, 90, -90, 135, -135, 180 })
        {
            var angle = degrees * MathF.PI / 180;
            var point = patient.Offset((away * MathF.Cos(angle) + side * MathF.Sin(angle)) * 1.2f);
            if (ClearLane(uid, point, patient, 0.1f, movement: true) &&
                StartPlanMove(uid, agent, point, now))
                return true;
        }
        return false;
    }

    private EntityCoordinates? MedicalCollectionPoint(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var plan = PlanFor(agent);
        if (plan?.CasualtyPoint is { } point && agent.Home is { } home && _transform.InRange(home, point, agent.LeashRange) &&
            _transform.InRange(Transform(uid).Coordinates, point, 14) && ValidOrderPoint(uid, point) &&
            ShelteredFromKnownThreats(uid, agent, point) && CoverHistoryCost(agent, point) == 0)
        {
            // Keep room around the collection point for other patients and their medics.
            foreach (var candidate in NearbySquadPositions(point, 2))
                if (ValidOrderPoint(uid, candidate) && !Reserved(uid, candidate) && ShelteredFromKnownThreats(uid, agent, candidate))
                    return candidate;
        }
        var shelter = FindPosition(uid, agent, Transform(uid), true)?.Anchor;
        if (plan != null && shelter != null)
            plan.CasualtyPoint = shelter;
        return shelter;
    }
}
