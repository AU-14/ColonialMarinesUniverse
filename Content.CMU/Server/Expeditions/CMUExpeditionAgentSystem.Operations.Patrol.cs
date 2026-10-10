using System.Linq;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private int _automaticRouteBudget;
    private int _automaticPlanCursor;

    private void ResetSquadOperations(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        // Replace rather than clear: an automatic continuation can retain only its
        // coverage memory across the ordinary order/activity cancellation path.
        agent.PatrolMemory = new();
        agent.PatrolMemoryAnchor = null;
        agent.AutoPatrolDestination = null;
        agent.AutoPatrolSector = -1;
        agent.AutoPatrolVisited = false;
        agent.AutoPatrolCohesionSince = null;
        agent.PatrolDecision = "disabled";
        agent.SupportTarget = null;
        agent.SupportContact = null;
        agent.SupportOrigin = null;
        agent.SupportFlankSide = 0;
        agent.SupportBestDistance = float.MaxValue;
        agent.SupportProgressAt = TimeSpan.Zero;
        agent.AssistanceDecision = "none";
    }

    private bool AvailableForPatrol(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now) =>
        CanOrderSquadMember(uid) && agent.AutoPatrol && !agent.HoldPosition && !agent.Entrench &&
        agent.TravelGoal == null && (agent.OrderedDestination == null || agent.Patrolling) &&
        agent.Action == null && agent.Treatment == null && agent.PendingWeapon == null &&
        !CommittedMovement(agent) && agent.SpacingUntil <= now &&
        agent.SupportSquadRoot == null && agent.RecoveryUntil <= now && agent.LastDamage < agent.RetreatDamage &&
        (agent.LastSeen == null || now >= agent.ForgetAt) &&
        agent.State is not (CMUExpeditionAgentState.Healing or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.Withdraw);

    private EntityUid? AutomaticPatrolLeader(SquadPlan plan, TimeSpan now)
    {
        if (plan.Leader is { } leader && Comp<CMUExpeditionAgentComponent>(leader).AutoPatrol)
            return AvailableForPatrol(leader, Comp<CMUExpeditionAgentComponent>(leader), now) ? leader : null;
        return plan.Members.Where(member => AvailableForPatrol(member, Comp<CMUExpeditionAgentComponent>(member), now))
            .Select(member => (EntityUid?) member).FirstOrDefault();
    }

    private void UpdateSquadOperations(TimeSpan now)
    {
        _automaticRouteBudget = 2;
        foreach (var (source, claim) in _assistanceClaims.ToArray())
            if (claim.Until <= now || !Exists(source) || !Exists(claim.Responder) ||
                !_squadPlans.TryGetValue(claim.Responder, out var responder) ||
                !responder.Members.Any(member => CanOrderSquadMember(member) &&
                    Comp<CMUExpeditionAgentComponent>(member) is { } agent &&
                    agent.SupportSquadRoot == source && agent.SupportUntil > now))
                _assistanceClaims.Remove(source);
        var plans = _squadPlans.Values.ToArray();
        for (var index = 0; index < plans.Length; index++)
        {
            var plan = plans[(_automaticPlanCursor + index) % plans.Length];
            foreach (var uid in plan.Members)
                UpdateSquadAssistance(uid, Comp<CMUExpeditionAgentComponent>(uid), now);
            if (plan.Contact != null || AutomaticPatrolLeader(plan, now) is not { } leader)
                continue;
            var lead = Comp<CMUExpeditionAgentComponent>(leader);
            if (now < lead.NextAutoPatrol || !AutomaticPatrolLegComplete(plan, leader, lead, now))
                continue;
            StartAutomaticPatrol(plan, retry: true);
        }
        if (plans.Length > 0)
            _automaticPlanCursor = (_automaticPlanCursor + 1) % plans.Length;
    }

    private bool AutomaticPatrolLegComplete(SquadPlan plan, EntityUid leader, CMUExpeditionAgentComponent lead, TimeSpan now)
    {
        if (lead.AutoPatrolDestination is not { } destination)
            return true;
        if (!lead.AutoPatrolVisited && lead.OrderedDestination == null &&
            _transform.InRange(Transform(leader).Coordinates, destination, 1.5f))
        {
            if (lead.AutoPatrolSector >= 0)
                lead.PatrolMemory[lead.AutoPatrolSector] = CMUPatrolPolicy.Visited(
                    lead.PatrolMemory.GetValueOrDefault(lead.AutoPatrolSector), now.TotalSeconds);
            lead.AutoPatrolVisited = true;
            lead.PatrolVisits++;
        }
        if (lead.OrderBlockedSince is { } blocked && now - blocked >= TimeSpan.FromSeconds(5) ||
            !lead.AutoPatrolVisited && now - lead.AutoPatrolLegStarted >= TimeSpan.FromSeconds(45))
        {
            RecordPatrolFailure(lead, lead.AutoPatrolSector, now);
            lead.AutoPatrolDestination = null;
            return true;
        }
        if (lead.OrderedDestination != null)
            return false;
        var position = Transform(leader).Coordinates;
        var straggler = plan.Members.Any(member => member != leader && CanOrderSquadMember(member) &&
            Comp<CMUExpeditionAgentComponent>(member) is { AutoPatrol: true } buddy &&
            AvailableForPatrol(member, buddy, now) && buddy.OrderedDestination != null && buddy.Patrolling &&
            buddy.AutoPatrolLegStarted == lead.AutoPatrolLegStarted &&
            !(buddy.OrderBlockedSince is { } stalled && now - stalled >= TimeSpan.FromSeconds(6)) &&
            Transform(member).MapID == Transform(leader).MapID &&
            !_transform.InRange(position, Transform(member).Coordinates, 8));
        if (straggler)
        {
            // Give healthy followers time to finish before expanding coverage. Stragglers
            // keep their recovery route when the bounded cohesion wait expires.
            lead.AutoPatrolCohesionSince ??= now;
            lead.PatrolDecision = "waiting-for-patrol-members";
            if (now - lead.AutoPatrolCohesionSince.Value < TimeSpan.FromSeconds(6))
                return false;
            lead.PatrolDecision = "patrol-stragglers-retain-recovery";
            return true;
        }
        lead.AutoPatrolCohesionSince = null;
        return true;
    }

    private static void RecordPatrolFailure(CMUExpeditionAgentComponent agent, int sector, TimeSpan now)
    {
        if (sector < 0)
            return;
        agent.PatrolMemory[sector] = CMUPatrolPolicy.Failed(agent.PatrolMemory.GetValueOrDefault(sector), now.TotalSeconds);
        agent.PatrolFailures++;
        agent.PatrolDecision = "avoiding-failed-patrol-sector";
    }

    private int StartAutomaticPatrol(SquadPlan plan, bool retry = false)
    {
        var now = _timing.CurTime;
        if (AutomaticPatrolLeader(plan, now) is not { } leader)
            return 0;
        var lead = Comp<CMUExpeditionAgentComponent>(leader);
        if (!retry)
            _automaticRouteBudget = 2;
        if (_automaticRouteBudget <= 0)
            return 0;
        lead.NextAutoPatrol = now + TimeSpan.FromSeconds(3);
        var center = lead.AutoPatrolAnchor ?? Transform(leader).Coordinates;
        if (!Exists(center.EntityId) || !TrySquadCoordinates(center, out center) ||
            Transform(center.EntityId).MapID != Transform(leader).MapID)
        {
            lead.PatrolDecision = "patrol-anchor-unavailable";
            return 0;
        }
        if (!retry || lead.PatrolMemoryAnchor is not { } previous || !Exists(previous.EntityId) ||
            !_transform.InRange(previous, center, .5f))
        {
            lead.PatrolMemory = new();
            lead.PatrolMemoryAnchor = center;
        }
        var start = Transform(leader).Coordinates;
        var candidates = new Dictionary<int, EntityCoordinates>();
        var options = new List<CMUPatrolOption>();
        for (var sector = 0; sector < CMUPatrolPolicy.SectorCount; sector++)
        {
            var memory = lead.PatrolMemory.GetValueOrDefault(sector);
            if (memory.RetryAt > now.TotalSeconds)
                continue;
            var point = center.Offset(CMUPatrolPolicy.Offset(sector, lead.Squad));
            if (!_transform.InRange(center, point, lead.LeashRange) || !start.TryDistance(EntityManager, point, out var distance) ||
                distance is < 3 or > 16)
                continue;
            candidates[sector] = point;
            options.Add(new CMUPatrolOption(sector, distance, true, memory));
        }
        while (_automaticRouteBudget > 0)
        {
            var sector = CMUPatrolPolicy.Select(options, now.TotalSeconds);
            if (sector < 0)
                break;
            options.RemoveAll(option => option.Sector == sector);
            _automaticRouteBudget--;
            // Only shortlisted sectors perform world/fixture queries. Nearby ground lets
            // an irregular corridor represent a sector whose geometric centre is a wall.
            EntityCoordinates? selected = null;
            foreach (var candidate in NearbySquadPositions(candidates[sector], 1))
            {
                if (!TrySquadCoordinates(candidate, out var usable) || !ValidOrderPoint(leader, usable) ||
                    Reserved(leader, usable) || KnownDangerCost(lead, usable) > 5 ||
                    !_transform.InRange(center, usable, lead.LeashRange) ||
                    !start.TryDistance(EntityManager, usable, out var distance) || distance is < 3 or > 16)
                    continue;
                selected = usable;
                break;
            }
            if (selected is not { } point)
            {
                RecordPatrolFailure(lead, sector, now);
                continue;
            }
            lead.PatrolSearches++;
            // Normal local search has a 256-cell ceiling. It can follow corners and usable
            // doors; automatic patrol does not turn a failed search into a vault shortcut.
            if (!BuildTacticalRoute(leader, lead, point, allowVaults: false))
            {
                RecordPatrolFailure(lead, sector, now);
                continue;
            }
            var route = lead.Route.ToArray();
            var memory = lead.PatrolMemory;
            var reserved = new List<EntityCoordinates>();
            var count = 0;
            foreach (var member in plan.Members.OrderBy(member => member == leader ? 0 : 1))
            {
                var agent = Comp<CMUExpeditionAgentComponent>(member);
                if (!AvailableForPatrol(member, agent, now) || Transform(member).MapID != Transform(leader).MapID ||
                    member != leader && (agent.OrderBlockedSince is { } stalled && now - stalled >= TimeSpan.FromSeconds(6) ||
                        agent.OrderedDestination != null && !_transform.InRange(start, Transform(member).Coordinates, 8)))
                    continue;
                var stop = NearbySquadPositions(point, member == leader ? 0 : 3).FirstOrDefault(candidate =>
                    ValidOrderPoint(member, candidate) && !Reserved(member, candidate) &&
                    _transform.InRange(center, candidate, agent.LeashRange) &&
                    RoutePassage(member, point, candidate, allowVault: false) &&
                    KnownDangerPassage(member, agent, point, candidate) &&
                    !reserved.Any(other => _transform.InRange(other, candidate, 1.3f)));
                if (stop == default || !OrderPosition(member, stop, false))
                {
                    if (member == leader)
                        break;
                    continue;
                }
                agent.AutoPatrol = true;
                agent.AutoPatrolAnchor = center;
                agent.Patrolling = true;
                agent.PatrolPoints.Clear();
                agent.PatrolPoints.Add(stop);
                agent.OrderRally = point;
                agent.AutoPatrolDestination = stop;
                agent.AutoPatrolSector = sector;
                agent.AutoPatrolLegStarted = now;
                agent.NextAutoPatrol = now + TimeSpan.FromSeconds(3);
                agent.PatrolDecision = "covering-new-patrol-sector";
                agent.OperationsDecision = "auto-patrol";
                if (member == leader)
                {
                    agent.PatrolMemory = memory;
                    agent.PatrolMemoryAnchor = center;
                    foreach (var waypoint in route)
                        agent.OrderRoute.Enqueue(waypoint);
                }
                reserved.Add(stop);
                count++;
            }
            if (count > 0)
                return count;
            lead.Route.Clear();
            lead.RouteDestination = null;
            RecordPatrolFailure(lead, sector, now);
        }
        lead.PatrolDecision = "waiting-for-reachable-patrol-sector";
        return 0;
    }
}
