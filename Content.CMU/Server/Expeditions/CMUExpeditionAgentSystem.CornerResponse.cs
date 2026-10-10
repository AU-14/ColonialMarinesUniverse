using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private TimeSpan _nextCornerRoute;

    private static void ClearCornerResponse(CMUExpeditionAgentComponent agent)
    {
        agent.CornerHolding = false;
        agent.CornerDangerPoint = null;
        agent.CornerUntil = TimeSpan.Zero;
        agent.CornerDestination = null;
        agent.CornerMoveUntil = TimeSpan.Zero;
        agent.CornerProbeUntil = TimeSpan.Zero;
        agent.CornerFlanking = false;
        agent.CornerFlankCandidate = 0;
        agent.NextCornerSearch = TimeSpan.Zero;
        agent.CornerDecision = "clear";
    }

    private bool RespondToHeldCorner(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var start = Transform(uid).Coordinates;
        if (agent.CornerDestination is { } moving)
        {
            if (agent.Action != null || agent.CoverDestination != moving || agent.RushTarget != null ||
                now >= agent.CornerMoveUntil || now - agent.LastHit < TimeSpan.FromSeconds(0.5))
            {
                StopCornerMove(uid, agent);
            }
            else
            {
                if (ContinueMove(uid, agent, Transform(uid), now))
                    return true;
                if (!agent.LastMoveFailed && agent.CornerFlanking)
                {
                    agent.CornerFlanks++;
                    agent.NextCornerSearch = now + TimeSpan.FromSeconds(8);
                }
                StopCornerMove(uid, agent);
            }
        }

        agent.CornerHolding = false;
        if (agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null || agent.FlareItem != null ||
            agent.ScavengeTarget != null || agent.SpacingDestination != null || agent.RushTarget != null ||
            agent.LastDamage >= agent.RetreatDamage || agent.RecoveryUntil > now || CommittedMovement(agent))
            return false;
        // A ready defender with a real lane keeps fighting. The other members stage behind
        // the remembered danger rather than successively occupying the casualty's tile.
        if (_guns.TryGetGun(uid, out var gun) && WeaponAmmo(gun) > 0 &&
            TryAimPoint(uid, agent, gun, out var aim) && SafeShot(uid, agent, gun, aim))
            return false;
        if (!FindHeldCorner(uid, agent, now, out var danger, out var until))
        {
            agent.CornerDangerPoint = null;
            agent.CornerUntil = TimeSpan.Zero;
            return false;
        }
        agent.CornerHolding = true;
        agent.CornerDangerPoint = danger;
        agent.CornerUntil = until;
        agent.CornerDecision = "containing-observed-lane";
        if (now < agent.NextCornerSearch || agent.HoldPosition || agent.Entrench ||
            HasCoverCommitment(uid, agent, now) || !_guns.TryGetGun(uid, out gun) || WeaponAmmo(gun) <= 0)
            return false;
        agent.NextCornerSearch = now + TimeSpan.FromSeconds(2);

        var position = _transform.GetMapCoordinates(uid);
        var marker = _transform.ToMapCoordinates(danger);
        var toward = marker.Position - position.Position;
        if (toward.LengthSquared() < 0.01f)
            toward = Vector2.UnitY;
        toward = Vector2.Normalize(toward);
        var side = new Vector2(-toward.Y, toward.X);
        var crowding = FriendlyCrowding(uid, start);
        var currentDanger = KnownDangerCost(agent, start);
        if (currentDanger >= 2 || crowding >= 2)
        {
            foreach (var offset in new[] { -toward * 1.5f, -toward * 2.25f, side * 1.5f, -side * 1.5f })
            {
                var candidate = _transform.ToCoordinates(start.EntityId, position.Offset(offset));
                if (!CornerPointSafe(uid, agent, candidate) || KnownDangerCost(agent, candidate) >= Math.Max(1, currentDanger) ||
                    FriendlyCrowding(uid, candidate) >= crowding && currentDanger < 2 ||
                    !TraversablePassage(uid, start, candidate, allowVault: false) || !KnownDangerPassage(uid, agent, start, candidate) ||
                    ExposureScore(uid, agent, candidate) > ExposureScore(uid, agent, start))
                    continue;
                ClearCover(agent);
                BeginCornerMove(uid, agent, candidate, now, false);
                agent.CornerStagingMoves++;
                return true;
            }
        }
        if (now < _nextCornerRoute || !CornerFlanker(uid, agent, danger, now))
            return false;
        // One probe per nearby squad every six seconds, including failed probes. A second
        // member cannot immediately repeat the same attempt after the first finds no route.
        agent.CornerProbeUntil = now + TimeSpan.FromSeconds(6);
        agent.NextCornerSearch = agent.CornerProbeUntil;
        _nextCornerRoute = now + TimeSpan.FromSeconds(0.25);
        for (var index = 0; index < 4; index++)
        {
            // Only one bounded route request per probe. Subsequent probes alternate candidate
            // bearings; every route edge must be ordinary ground and outside known fire.
            var selected = (index + agent.CornerFlankCandidate) % 4;
            var offset = CMUCornerResponsePolicy.FlankOffset(position.Position, marker.Position, selected);
            var candidate = _transform.ToCoordinates(start.EntityId, position.Offset(offset));
            if (!CornerPointSafe(uid, agent, candidate) || KnownDangerCost(agent, candidate) >= 2 ||
                ExposureScore(uid, agent, candidate) > ExposureScore(uid, agent, start) + 0.5f)
                continue;
            agent.CornerFlankCandidate = (selected + 1) % 4;
            ClearCover(agent);
            if (!BuildTacticalRoute(uid, agent, candidate, allowVaults: false) ||
                !CornerRouteSafe(uid, agent, start, out var sheltered) ||
                !sheltered && (!TryReserveManeuver(uid, agent, now) || agent.CoveringShooter == null))
            {
                ReleaseManeuver(uid, agent);
                ClearCover(agent);
                agent.CornerDecision = "holding-no-safe-alternate";
                return false;
            }
            BeginCornerMove(uid, agent, candidate, now, true);
            return true;
        }
        agent.CornerDecision = "holding-no-safe-alternate";
        return false;
    }

    private bool FindHeldCorner(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now,
        out EntityCoordinates point, out TimeSpan until)
    {
        point = default;
        until = TimeSpan.Zero;
        var start = Transform(uid).Coordinates;
        // Guard placement has its own danger-aware order route. Let it finish a safe
        // approach (or report blocked) instead of treating its straight bearing as a chase.
        if (agent.HoldPosition && agent.OrderedDestination != null)
            return false;
        var destination = agent.LastSeen != null && now < agent.ForgetAt ? agent.LastSeen : agent.OrderedDestination;
        if (destination is not { } goal || !Exists(goal.EntityId) || KnownDangerPassage(uid, agent, start, goal))
            return false;
        var position = _transform.GetMapCoordinates(uid);
        var target = _transform.ToMapCoordinates(goal);
        if (position.MapId != target.MapId)
            return false;
        var nearest = float.MaxValue;
        EntityCoordinates selectedPoint = default;
        var selectedUntil = TimeSpan.Zero;
        void Consider(EntityCoordinates candidate, TimeSpan expires, float weight)
        {
            if (weight < 9 || expires <= now || !Exists(candidate.EntityId))
                return;
            var map = _transform.ToMapCoordinates(candidate);
            if (map.MapId != position.MapId || !CMUCornerResponsePolicy.RelevantApproach(position.Position, map.Position, target.Position))
                return;
            var distance = Vector2.DistanceSquared(position.Position, map.Position);
            if (distance >= nearest)
                return;
            nearest = distance;
            selectedPoint = candidate;
            selectedUntil = expires;
        }
        foreach (var hazard in agent.KnownDanger)
            Consider(hazard.Point, hazard.Until, hazard.Weight);
        foreach (var lane in agent.KnownFireLanes)
            Consider(lane.End, lane.Until, lane.Weight);
        point = selectedPoint;
        until = selectedUntil;
        return nearest < float.MaxValue;
    }

    private bool CornerPointSafe(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point) =>
        agent.Home is { } home && _transform.InRange(home, point, agent.LeashRange) &&
        !Reserved(uid, point) && ValidOrderPoint(uid, point) && !GrenadeDanger(point) &&
        MeleeClearance(agent, point) >= Math.Min(3, MeleeClearance(agent, Transform(uid).Coordinates));

    private bool CornerRouteSafe(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start,
        out bool sheltered, EntityCoordinates? immediate = null)
    {
        sheltered = true;
        var remainingSamples = 128;
        if (immediate is { } next)
        {
            if (!CornerPassageSafe(uid, agent, start, next, ref sheltered, ref remainingSamples))
                return false;
            start = next;
        }
        foreach (var waypoint in agent.Route)
        {
            if (!CornerPassageSafe(uid, agent, start, waypoint, ref sheltered, ref remainingSamples))
                return false;
            start = waypoint;
        }
        return immediate != null || agent.Route.Count > 0;
    }

    private bool CornerPassageSafe(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start,
        EntityCoordinates end, ref bool sheltered, ref int remainingSamples)
    {
        if (!RoutePassage(uid, start, end, allowVault: false) || !KnownDangerPassage(uid, agent, start, end))
            return false;
        if (!sheltered)
            return true;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(end);
        sheltered = from.MapId == to.MapId && CMUCornerResponsePolicy.ShelteredPassage(from.Position, to.Position,
            sample => ShelteredFromKnownThreats(uid, agent,
                _transform.ToCoordinates(start.EntityId, new MapCoordinates(sample, from.MapId))), ref remainingSamples);
        return true;
    }

    // Native steering receives only a route whose current geometry still satisfies the
    // corner move's contract, including replacements from generic recovery/detour logic.
    private bool CornerMovementAllowed(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination)
    {
        if (agent.CornerDestination is not { } owned)
            return true;
        if (agent.CoverDestination != owned || agent.Action != null)
        {
            // A utility or survival action has taken ownership. Do not stop its movement.
            agent.CornerDestination = null;
            agent.CornerFlanking = false;
            agent.CornerMoveUntil = TimeSpan.Zero;
            return true;
        }
        var start = Transform(uid).Coordinates;
        var safe = agent.CornerFlanking
            ? CornerRouteSafe(uid, agent, start, out var sheltered, destination) &&
                (sheltered || TryReserveManeuver(uid, agent, _timing.CurTime) && agent.CoveringShooter != null)
            // A short survival stage may start exposed. It must leave known danger on
            // ordinary ground; requiring a shooter here would prevent the escape itself.
            : RoutePassage(uid, start, destination, allowVault: false) && KnownDangerPassage(uid, agent, start, destination);
        if (safe)
            return true;
        StopCornerMove(uid, agent);
        agent.LastMoveFailed = true;
        agent.CornerDecision = "holding-alternate-no-longer-safe";
        return false;
    }

    private bool CornerFlanker(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point, TimeSpan now)
    {
        if (agent.CombatRole is CMUExpeditionCombatRole.Support or CMUExpeditionCombatRole.Marksman or CMUExpeditionCombatRole.Medic ||
            now < agent.CornerProbeUntil)
            return false;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (!LocalSquadMember(uid, agent, other, buddy))
                continue;
            if (buddy.CornerDangerPoint is { } known && _transform.InRange(known, point, 3) &&
                (now < buddy.CornerProbeUntil || buddy.CornerDestination != null && buddy.CornerFlanking))
                return false;
            if (other.CompareTo(uid) < 0 && !buddy.HoldPosition && !buddy.Entrench && buddy.Action == null &&
                buddy.RushTarget == null && buddy.LastDamage < buddy.RetreatDamage && buddy.RecoveryUntil <= now &&
                buddy.PendingWeapon == null && buddy.Treatment == null && buddy.FlareItem == null && buddy.ScavengeTarget == null &&
                buddy.SpacingDestination == null && now >= buddy.NextCornerSearch && !CommittedMovement(buddy) &&
                !HasCoverCommitment(other, buddy, now) &&
                buddy.CombatRole is not (CMUExpeditionCombatRole.Support or CMUExpeditionCombatRole.Marksman or CMUExpeditionCombatRole.Medic) &&
                _guns.TryGetGun(other, out var gun) && WeaponAmmo(gun) > 0 &&
                !(TryAimPoint(other, buddy, gun, out var aim) && SafeShot(other, buddy, gun, aim)) &&
                FindHeldCorner(other, buddy, now, out var otherCorner, out _) && _transform.InRange(otherCorner, point, 3))
                return false;
        }
        return true;
    }

    private void BeginCornerMove(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point, TimeSpan now, bool flank)
    {
        agent.ContactDestination = null;
        agent.CornerDestination = point;
        agent.CornerFlanking = flank;
        agent.CornerMoveUntil = now + TimeSpan.FromSeconds(flank ? 8 : 3);
        agent.CornerDecision = flank ? "probing-safe-alternate" : "staging-behind-observed-lane";
        // PlanMove retains the already validated route without searching a second time.
        BeginMove(uid, agent, point, CMUExpeditionAgentState.PlanMove, now);
        agent.MoveUntil = agent.CornerMoveUntil;
    }

    private void StopCornerMove(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.CornerDestination is { } owned && (agent.CoverDestination == owned ||
            agent.CoverDestination == null && agent.Action == null && agent.State == CMUExpeditionAgentState.PlanMove))
        {
            _steering.Unregister(uid);
            ReleaseManeuver(uid, agent);
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
        }
        agent.CornerDestination = null;
        agent.CornerFlanking = false;
    }

    private bool HoldObservedCorner(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!agent.CornerHolding || now >= agent.CornerUntil || agent.Action != null || agent.Treatment != null ||
            agent.PendingWeapon != null || agent.SpacingDestination != null || agent.RushTarget != null ||
            agent.LastDamage >= agent.RetreatDamage || CommittedMovement(agent) ||
            ShouldTreat(agent, agent.LastDamage, now) && HasMedicine(uid))
            return false;
        _steering.Unregister(uid);
        agent.ContactDestination = null;
        if (_guns.TryGetGun(uid, out var gun) && WeaponAmmo(gun) > 0 &&
            TryAimPoint(uid, agent, gun, out var aim) && SafeShot(uid, agent, gun, aim))
        {
            if (agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage))
                Aim(agent, now, true);
        }
        else
            agent.State = CMUExpeditionAgentState.Watch;
        Decision(agent, "corner-containment", agent.CornerDecision);
        return true;
    }
}
