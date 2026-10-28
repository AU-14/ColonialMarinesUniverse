using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static void ResetContactSearch(CMUExpeditionAgentComponent agent)
    {
        if (agent.ContactSearchPoint != null)
        {
            // A survival or utility controller may have taken over the route while this
            // search was suspended. Release only the investigation's own route.
            if (agent.InvestigationDestination != null && agent.RouteDestination == agent.InvestigationDestination)
            {
                agent.Route.Clear();
                agent.RouteDestination = null;
            }
            agent.InvestigationDestination = null;
            agent.InvestigationContact = null;
            agent.NextInvestigation = TimeSpan.Zero;
        }
        agent.ContactSearchPoint = null;
        agent.ContactInspectUntil = null;
        agent.ContactSearchUntil = TimeSpan.Zero;
    }

    private bool SearchContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point,
        TimeSpan observed, TimeSpan now, bool sound = false)
    {
        if (!Exists(point.EntityId) || agent.Home is not { } home || !_transform.InRange(home, point, agent.LeashRange))
        {
            ResetContactSearch(agent);
            return false;
        }
        if (agent.ContactSearchPoint != point || agent.SearchingSound != sound || observed > agent.ContactSearchObservedAt)
        {
            ResetContactSearch(agent);
            agent.ContactSearchPoint = point;
            agent.ContactSearchObservedAt = observed;
            // Renew only from fresh sensory evidence, never from movement or another think.
            agent.ContactSearchUntil = observed + agent.ContactSearchDuration;
            agent.SearchingSound = sound;
            if (!sound && agent.ForgetAt < agent.ContactSearchUntil)
                agent.ForgetAt = agent.ContactSearchUntil;
        }
        if (now >= agent.ContactSearchUntil)
        {
            ResetContactSearch(agent);
            _steering.Unregister(uid);
            Decision(agent, "search", "evidence-expired");
            return false;
        }
        // Emergency actions run before this controller. Suppression can pause an
        // investigation but cannot renew its deadline or override a hold order.
        if (agent.HoldPosition || agent.LastDamage >= agent.RetreatDamage ||
            agent.RecoveryUntil > now || agent.Duty is CMUSquadDuty.Recover or CMUSquadDuty.RearGuard ||
            agent.SquadPhase == "withdraw" || now < agent.SuppressedUntil && agent.Stress >= 0.65f)
        {
            agent.ContactInspectUntil = null;
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Watch;
            Decision(agent, "search", "holding-under-orders-or-pressure");
            return true;
        }
        var inspectRange = !sound && agent.LastContactWasMelee ? agent.MeleeStandoffRange : 2.5f;
        if (_transform.InRange(Transform(uid).Coordinates, point, inspectRange) && SightLine(uid, point, inspectRange))
        {
            agent.ContactInspectUntil ??= now + agent.ContactInspectDuration;
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Watch;
            Decision(agent, "search", "inspecting-last-known-position");
            if (now < agent.ContactInspectUntil.Value)
                return true;
            ResetContactSearch(agent);
            Decision(agent, "search", "area-checked");
            return false;
        }
        agent.ContactInspectUntil = null;
        // Existing navigation owns reservations, bounded route searches and failed-route backoff.
        InvestigateContact(uid, agent, point, now, sound: sound);
        Decision(agent, "search", "approaching-last-known-position");
        return true;
    }
}
