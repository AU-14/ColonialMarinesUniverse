using Content.Shared.NPC.Components;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;
using System.Linq;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool IsFriendly(EntityUid uid, EntityUid other)
    {
        if (uid == other)
            return true;
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var agent) && TryComp<NpcFactionMemberComponent>(other, out var factions))
        {
            if (factions.Factions.Any(f => agent.FriendlyFactions.Contains(f.Id)))
                return true;
            if (factions.Factions.Any(f => agent.TargetFactions.Contains(f.Id)))
                return false;
        }
        return _factions.IsEntityFriendly(uid, other);
    }

    private IEnumerable<EntityUid> ExpeditionHostiles(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.TargetFactions.Count == 0)
            return _factions.GetNearbyHostiles(uid, agent.DetectionRange).Where(other => !IsFriendly(uid, other));
        var nearby = new HashSet<EntityUid>();
        var location = _transform.GetMapCoordinates(uid);
        _lookup.GetEntitiesInRange(location.MapId, location.Position, agent.DetectionRange, nearby);
        return nearby.Where(other => other != uid && TryComp<NpcFactionMemberComponent>(other, out var factions) &&
            factions.Factions.Any(f => agent.TargetFactions.Contains(f.Id)) && !IsFriendly(uid, other));
    }

    public bool OrderPosition(EntityUid uid, EntityCoordinates destination, bool entrench)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) ||
            !TryComp<CMUExpeditionMapComponent>(destination.EntityId, out var map) ||
            Transform(uid).MapUid != destination.EntityId || destination.X < 1 || destination.Y < 1 ||
            destination.X >= map.Plan.Size - 1 || destination.Y >= map.Plan.Size - 1 ||
            map.Plan.Terrain[map.Plan.Index((int) destination.X, (int) destination.Y)] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff ||
            !BodyFits(uid, destination))
            return false;
        CancelPlan(uid, agent, false);
        CancelTreatment(agent);
        CancelWork(uid, agent);
        agent.OrderedDestination = destination;
        agent.Entrench = entrench;
        agent.Target = null;
        agent.LastSeen = null;
        return true;
    }

    public void ResetOrders(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        CancelWork(uid, agent);
        CancelPlan(uid, agent, false);
        CancelTreatment(agent);
        agent.Target = null;
        agent.LastSeen = null;
        agent.RadioTarget = null;
        agent.RadioPosition = null;
    }

    private bool GrenadeDecisionAvailable(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (now < agent.SquadGrenadeReady && now >= agent.GrenadeWindowEnd)
            return false;
        var used = 0;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other != uid && !SameSquad(uid, agent, other, buddy))
                continue;
            if (buddy.GrenadeReservationUntil > now || buddy.LastGrenade > TimeSpan.Zero && now - buddy.LastGrenade < TimeSpan.FromSeconds(35))
                used++;
        }
        return used < 2;
    }

    private bool ReserveGrenadeDecision(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!GrenadeDecisionAvailable(uid, agent, now))
            return false;
        if (now >= agent.SquadGrenadeReady)
        {
            var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
            while (query.MoveNext(out var other, out var buddy))
            {
                if (other != uid && !SameSquad(uid, agent, other, buddy))
                    continue;
                buddy.GrenadeWindowEnd = now + TimeSpan.FromSeconds(2);
                buddy.SquadGrenadeReady = now + TimeSpan.FromSeconds(35);
            }
        }
        agent.GrenadeReservationUntil = now + TimeSpan.FromSeconds(3);
        return true;
    }
}
