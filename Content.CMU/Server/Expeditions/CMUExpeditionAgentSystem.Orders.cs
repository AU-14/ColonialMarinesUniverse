using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using System.Linq;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private const string AllHostileFaction = "AllHostile";

    // In expedition target orders this is an engagement mode, not a requirement
    // that the enemy belongs to the native AllHostile faction.
    private static bool TargetsAllHostiles(CMUExpeditionAgentComponent agent) =>
        agent.TargetFactions.Contains(AllHostileFaction);

    private bool ProtectedSquadmate(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid other) =>
        agent.Squad != 0 && TryComp<CMUExpeditionAgentComponent>(other, out var buddy) &&
        buddy.Squad == agent.Squad && Transform(uid).MapID == Transform(other).MapID;

    public bool TryParseFactionOrders(string value, bool targets, out string[] factions, out string invalid)
    {
        factions = Array.Empty<string>();
        invalid = "";
        if (value.Trim().Equals("default", StringComparison.OrdinalIgnoreCase))
            return true;
        var entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < entries.Length; i++)
        {
            if (targets && (entries[i].Equals(AllHostileFaction, StringComparison.OrdinalIgnoreCase) ||
                            entries[i].Equals("allhostiles", StringComparison.OrdinalIgnoreCase)))
                entries[i] = AllHostileFaction;
            if (ProtoMan.HasIndex<NpcFactionPrototype>(entries[i]))
                continue;
            invalid = entries[i];
            return false;
        }
        factions = entries.Distinct().ToArray();
        return true;
    }

    private bool IsFriendly(EntityUid uid, EntityUid other)
    {
        if (uid == other)
            return true;
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var agent))
        {
            var allHostile = TargetsAllHostiles(agent);
            if (allHostile && (ProtectedSquadmate(uid, agent, other) || _factions.IsIgnored(uid, other)))
                return true;
            if (VehicleBody(other))
                return VehicleDisposition(uid, agent, other) < 0;
            if (TryComp<NpcFactionMemberComponent>(other, out var factions))
            {
                if (factions.Factions.Any(f => agent.FriendlyFactions.Contains(f.Id)))
                    return true;
                if (!allHostile && factions.Factions.Any(f => agent.TargetFactions.Contains(f.Id)))
                    return false;
            }
        }
        return _factions.IsEntityFriendly(uid, other);
    }

    private IEnumerable<EntityUid> ExpeditionHostiles(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var nearby = _nearbyPool.Get();
        try
        {
            // Infantry and vehicle acquisition share the same spatial query. Keep the
            // collection leased until enumeration ends, including early-return callers.
            _lookup.GetEntitiesInRange(uid, agent.DetectionRange, nearby);
            foreach (var other in nearby)
                if (AcquisitionHostile(uid, agent, other))
                    yield return other;
            // Native retaliation is not range-limited until the visibility check.
            if (agent.TargetFactions.Count == 0 && HasComp<NpcFactionMemberComponent>(uid))
                foreach (var other in _factions.GetHostiles(uid))
                    if (!nearby.Contains(other) && AcquisitionHostile(uid, agent, other))
                        yield return other;
        }
        finally
        {
            _nearbyPool.Return(nearby);
        }
    }

    private bool AcquisitionHostile(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid other)
    {
        if (VehicleBody(other))
            return ArmedVehicle(other) && VehicleDisposition(uid, agent, other) > 0;
        if (agent.TargetFactions.Count == 0 && !HasComp<NpcFactionMemberComponent>(other) &&
            !_factions.GetHostiles(uid).Contains(other))
            return false;
        return (agent.TargetFactions.Count > 0 || HasComp<NpcFactionMemberComponent>(uid)) &&
            AcceptOrderedContact(uid, agent, other);
    }

    private bool NearbyExpeditionHostile(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid other)
    {
        if (!Exists(other))
            return false;
        var retaliation = agent.TargetFactions.Count == 0 && !VehicleBody(other) &&
            HasComp<NpcFactionMemberComponent>(uid) && _factions.GetHostiles(uid).Contains(other);
        return (retaliation || _transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, agent.DetectionRange)) &&
            AcquisitionHostile(uid, agent, other);
    }

    private bool AcceptOrderedContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target)
    {
        if (!Exists(target) || IsFriendly(uid, target))
            return false;
        if (VehicleBody(target))
            return ArmedVehicle(target) && VehicleDisposition(uid, agent, target) > 0;
        var orderedFaction = agent.TargetFactions.Count > 0 &&
            TryComp<NpcFactionMemberComponent>(target, out var member) &&
            member.Factions.Any(f => agent.TargetFactions.Contains(f.Id));
        if (TargetsAllHostiles(agent))
            return _mobs.IsAlive(target) && (orderedFaction || NativeHostile(uid, target) || HasEquippedWeapon(target));
        if (agent.TargetFactions.Count > 0)
            return orderedFaction;
        // Receiving another squad's report does not make its neutral contacts hostile.
        // Match native acquisition, including per-entity retaliation and ignore rules.
        return NativeHostile(uid, target);
    }

    private bool NativeHostile(EntityUid uid, EntityUid target) =>
        !_factions.IsIgnored(uid, target) &&
            (TryComp<NpcFactionMemberComponent>(uid, out var observer) &&
                TryComp<NpcFactionMemberComponent>(target, out var contact) && observer.HostileFactions.Overlaps(contact.Factions) ||
             _factions.GetHostiles(uid).Contains(target));

    // Fists and animal bites are innate melee components, not evidence that a neutral
    // is armed. Inspect held items, never hidden inventory, for non-hostile contacts.
    private bool HasEquippedWeapon(EntityUid target) => HasComp<GunComponent>(target) ||
        _hands.EnumerateHeld(target).Any(item => HasComp<GunComponent>(item) || HasComp<MeleeWeaponComponent>(item));

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
