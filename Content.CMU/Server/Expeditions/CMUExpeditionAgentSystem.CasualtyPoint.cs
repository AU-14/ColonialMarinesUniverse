using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
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
