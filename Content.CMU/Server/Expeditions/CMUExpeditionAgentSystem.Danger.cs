using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void ShareKnownDanger(CMUExpeditionAgentComponent sender, CMUExpeditionAgentComponent receiver,
        EntityCoordinates contact, TimeSpan now)
    {
        // Native radio delivery gates this copy. Repeated reports retain the original expiry
        // and weight; hearing the same casualty twice is not a second casualty.
        receiver.KnownDanger.RemoveAll(danger => danger.Until <= now || !Exists(danger.Point.EntityId));
        foreach (var danger in sender.KnownDanger)
        {
            if (danger.Until <= now || !Exists(danger.Point.EntityId) || !_transform.InRange(contact, danger.Point, 15))
                continue;
            var index = receiver.KnownDanger.FindIndex(known => _transform.InRange(known.Point, danger.Point, 0.8f));
            if (index >= 0)
            {
                var known = receiver.KnownDanger[index];
                if (known.Until >= danger.Until && known.Weight >= danger.Weight)
                    continue;
                receiver.KnownDanger[index] = danger with
                {
                    Until = known.Until > danger.Until ? known.Until : danger.Until,
                    Weight = Math.Max(known.Weight, danger.Weight),
                    Radius = Math.Max(known.Radius, danger.Radius),
                };
            }
            else
            {
                if (receiver.KnownDanger.Count >= 12)
                    receiver.KnownDanger.RemoveAt(0);
                receiver.KnownDanger.Add(danger);
            }
        }
        foreach (var lane in sender.KnownFireLanes)
            if (lane.Until > now && Exists(lane.Start.EntityId) && Exists(lane.End.EntityId) &&
                (_transform.InRange(contact, lane.Start, 15) || _transform.InRange(contact, lane.End, 15)))
                StoreFireLane(receiver, lane, now);
        receiver.ExposureScores.Clear();
    }

    private void RememberDanger(CMUExpeditionAgentComponent agent, EntityCoordinates point, TimeSpan now,
        float weight, float radius, float seconds)
    {
        if (Transform(point.EntityId).MapUid is not { } map)
            return;
        // Only the witnessed location survives. The attacker's entity or later position is never retained here.
        point = _transform.ToCoordinates(map, _transform.ToMapCoordinates(point));
        agent.KnownDanger.RemoveAll(danger => danger.Until <= now || !Exists(danger.Point.EntityId));
        var index = agent.KnownDanger.FindIndex(danger => _transform.InRange(danger.Point, point, 0.8f));
        if (index >= 0)
        {
            var previous = agent.KnownDanger[index];
            weight = Math.Min(18, Math.Max(weight, previous.Weight + 1));
            radius = Math.Max(radius, previous.Radius);
            seconds = Math.Max(seconds, (float) (previous.Until - now).TotalSeconds);
            agent.KnownDanger.RemoveAt(index);
        }
        if (agent.KnownDanger.Count >= 12)
            agent.KnownDanger.RemoveAt(0);
        agent.KnownDanger.Add(new CMUExpeditionDanger(point, now + TimeSpan.FromSeconds(seconds), weight, radius));
        agent.ExposureScores.Clear();
    }

    private float KnownDangerCost(CMUExpeditionAgentComponent agent, EntityCoordinates point)
    {
        if (agent.KnownDanger.Count == 0 && agent.KnownFireLanes.Count == 0)
            return 0;
        var cost = 0f;
        var location = _transform.ToMapCoordinates(point);
        foreach (var danger in agent.KnownDanger)
        {
            if (danger.Until <= _timing.CurTime || !Exists(danger.Point.EntityId))
                continue;
            var remembered = _transform.ToMapCoordinates(danger.Point);
            if (remembered.MapId != location.MapId)
                continue;
            var distance = Vector2.Distance(remembered.Position, location.Position);
            cost = Math.Max(cost, danger.Weight * Math.Max(0, 1 - distance / danger.Radius));
        }
        foreach (var lane in agent.KnownFireLanes)
        {
            if (lane.Until <= _timing.CurTime || !Exists(lane.Start.EntityId) || !Exists(lane.End.EntityId))
                continue;
            var first = _transform.ToMapCoordinates(lane.Start);
            var last = _transform.ToMapCoordinates(lane.End);
            if (first.MapId == location.MapId && last.MapId == location.MapId)
                cost = Math.Max(cost, CMUCornerResponsePolicy.LaneCost(location.Position, first.Position, last.Position,
                    lane.Weight, lane.Width));
        }
        return cost;
    }

    private bool KnownDangerPassage(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates from, EntityCoordinates to)
    {
        if (agent.KnownDanger.Count == 0 && agent.KnownFireLanes.Count == 0)
            return true;
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        if (start.MapId != end.MapId)
            return false;
        return CMUCornerResponsePolicy.PassageSafe(start.Position, end.Position, position =>
            KnownDangerCost(agent, _transform.ToCoordinates(from.EntityId, new MapCoordinates(position, start.MapId))));
    }

    private void StoreFireLane(CMUExpeditionAgentComponent agent, CMUExpeditionFireLane lane, TimeSpan now)
    {
        agent.KnownFireLanes.RemoveAll(known => known.Until <= now || !Exists(known.Start.EntityId) || !Exists(known.End.EntityId));
        var index = agent.KnownFireLanes.FindIndex(known =>
            _transform.InRange(known.Start, lane.Start, 0.8f) && _transform.InRange(known.End, lane.End, 0.8f));
        if (index >= 0)
        {
            if (agent.KnownFireLanes[index].Until >= lane.Until)
                return;
            agent.KnownFireLanes[index] = lane;
        }
        else
        {
            if (agent.KnownFireLanes.Count >= 8)
                agent.KnownFireLanes.RemoveAt(0);
            agent.KnownFireLanes.Add(lane);
        }
        agent.ExposureScores.Clear();
        agent.NextThink = now;
    }

    private void ObserveIncomingLane(EntityUid uid, CMUExpeditionAgentComponent agent,
        MapCoordinates passing, Vector2 direction, TimeSpan now)
    {
        if (now < agent.NextFireLaneObservation || direction.LengthSquared() < 0.01f || Transform(uid).MapUid is not { } map)
            return;
        agent.NextFireLaneObservation = now + TimeSpan.FromSeconds(0.4);
        direction = Vector2.Normalize(direction);
        var observer = _transform.GetMapCoordinates(uid);
        if (observer.MapId != passing.MapId || !RayClear(uid, observer, passing, shelter: true))
            return;
        // A near pass reveals only a short local segment. Clip both ends against actual
        // sight and geometry; never extend it to the unseen muzzle or through the next wall.
        MapCoordinates End(float sign, int steps)
        {
            var endpoint = passing;
            for (var step = 1; step <= steps; step++)
            {
                var candidate = passing.Offset(direction * (sign * step * 0.5f));
                if (!RayClear(uid, observer, candidate, shelter: true) || !RayClear(uid, passing, candidate, shelter: true))
                    break;
                endpoint = candidate;
            }
            return endpoint;
        }
        var first = End(-1, 6);
        var last = End(1, 3);
        if (Vector2.DistanceSquared(first.Position, last.Position) < 0.25f)
            return;
        var lane = new CMUExpeditionFireLane(_transform.ToCoordinates(map, first), _transform.ToCoordinates(map, last),
            now + TimeSpan.FromSeconds(10), 12, 1.25f);
        StoreFireLane(agent, lane, now);
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || HasComp<ActorComponent>(other) || !_mobs.IsAlive(other) ||
                !SameSquad(uid, agent, other, buddy) || !Visible(other, uid, 10))
                continue;
            // A local visual warning supplies only the endpoints that this observer can
            // also see. Full frozen segments travel separately through native radio.
            var eye = _transform.GetMapCoordinates(other);
            if (RayClear(other, eye, first, shelter: true) && RayClear(other, eye, last, shelter: true))
                StoreFireLane(buddy, lane, now);
        }
    }

    private void ObserveCasualtyDanger(EntityUid casualty)
    {
        if (!_npcs.Enabled)
            return;
        var now = _timing.CurTime;
        var point = Transform(casualty).Coordinates;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var observer, out var agent))
        {
            if (observer == casualty || HasComp<ActorComponent>(observer) || !_mobs.IsAlive(observer) ||
                !IsFriendly(observer, casualty) || !Visible(observer, casualty, 12))
                continue;
            RememberDanger(agent, point, now, 16, 1.7f, 24);
            agent.Stress = Math.Min(1, agent.Stress + 0.2f);
            agent.NextThink = now;
            // Re-evaluate any cached approach through the witnessed casualty, including patrol orders.
            agent.Route.Clear();
            agent.RouteDestination = null;
            agent.OrderRoute.Clear();
            agent.InvestigationDestination = null;
            agent.IncomingFireDecision = "witnessed-casualty-avoiding-approach";
        }
    }

    private void RememberIncomingFire(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates point, TimeSpan now, bool hit)
    {
        agent.IncomingFireUntil = now + TimeSpan.FromSeconds(hit ? 2.5 : 1.5);
        agent.SuppressedUntil = now + TimeSpan.FromSeconds(hit ? 1.5 : 1);
        if (!hit && now < agent.NextIncomingObservation)
            return;
        agent.NextThink = now;
        agent.NextIncomingObservation = now + TimeSpan.FromSeconds(0.4);
        RememberDanger(agent, point, now, hit ? 12 : 7, hit ? 1.5f : 1.1f, hit ? 12 : 6);
        if (!hit)
            return;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var observer, out var buddy))
        {
            if (observer == uid || HasComp<ActorComponent>(observer) || !_mobs.IsAlive(observer) ||
                !IsFriendly(observer, uid) || !Visible(observer, uid, 10))
                continue;
            RememberDanger(buddy, point, now, 9, 1.3f, 8);
            buddy.NextThink = now;
        }
    }

    private bool RespondToIncomingFire(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        agent.KnownDanger.RemoveAll(danger => danger.Until <= now || !Exists(danger.Point.EntityId));
        agent.KnownFireLanes.RemoveAll(lane => lane.Until <= now || !Exists(lane.Start.EntityId) || !Exists(lane.End.EntityId));
        var start = Transform(uid).Coordinates;
        if (now >= agent.IncomingFireUntil)
        {
            if (now >= agent.SuppressedUntil && now - agent.LastHit > TimeSpan.FromSeconds(3) &&
                KnownDangerCost(agent, start) < 1 && BodyFits(uid, start) &&
                (agent.LastSafePosition is not { } safe || !Exists(safe.EntityId) || !_transform.InRange(start, safe, 2)))
                agent.LastSafePosition = start;
            return false;
        }
        if (agent.SpacingDestination is { } committed && !KnownDangerPassage(uid, agent, start, committed))
        {
            StopSpacing(uid, agent);
            agent.NextIncomingResponse = TimeSpan.Zero;
        }
        if (agent.RushTarget != null || agent.SpacingDestination != null || now < agent.NextIncomingResponse)
            return false;
        if (ContinuingDangerEscape(uid, agent, now))
        {
            agent.IncomingFireDecision = "continuing-escape-to-cover";
            return false;
        }
        // A clear return shot retains the ordinary burst/cover rhythm. An unseen or corner-blocked
        // attacker must not bypass self-preservation merely because Observe found no target.
        if (agent.LastDamage < agent.EmergencyHealDamage && agent.Target is { } target &&
            Visible(uid, target, WeaponFireRange(uid, agent)) && _guns.TryGetGun(uid, out var gun) &&
            WeaponAmmo(gun) > 0 && TryAimPoint(uid, agent, gun, out var aim) && SafeShot(uid, agent, gun, aim))
            return false;
        agent.NextIncomingResponse = now + TimeSpan.FromSeconds(0.75);
        var currentCost = KnownDangerCost(agent, start);
        var currentExposure = ExposureScore(uid, agent, start);
        var candidates = new List<EntityCoordinates>();
        if (agent.LastSafePosition is { } previous && Exists(previous.EntityId) && _transform.InRange(start, previous, 4))
            candidates.Add(previous);
        foreach (var distance in new[] { 2.25f, 1.25f, 0.75f })
        for (var direction = 0; direction < 8; direction++)
        {
            var angle = direction * MathF.Tau / 8;
            candidates.Add(start.Offset(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance));
        }
        EntityCoordinates? best = null;
        var bestScore = float.MinValue;
        foreach (var candidate in candidates)
        {
            var danger = KnownDangerCost(agent, candidate);
            if (danger >= currentCost - 0.5f || agent.Home is not { } home ||
                !_transform.InRange(home, candidate, agent.LeashRange) || Reserved(uid, candidate) ||
                !TraversablePassage(uid, start, candidate, allowVault: false) || !KnownDangerPassage(uid, agent, start, candidate) ||
                ExposureScore(uid, agent, candidate) > currentExposure + 0.5f ||
                MeleeClearance(agent, candidate) < Math.Min(3, MeleeClearance(agent, start)))
                continue;
            var score = currentCost - danger - FriendlyCrowding(uid, candidate) * 2 -
                ExposureScore(uid, agent, candidate) + (agent.LastSafePosition == candidate ? 3 : 0);
            if (score <= bestScore)
                continue;
            best = candidate;
            bestScore = score;
        }
        if (best is not { } escape)
        {
            agent.IncomingFireDecision = "incoming-fire-no-safe-step";
            return false;
        }
        CancelAimedWeapon(agent);
        CancelFlare(uid, agent);
        BeginCombatSpacing(uid, agent, now);
        agent.SpacingDestination = escape;
        agent.SpacingUntil = agent.SpacingMoveUntil = now + TimeSpan.FromSeconds(2);
        agent.SpacingDecision = "evading-incoming-fire";
        agent.IncomingFireDecision = "leaving-observed-fire-lane";
        agent.IncomingFireEscapes++;
        agent.ImmediateFireUntil = now + TimeSpan.FromSeconds(1);
        ReadyRifle(uid, agent);
        Move(uid, escape, validated: true);
        return true;
    }

    private bool ContinuingDangerEscape(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.CoverDestination is not { } destination || now >= agent.MoveUntil ||
            now - agent.MoveProgressAt >= TimeSpan.FromSeconds(1.5) ||
            agent.State is not (CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.Withdraw or
                CMUExpeditionAgentState.OutOfAmmo or CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.PlanMove) ||
            GrenadeDanger(destination))
            return false;
        var start = Transform(uid).Coordinates;
        var next = agent.RouteDestination == destination && agent.Route.TryPeek(out var waypoint) ? waypoint : destination;
        // Incoming rounds are expected during a withdrawal. Preserve a progressing
        // route to safer ground instead of cancelling it for a new sidestep every hit.
        return KnownDangerCost(agent, destination) < KnownDangerCost(agent, start) - 0.5f &&
            KnownDangerPassage(uid, agent, start, next) &&
            ExposureScore(uid, agent, destination) <= ExposureScore(uid, agent, start) &&
            MeleeClearance(agent, next) >= Math.Min(agent.MeleeStandoffRange, MeleeClearance(agent, start));
    }
}
