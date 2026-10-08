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

    private bool AcceptOrderedContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target) =>
        !IsFriendly(uid, target) && (agent.TargetFactions.Count == 0 ||
            TryComp<NpcFactionMemberComponent>(target, out var member) && member.Factions.Any(f => agent.TargetFactions.Contains(f.Id)));

    public bool OrderPosition(EntityUid uid, EntityCoordinates destination, bool entrench)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) ||
            !TrySquadCoordinates(destination, out destination) ||
            Transform(uid).MapUid != Transform(destination.EntityId).MapUid || !ValidOrderPoint(uid, destination))
            return false;
        ResetOrders(uid, agent);
        agent.Patrolling = false;
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
        agent.OrderRoute.Clear();
        agent.NextOrderRoute = TimeSpan.Zero;
        agent.OrderBlocked = false;
        ClearCover(agent);
        _steering.Unregister(uid);
        agent.State = CMUExpeditionAgentState.Guard;
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
