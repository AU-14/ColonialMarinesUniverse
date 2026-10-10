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
        if (VehicleBody(other) && TryComp<CMUExpeditionAgentComponent>(uid, out var driverObserver))
            return VehicleDisposition(uid, driverObserver, other) < 0;
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
            return _factions.GetNearbyHostiles(uid, agent.DetectionRange).Where(other => !IsFriendly(uid, other)).Union(HostileVehicles(uid, agent));
        var nearby = new HashSet<EntityUid>();
        var location = _transform.GetMapCoordinates(uid);
        _lookup.GetEntitiesInRange(location.MapId, location.Position, agent.DetectionRange, nearby);
        return nearby.Where(other => other != uid && TryComp<NpcFactionMemberComponent>(other, out var factions) &&
            factions.Factions.Any(f => agent.TargetFactions.Contains(f.Id)) && !IsFriendly(uid, other)).Union(HostileVehicles(uid, agent));
    }

    private bool AcceptOrderedContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target)
    {
        if (!Exists(target) || IsFriendly(uid, target))
            return false;
        if (VehicleBody(target))
            return ArmedVehicle(target) && VehicleDisposition(uid, agent, target) > 0;
        if (agent.TargetFactions.Count > 0)
            return TryComp<NpcFactionMemberComponent>(target, out var member) &&
                member.Factions.Any(f => agent.TargetFactions.Contains(f.Id));
        // Receiving another squad's report does not make its neutral contacts hostile.
        // Match native acquisition, including per-entity retaliation and ignore rules.
        return !_factions.IsIgnored(uid, target) &&
            (TryComp<NpcFactionMemberComponent>(uid, out var observer) &&
                TryComp<NpcFactionMemberComponent>(target, out var contact) && observer.HostileFactions.Overlaps(contact.Factions) ||
             _factions.GetHostiles(uid).Contains(target));
    }

    public bool OrderPosition(EntityUid uid, EntityCoordinates destination, bool entrench, Direction? facing = null)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) ||
            !TrySquadCoordinates(destination, out destination) ||
            !ValidOrderPoint(uid, destination))
            return false;
        ResetOrders(uid, agent);
        agent.Patrolling = false;
        agent.HoldPosition = entrench;
        agent.OrderedDestination = destination;
        agent.OrderRally = destination;
        agent.Entrench = entrench && !HasComp<CMUExpeditionMedicComponent>(uid);
        agent.GuardAnchor = entrench ? destination : null;
        agent.GuardFacing = facing == null ? null :
            (facing.Value.ToAngle() - _transform.GetWorldRotation(destination.EntityId)).GetCardinalDir();
        agent.NextWork = _timing.CurTime + TimeSpan.FromSeconds(3);
        agent.FortificationDecision = agent.Entrench ? "awaiting-guard-position" : "not-ordered";
        agent.Target = null;
        agent.LastSeen = null;
        agent.PendingWeapon = null;
        return true;
    }

    public void ResetOrders(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        ClearAssaultOrder(agent);
        ResetSquadOperations(uid, agent);
        agent.AutoPatrol = false;
        agent.AutoPatrolAnchor = null;
        agent.HoldPosition = false;
        agent.SupportSquadRoot = null;
        agent.SupportUntil = TimeSpan.Zero;
        agent.OperationsDecision = "local-orders";
        CancelAgentActivity(uid, agent, "explicit-command");
        agent.TravelGoal = null;
        agent.TravelPortal = null;
        agent.PortalUntil = TimeSpan.Zero;
        agent.FailedPortals.Clear();
        agent.HeardPoint = null;
        agent.SupplySource = null;
        agent.DeliveryRecipient = null;
        agent.DeliveryPoint = null;
        agent.FailedDeliveries.Clear();
        agent.NextRegroupRoute = TimeSpan.Zero;
        agent.DutyUntil = TimeSpan.Zero;
        Decision(agent, "orders-reset", "explicit-command");
        agent.FlashPosition = null;
        agent.Entrench = false;
        agent.GuardFacing = null;
        agent.GuardAnchor = null;
        agent.FortificationPoint = null;
        agent.FortificationDecision = "not-ordered";
        agent.Target = null;
        agent.LastSeen = null;
        agent.RadioTarget = null;
        agent.RadioPosition = null;
        agent.ContactFromRadio = false;
        agent.RadioDecision = "orders-reset";
        agent.NextInvestigation = TimeSpan.Zero;
        agent.NextTargetSwitch = TimeSpan.Zero;
        agent.OrderRoute.Clear();
        agent.NextOrderRoute = TimeSpan.Zero;
        agent.OrderBlocked = false;
        agent.OrderBlockedSince = null;
        agent.LastOrderProgressPosition = null;
        agent.OrderRally = null;
        ResetTravelCohesion(agent);
        ClearThreatAssessment(agent);
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
