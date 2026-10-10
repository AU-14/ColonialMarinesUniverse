using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static void ClearGrenadeStaging(CMUExpeditionAgentComponent agent)
    {
        agent.GrenadeObservedContact = null;
        agent.GrenadeContactUntil = TimeSpan.Zero;
        agent.GrenadeThrowStance = null;
    }

    private void RememberGrenadeContact(CMUExpeditionAgentComponent agent, EntityCoordinates point, TimeSpan observed)
    {
        // A ground-map snapshot cannot follow a hidden entity or a moving parent afterwards.
        var map = Transform(point.EntityId).MapUid;
        if (map == null)
            return;
        agent.GrenadeObservedContact = _transform.ToCoordinates(map.Value, _transform.ToMapCoordinates(point));
        agent.GrenadeContactUntil = observed + TimeSpan.FromSeconds(6);
    }

    private bool RememberedGrenadeContact(CMUExpeditionAgentComponent agent, EntityCoordinates target) =>
        agent.GrenadeObservedContact is { } observed && Exists(observed.EntityId) &&
        _timing.CurTime < agent.GrenadeContactUntil && _transform.InRange(observed, target, 2);

    private bool TryPlanGrenade(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (Grenade(uid, false) is { } blast && BlastPoint(uid, agent, blast) is { } direct)
        {
            agent.GrenadeTarget = direct;
            agent.GrenadeDecision = "cluster-or-last-resort";
            RememberGrenadeContact(agent, direct, now);
            return true;
        }
        if (now < agent.NextGrenadeStaging || agent.ContactFromRadio || agent.LastContactWasMelee ||
            agent.MeleeThreats.Count > 0 || agent.RushTarget != null ||
            agent.LastSeen is not { } contact || !Exists(contact.EntityId) ||
            now - agent.LastContact > TimeSpan.FromSeconds(3) ||
            !(agent.CornerHolding || now < agent.IncomingFireUntil || agent.RecentShooters.Count > 0 ||
                agent.RepeatedPeekHits >= 2 && now - agent.LastPeekHit < TimeSpan.FromSeconds(10)))
            return false;

        agent.NextGrenadeStaging = now + TimeSpan.FromSeconds(3);
        RememberGrenadeContact(agent, contact, agent.LastContact);
        if (agent.GrenadeObservedContact is not { } frozen)
            return false;
        if (Grenade(uid, false) is { } grenade && !GrenadeDanger(frozen))
        {
            if (SafeGrenade(uid, frozen, grenade))
            {
                agent.GrenadeTarget = frozen;
                agent.GrenadeDecision = "flush-observed-angle";
                return true;
            }
            // Native throws follow one physical trajectory: choose a real new stance,
            // never bend a throw around a wall or promise an unverified ricochet.
            if (!agent.HoldPosition)
            {
                var stance = GrenadeThrowPosition(uid, agent, grenade, frozen, requireShelter: true);
                if (stance == null && TryReserveManeuver(uid, agent, now) && agent.CoveringShooter != null)
                    stance = GrenadeThrowPosition(uid, agent, grenade, frozen);
                if (stance != null)
                {
                    agent.GrenadeThrowStance = stance;
                    agent.GrenadeTarget = frozen;
                    agent.GrenadeDecision = "staging-corner-grenade";
                    return true;
                }
                ReleaseManeuver(uid, agent);
            }
        }

        // If no covered HE stance exists, screen the reachable near side of the angle.
        // The cloud may hide the enemy too, so this remains disabled for melee contacts.
        if (Grenade(uid, true) is { } smoke && CornerSmokePoint(uid, agent, smoke, frozen) is { } screen)
        {
            agent.SmokeGrenade = true;
            agent.GrenadeTarget = screen;
            agent.GrenadeDecision = "screen-held-corner";
            return true;
        }
        ClearGrenadeStaging(agent);
        return false;
    }

    private EntityCoordinates? GrenadeThrowPosition(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityUid grenade, EntityCoordinates target, bool requireShelter = false)
    {
        var start = Transform(uid).Coordinates;
        var exposure = ExposureScore(uid, agent, start);
        EntityCoordinates? best = null;
        var score = float.MaxValue;
        foreach (var distance in new[] { 1f, 2f, 3f })
        for (var direction = 0; direction < 8; direction++)
        {
            var angle = direction * MathF.Tau / 8;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            var point = start.Offset(offset);
            if (agent.Home is not { } home || !_transform.InRange(home, point, agent.LeashRange) ||
                Reserved(uid, point) || !GroundSafe(point) || !BodyFits(uid, point) ||
                !TraversablePassage(uid, start, point, allowVault: false) ||
                !TrafficPassageClear(uid, start, point) || !KnownDangerPassage(uid, agent, start, point) ||
                requireShelter && !GrenadeApproachSheltered(uid, start, point, target) ||
                ExposureScore(uid, agent, start.Offset(offset * 0.5f)) > exposure + 0.5f ||
                ExposureScore(uid, agent, point) > exposure + 0.5f ||
                !SafeGrenade(uid, target, grenade, point))
                continue;
            var candidateScore = distance + FriendlyCrowding(uid, point) * 2 + ExposureScore(uid, agent, point);
            if (candidateScore >= score)
                continue;
            score = candidateScore;
            best = point;
        }
        return best;
    }

    private bool GrenadeApproachSheltered(EntityUid uid, EntityCoordinates start, EntityCoordinates destination,
        EntityCoordinates threat)
    {
        var end = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(destination));
        var delta = end.Position - start.Position;
        var steps = Math.Max(1, (int) MathF.Ceiling(delta.Length() / 0.5f));
        for (var step = 0; step <= steps; step++)
            if (!Sheltered(uid, start.Offset(delta * (step / (float) steps)), threat))
                return false;
        return true;
    }

    private bool GrenadeApproachSupported(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates destination, EntityCoordinates threat, TimeSpan now) =>
        GrenadeApproachSheltered(uid, Transform(uid).Coordinates, destination, threat) ||
        TryReserveManeuver(uid, agent, now) && agent.CoveringShooter != null;

    private bool GrenadeStagingMovementAllowed(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates nextWaypoint)
    {
        if (agent.GrenadeThrowStance is not { } stance)
            return true;
        var start = Transform(uid).Coordinates;
        // Generic detours and traffic pockets must prove their actual next leg before
        // native steering starts. The original throw stance does not certify a new route.
        if (agent.Action == CMUTacticalAction.ThrowGrenade && agent.State == CMUExpeditionAgentState.PlanMove &&
            agent.CoverDestination == stance && _timing.CurTime < agent.ActionUntil &&
            agent.GrenadeTarget is { } target && agent.ActionItem is { } grenade && Exists(grenade) &&
            RememberedGrenadeContact(agent, target) && SafeGrenade(uid, target, grenade, stance) &&
            agent.Home is { } home && _transform.InRange(home, nextWaypoint, agent.LeashRange) &&
            GroundSafe(nextWaypoint) && BodyFits(uid, nextWaypoint) &&
            TraversablePassage(uid, start, nextWaypoint, allowVault: false) &&
            KnownDangerPassage(uid, agent, start, nextWaypoint) &&
            GrenadeApproachSupported(uid, agent, nextWaypoint, target, _timing.CurTime))
            return true;
        CancelPlan(uid, agent, true);
        return false;
    }

    private EntityCoordinates? CornerSmokePoint(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityUid smoke, EntityCoordinates contact)
    {
        var start = Transform(uid).Coordinates;
        var origin = _transform.ToMapCoordinates(start);
        var toward = _transform.ToMapCoordinates(contact).Position - origin.Position;
        if (toward.LengthSquared() < 9)
            return null;
        var direction = Vector2.Normalize(toward);
        var side = new Vector2(-direction.Y, direction.X);
        EntityCoordinates? best = null;
        var score = float.MinValue;
        foreach (var distance in new[] { Math.Min(4, toward.Length() * 0.75f), 3f, 2f })
        foreach (var angle in new[] { 0f, MathF.PI / 4, -MathF.PI / 4, MathF.PI * 5 / 12, -MathF.PI * 5 / 12 })
        {
            // A near-side landing beside the wall can screen an approach even when the
            // contact itself has no throw lane. Every candidate still needs a straight lane.
            var forward = MathF.Cos(angle) * distance;
            var sideways = MathF.Sin(angle) * distance;
            var point = _transform.ToCoordinates(start.EntityId, origin.Offset(direction * forward + side * sideways));
            var candidateScore = forward - Math.Abs(sideways) * 0.25f;
            if (candidateScore <= score || !GroundSafe(point) || !SafeGrenade(uid, point, smoke) ||
                SmokeReserved(uid, agent, point))
                continue;
            score = candidateScore;
            best = point;
        }
        return best;
    }

    private bool ContinueGrenadeStaging(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.GrenadeThrowStance is not { } stance || agent.GrenadeTarget is not { } target ||
            agent.ActionItem is not { } grenade || !Exists(grenade) ||
            !RememberedGrenadeContact(agent, target) || !SafeGrenade(uid, target, grenade, stance) ||
            !TraversablePassage(uid, Transform(uid).Coordinates, stance, allowVault: false) ||
            !KnownDangerPassage(uid, agent, Transform(uid).Coordinates, stance) ||
            !GrenadeApproachSupported(uid, agent, stance, target, now))
        {
            CancelPlan(uid, agent, true);
            return false;
        }
        if (ContinueMove(uid, agent, Transform(uid), now))
            return true;
        if (agent.LastMoveFailed || !_transform.InRange(Transform(uid).Coordinates, stance, 0.4f) ||
            !SafeGrenade(uid, target, grenade) ||
            // Arrival releases the movement reservation. Keep actual covering fire
            // through exposed grenade preparation, when the rifle must be lowered.
            !Sheltered(uid, Transform(uid).Coordinates, target) &&
                (!TryReserveManeuver(uid, agent, now) || agent.CoveringShooter == null))
        {
            CancelPlan(uid, agent, true);
            return false;
        }
        _steering.Unregister(uid);
        agent.Route.Clear();
        agent.RouteDestination = null;
        agent.CoverDestination = null;
        agent.ActiveMoveDestination = null;
        agent.GrenadeThrowStance = null;
        agent.State = CMUExpeditionAgentState.Throwing;
        agent.ActionStarted = now;
        if (_guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
        return true;
    }
}
