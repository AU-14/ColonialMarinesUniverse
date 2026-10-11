using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    public bool OrderAssault(EntityUid uid, EntityCoordinates destination)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) || !CanOrderSquadMember(uid) ||
            !TrySquadCoordinates(destination, out destination) || !ValidOrderPoint(uid, destination) ||
            Transform(uid).MapID != Transform(destination.EntityId).MapID)
            return false;
        if (!OrderPosition(uid, destination, false))
            return false;
        agent.AssaultDestination = destination;
        agent.AssaultBoundsCompleted = 0;
        agent.AssaultDecision = "advancing-to-objective";
        agent.DutyUntil = TimeSpan.Zero;
        return true;
    }

    private static void ClearAssaultOrder(CMUExpeditionAgentComponent agent)
    {
        agent.AssaultDestination = null;
        agent.AssaultBoundDestination = null;
        agent.AssaultNextBound = TimeSpan.Zero;
        agent.NextAssaultRoute = TimeSpan.Zero;
        agent.AssaultDecision = "none";
    }

    private void CompleteAssaultOrder(CMUExpeditionAgentComponent agent)
    {
        ClearAssaultOrder(agent);
        agent.OrderedDestination = null;
        agent.OrderRally = null;
        agent.AssaultDecision = "objective-secured";
        Decision(agent, "assault-complete", "objective-secured");
    }

    private bool FollowAssaultOrder(EntityUid uid, CMUExpeditionAgentComponent agent, bool hasAmmo, float damage, TimeSpan now)
    {
        if (agent.AssaultDestination is not { } objective)
            return false;
        if (!Exists(objective.EntityId) || Transform(objective.EntityId).MapID != Transform(uid).MapID)
        {
            if (agent.AssaultBoundDestination is { } owned && agent.CoverDestination == owned)
            {
                ReleaseManeuver(uid, agent);
                ClearCover(agent);
                _steering.Unregister(uid);
                agent.State = CMUExpeditionAgentState.Guard;
            }
            agent.OrderedDestination = null;
            agent.OrderRoute.Clear();
            ClearAssaultOrder(agent);
            agent.AssaultDecision = "objective-unavailable";
            return false;
        }
        // Survival/utility controllers can replace a bound. Its strategic order survives,
        // but the old short leg must not become the resumed travel destination.
        if (agent.AssaultBoundDestination is { } interrupted &&
            (agent.CoverDestination != interrupted || agent.State != CMUExpeditionAgentState.Reposition))
        {
            agent.AssaultBoundDestination = null;
            agent.OrderedDestination = objective;
            agent.AssaultNextBound = now + TimeSpan.FromSeconds(1);
        }
        if (agent.SquadPhase is "anti-rush" or "withdraw" or "anti-armor")
        {
            if (agent.AssaultBoundDestination != null)
            {
                ReleaseManeuver(uid, agent);
                ClearCover(agent);
                _steering.Unregister(uid);
                agent.AssaultBoundDestination = null;
                agent.OrderedDestination = objective;
                agent.State = CMUExpeditionAgentState.Guard;
            }
            agent.AssaultDecision = "squad-response-priority";
            return false;
        }
        if (agent.HoldPosition || agent.Action != null || agent.Treatment != null || agent.TreatmentMedicine != null ||
            agent.PendingWeapon != null || agent.AimedWeapon != null || agent.FlareItem != null || agent.UtilityCleanupItem != null ||
            agent.WorkItem != null || agent.PreparingWork || agent.ScavengeTarget != null || agent.FireRescueTarget != null ||
            agent.RushTarget != null || agent.SpacingDestination != null || agent.VaultTarget != null ||
            agent.CornerHolding || agent.CornerDestination != null || agent.RecoveryUntil > now ||
            agent.State is CMUExpeditionAgentState.Healing or CMUExpeditionAgentState.Reloading or
                CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.Withdraw or CMUExpeditionAgentState.OutOfAmmo ||
            damage >= agent.RetreatDamage || !hasAmmo || GrenadeDanger(Transform(uid).Coordinates) ||
            ShouldTreat(agent, damage, now) && TreatmentSafe(uid, agent) && HasMedicine(uid))
        {
            agent.AssaultDecision = "survival-or-utility-priority";
            return false;
        }
        var start = Transform(uid).Coordinates;
        if (agent.AssaultBoundDestination is { } bound)
        {
            if (ContinueMove(uid, agent, Transform(uid), now))
                return true;
            if (!agent.LastMoveFailed && _transform.InRange(start, bound, 0.75f))
                agent.AssaultBoundsCompleted++;
            else
                agent.OrderRoute.Clear();
            agent.AssaultBoundDestination = null;
            agent.OrderedDestination = objective;
            agent.AssaultNextBound = now + TimeSpan.FromSeconds(0.8);
            agent.State = CMUExpeditionAgentState.Guard;
            agent.AssaultDecision = "covering-next-bound";
        }
        else if (CommittedMovement(agent) || HasCoverCommitment(uid, agent, now))
            return false;
        if (_transform.InRange(start, objective, 0.5f))
        {
            agent.Home = objective;
            agent.OrderRoute.Clear();
            ClearTraffic(agent);
            _steering.Unregister(uid);
            CompleteAssaultOrder(agent);
            return false;
        }
        var contact = agent.Target is { } enemy && CombatTargetAlive(enemy) && Visible(uid, enemy, WeaponFireRange(uid, agent));
        if (!contact)
        {
            // A remembered enemy or an uncertain sound must not replace the ordered goal.
            // Known danger still rejects lethal corners along the ordinary order route.
            agent.OrderedDestination = objective;
            agent.Home = start;
            ReleaseManeuver(uid, agent);
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            agent.AssaultDecision = "advancing-to-objective";
            return FollowOrders(uid, agent, now);
        }
        agent.AssaultDecision = "supporting-assault";
        if (now < agent.AssaultNextBound || agent.Duty != CMUSquadDuty.Advance && HasLocalAssaultPartner(uid, agent))
            return SupportAssaultAdvance(uid, agent, damage, now);
        agent.AssaultNextBound = now + TimeSpan.FromSeconds(0.6);
        agent.OrderedDestination = objective;
        agent.Home = start;
        AdvanceRoute(uid, agent.OrderRoute, start);
        if (agent.OrderRoute.Count == 0)
        {
            if (now < agent.NextAssaultRoute)
                return SupportAssaultAdvance(uid, agent, damage, now);
            if (!BorrowSquadRoute(uid, agent, objective))
            {
                if (_orderRouteSearched)
                    return SupportAssaultAdvance(uid, agent, damage, now);
                _orderRouteSearched = true;
                agent.NextAssaultRoute = now + TimeSpan.FromSeconds(2);
                if (!BuildTacticalRoute(uid, agent, objective, ordered: true, allowVaults: false))
                {
                    agent.AssaultDecision = "objective-route-blocked";
                    return SupportAssaultAdvance(uid, agent, damage, now);
                }
                foreach (var point in agent.Route)
                    agent.OrderRoute.Enqueue(point);
                agent.Route.Clear();
                agent.RouteDestination = null;
            }
        }
        if (!agent.OrderRoute.TryPeek(out var next) || !RoutePassage(uid, start, next, allowVault: false) ||
            !KnownDangerPassage(uid, agent, start, next))
        {
            agent.OrderRoute.Clear();
            agent.AssaultDecision = "waiting-for-safe-approach";
            return SupportAssaultAdvance(uid, agent, damage, now);
        }
        var direction = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(next)).Position - start.Position;
        var distance = direction.Length();
        if (distance < 0.1f)
            return SupportAssaultAdvance(uid, agent, damage, now);
        EntityCoordinates? chosen = null;
        var waitingForCover = false;
        foreach (var length in new[] { Math.Min(1.9f, distance), Math.Min(0.9f, distance) })
        {
            var candidate = start.Offset(direction / distance * length);
            var middle = start.Offset((candidate.Position - start.Position) * 0.5f);
            if (!TraversablePassage(uid, start, candidate, allowVault: false) || !TrafficPassageClear(uid, start, candidate) ||
                !KnownDangerPassage(uid, agent, start, candidate) || Reserved(uid, candidate) ||
                CrossesCoveringFire(uid, agent, start, candidate) ||
                MeleeClearance(agent, candidate) < Math.Min(agent.MeleeStandoffRange, MeleeClearance(agent, start)))
                continue;
            if (!TryReserveManeuver(uid, agent, now, candidate))
            {
                waitingForCover = true;
                continue;
            }
            if (!AssaultExposureSafe(uid, agent, start, middle, candidate))
            {
                ReleaseManeuver(uid, agent);
                continue;
            }
            chosen = candidate;
            break;
        }
        if (chosen is not { } step)
        {
            if (TryOpenContactDoor(uid, agent, next, now))
            {
                agent.AssaultDecision = "opening-objective-route";
                return true;
            }
            agent.AssaultDecision = waitingForCover ? "waiting-for-assault-cover" : "waiting-for-safe-bound";
            return SupportAssaultAdvance(uid, agent, damage, now);
        }
        ClearCover(agent);
        if (!BeginMove(uid, agent, step, CMUExpeditionAgentState.Reposition, now))
        {
            ReleaseManeuver(uid, agent);
            return SupportAssaultAdvance(uid, agent, damage, now);
        }
        agent.OrderedDestination = step;
        agent.AssaultBoundDestination = step;
        agent.AssaultDecision = "advancing-under-fire";
        Decision(agent, "assault-bound", "advancing-under-fire");
        return true;
    }

    private bool AssaultExposureSafe(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start,
        EntityCoordinates middle, EntityCoordinates destination)
    {
        var current = ExposureScore(uid, agent, start);
        if (ExposureScore(uid, agent, middle) <= current + 0.5f && ExposureScore(uid, agent, destination) <= current + 0.5f)
            return true;
        if (agent.CoveringShooter is not { } shooter || !TryComp<CMUExpeditionAgentComponent>(shooter, out var covering) ||
            KnownDangerCost(agent, middle) > KnownDangerCost(agent, start) ||
            KnownDangerCost(agent, destination) > KnownDangerCost(agent, start))
            return false;
        // Actual covering fire may permit leaving cover towards that same opponent.
        // It cannot authorize exposure to another sector or remembered incoming fire.
        var supportedSector = false;
        foreach (var threat in agent.ThreatSectors)
        {
            if (threat is not { } contact || !Sheltered(uid, start, contact.Position) ||
                Sheltered(uid, middle, contact.Position) && Sheltered(uid, destination, contact.Position))
                continue;
            if (contact.Target != agent.Target || contact.Target != covering.Target)
                return false;
            supportedSector = true;
        }
        return supportedSector;
    }

    private bool SupportAssaultAdvance(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        // Supporting the bound still permits grenades, rescue and defensive utility.
        // Ordinary contact pursuit cannot take ownership of the strategic objective.
        agent.ContactDestination = null;
        if (RunPlan(uid, agent, true, damage, false, now))
            return true;
        _steering.Unregister(uid);
        if (agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage) &&
            (agent.State != CMUExpeditionAgentState.Recover || now >= agent.FireAt))
            Aim(agent, now);
        return true;
    }

    private bool HasLocalAssaultPartner(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
            if (LocalSquadMember(uid, agent, other, buddy) && buddy.AssaultDestination != null)
                return true;
        return false;
    }
}
