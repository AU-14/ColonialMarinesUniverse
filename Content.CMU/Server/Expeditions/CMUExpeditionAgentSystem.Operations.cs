using System.Linq;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private sealed record SquadAssistanceClaim(EntityUid Responder, EntityUid Target, EntityCoordinates Origin,
        EntityCoordinates Contact, int Side, TimeSpan Until);

    private readonly Dictionary<EntityUid, SquadAssistanceClaim> _assistanceClaims = new();

    private bool CoordinatedSquadMember(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityUid other, CMUExpeditionAgentComponent buddy) => SameSquad(uid, agent, other, buddy) ||
        agent.CoordinateSquads && buddy.CoordinateSquads && Transform(uid).MapID == Transform(other).MapID &&
        IsFriendly(uid, other) && IsFriendly(other, uid) &&
        (agent.SupportSquadRoot != null && agent.SupportSquadRoot == buddy.SquadRoot && _timing.CurTime < agent.SupportUntil ||
         buddy.SupportSquadRoot != null && buddy.SupportSquadRoot == agent.SquadRoot && _timing.CurTime < buddy.SupportUntil);

    private bool CanShareContact(EntityUid source, CMUExpeditionAgentComponent sender,
        EntityUid receiver, CMUExpeditionAgentComponent recipient) => SameSquad(source, sender, receiver, recipient) ||
        sender.CoordinateSquads && recipient.CoordinateSquads && sender.SquadRoot != null && recipient.SquadRoot != null &&
        Transform(source).MapID == Transform(receiver).MapID && IsFriendly(source, receiver) && IsFriendly(receiver, source);

    // Called only after the native radio send/receive checks succeeded. One other squad can
    // answer a contact; it keeps its patrol route and resumes it when the report expires.
    private bool AcceptSquadAssistance(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityUid source, CMUExpeditionAgentComponent sender, EntityUid target, EntityCoordinates point, TimeSpan now)
    {
        if (SameSquad(uid, agent, source, sender))
            return true;
        if (Transform(source).MapUid is not { } map || !Exists(point.EntityId))
            return Decline("support-map-unavailable");
        // Reports retain their observed world position when a supporting grid moves.
        point = _transform.ToCoordinates(map, _transform.ToMapCoordinates(point));
        if (!CanShareContact(source, sender, uid, agent) || agent.SquadRoot is not { } root ||
            sender.SquadRoot is not { } sourceRoot || now < agent.NextAssistance ||
            !AvailableForAssistance(uid, agent, sourceRoot, target, point, now))
            return Decline("busy-or-outside-support-area");
        if (_assistanceClaims.TryGetValue(sourceRoot, out var claim) && claim.Until > now &&
            claim.Responder != root && _squadPlans.TryGetValue(claim.Responder, out var responder) &&
            responder.Members.Any(CanOrderSquadMember))
            return Decline("another-squad-responding");
        // Do not drag an already engaged squad away from its own fight or send reinforcements
        // back to the squad they are currently helping in response to its relayed contact.
        if (sender.SupportSquadRoot != null && sender.SupportUntil > now || PlanFor(agent) is not { } plan ||
            plan.Leader is not { } leader || !CanOrderSquadMember(leader) ||
            plan.Members.Any(member => CanOrderSquadMember(member) &&
                (!AvailableForAssistance(member, Comp<CMUExpeditionAgentComponent>(member), sourceRoot, target, point, now) ||
                 !_transform.InRange(Transform(leader).Coordinates, Transform(member).Coordinates, 10))))
            return Decline("keeping-squad-together");
        if (claim != null && claim.Until > now && claim.Responder == root &&
            (claim.Target != target || !_transform.InRange(claim.Contact, point, 8)))
            return Decline("maintaining-support-contact");
        if (claim == null || claim.Until <= now || claim.Responder != root)
        {
            var origin = _transform.ToCoordinates(map, _transform.GetMapCoordinates(source));
            var direction = _transform.ToMapCoordinates(point).Position - _transform.ToMapCoordinates(origin).Position;
            var offset = _transform.GetWorldPosition(leader) - _transform.ToMapCoordinates(origin).Position;
            var side = Vector2.Dot(offset, new Vector2(-direction.Y, direction.X)) >= 0 ? 1 : -1;
            claim = new SquadAssistanceClaim(root, target, origin, point, side, now + TimeSpan.FromSeconds(10));
        }
        else
            claim = claim with { Contact = point, Until = now + TimeSpan.FromSeconds(10) };
        _assistanceClaims[sourceRoot] = claim;
        if (agent.SupportSquadRoot != sourceRoot || agent.SupportTarget != target)
        {
            agent.SupportBestDistance = float.MaxValue;
            agent.SupportProgressAt = now;
            agent.AssistanceAccepted++;
        }
        agent.SupportSquadRoot = sourceRoot;
        agent.SupportTarget = target;
        agent.SupportOrigin = claim.Origin;
        agent.SupportContact = point;
        agent.SupportFlankSide = claim.Side;
        agent.SupportUntil = claim.Until;
        agent.AssistanceDecision = "responding-with-squad";
        agent.OperationsDecision = "assisting-friendly-squad";
        return true;

        bool Decline(string reason)
        {
            agent.AssistanceDeclined++;
            agent.AssistanceDecision = reason;
            return false;
        }
    }

    private bool AvailableForAssistance(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid sourceRoot,
        EntityUid target, EntityCoordinates point, TimeSpan now) => agent.CoordinateSquads &&
        !agent.HoldPosition && !agent.Entrench && now >= agent.NextAssistance &&
        agent.TravelGoal == null && (agent.OrderedDestination == null || agent.Patrolling) &&
        agent.Action == null && agent.Treatment == null && agent.PendingWeapon == null &&
        agent.State is not (CMUExpeditionAgentState.Healing or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.Withdraw) &&
        agent.RecoveryUntil <= now && agent.LastDamage < agent.RetreatDamage &&
        agent.Home is { } home && _transform.InRange(home, point, agent.LeashRange) &&
        (agent.AutoPatrolAnchor is not { } anchor || _transform.InRange(anchor, point, agent.LeashRange)) &&
        Transform(uid).MapID == Transform(point.EntityId).MapID && AcceptOrderedContact(uid, agent, target) &&
        (agent.ContactFromRadio || agent.Target == null || now - agent.LastContact >= agent.LostSightDelay || agent.Target == target) &&
        (agent.SupportSquadRoot == null || agent.SupportSquadRoot == sourceRoot || now >= agent.SupportUntil);

    private void UpdateSquadAssistance(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.SupportSquadRoot is not { } root)
            return;
        var invalid = !Exists(root) || !agent.CoordinateSquads || agent.HoldPosition || agent.Entrench ||
            agent.TravelGoal != null || agent.OrderedDestination != null && !agent.Patrolling ||
            agent.SupportTarget is not { } target || !AcceptOrderedContact(uid, agent, target) ||
            agent.SupportContact is not { } point || !Exists(point.EntityId) ||
            Transform(point.EntityId).MapID != Transform(uid).MapID;
        var stalled = false;
        if (!invalid && agent.SupportContact is { } contact &&
            Transform(uid).Coordinates.TryDistance(EntityManager, contact, out var distance))
        {
            if (distance < agent.SupportBestDistance - .5f ||
                agent.Target == agent.SupportTarget && (!agent.ContactFromRadio || now - agent.LastShotAt < TimeSpan.FromSeconds(2)))
            {
                agent.SupportBestDistance = distance;
                agent.SupportProgressAt = now;
            }
            // Expiry must not be refreshed forever by reports when the route makes no
            // progress. Being in a real firing position counts as productive assistance.
            stalled = agent.ContactFromRadio && now - agent.SupportProgressAt >= TimeSpan.FromSeconds(10);
        }
        if (!invalid && !stalled && now < agent.SupportUntil)
            return;
        agent.SupportSquadRoot = null;
        agent.SupportUntil = TimeSpan.Zero;
        agent.NextAssistance = now + TimeSpan.FromSeconds(stalled ? 12 : 4);
        agent.AssistanceDecision = stalled ? "support-route-stalled" : invalid ? "support-no-longer-valid" : "support-report-expired";
        if (agent.ContactFromRadio && agent.Target == agent.SupportTarget)
        {
            agent.Target = null;
            agent.LastSeen = null;
            if (agent.RadioTarget == agent.SupportTarget)
                agent.RadioPosition = null;
            agent.ContactFromRadio = false;
            agent.ForgetAt = now;
            if (agent.State is CMUExpeditionAgentState.Investigate or CMUExpeditionAgentState.Watch)
            {
                ClearCover(agent);
                agent.State = CMUExpeditionAgentState.Guard;
                _steering.Unregister(uid);
            }
        }
        agent.SupportTarget = null;
        agent.SupportContact = null;
        agent.OperationsDecision = agent.AutoPatrol ? "auto-patrol" : "local-orders";
    }

    private EntityCoordinates SupportApproach(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates contact, EntityCoordinates fallback)
    {
        if (!agent.ContactFromRadio || agent.SupportSquadRoot == null || _timing.CurTime >= agent.SupportUntil ||
            agent.SupportOrigin is not { } origin || !Exists(origin.EntityId) ||
            agent.SupportTarget != agent.Target || agent.SupportContact is not { } reported)
            return fallback;
        contact = reported;
        var start = Transform(uid).Coordinates;
        var target = _transform.ToMapCoordinates(contact);
        var source = _transform.ToMapCoordinates(origin);
        var position = _transform.GetMapCoordinates(uid);
        var direction = target.Position - source.Position;
        if (source.MapId != target.MapId || target.MapId != position.MapId || direction.LengthSquared() < 4)
            return fallback;
        direction = Vector2.Normalize(direction);
        var side = new Vector2(-direction.Y, direction.X);
        var sign = agent.SupportFlankSide != 0 ? agent.SupportFlankSide :
            Vector2.Dot(position.Position - source.Position, side) >= 0 ? 1 : -1;
        // The assisting squad approaches a different bearing while the reporting squad
        // holds contact. This is a frozen radio position, never a hidden target transform.
        var flank = target.Position - direction * Math.Clamp(agent.PreferredFireRange, 4, 8) + side * (sign * 5);
        var delta = flank - position.Position;
        if (delta.LengthSquared() < 1)
            return fallback;
        if (delta.LengthSquared() > 64)
            delta = Vector2.Normalize(delta) * 8;
        var point = _transform.ToCoordinates(start.EntityId, position.Offset(delta));
        if (!ValidOrderPoint(uid, point) || agent.Home is not { } home ||
            !_transform.InRange(home, point, agent.LeashRange) || !KnownDangerPassage(uid, agent, start, point))
            return fallback;
        agent.OperationsDecision = "supporting-from-flank";
        return point;
    }

}
