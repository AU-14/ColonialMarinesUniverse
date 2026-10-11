using System.Numerics;
using Content.Shared.NPC.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static void ClearTraffic(CMUExpeditionAgentComponent agent)
    {
        agent.TrafficGoal = null;
        agent.TrafficTicket = TimeSpan.Zero;
        agent.TrafficActiveUntil = TimeSpan.Zero;
        agent.TrafficYieldTo = null;
        agent.TrafficYieldPoint = null;
        agent.TrafficPassExit = null;
        agent.NextTrafficPocket = TimeSpan.Zero;
        agent.TrafficWaitingSince = null;
        agent.TrafficDecision = "clear";
        ClearTrafficYield(agent);
    }

    /// <summary>
    /// Reserve a short travel corridor, not the whole route. Queue behind leaders; opposing
    /// traffic uses a stable ticket and a physical passing pocket. Hard body clearance is never relaxed.
    /// </summary>
    private bool QueueMovement(EntityUid uid, CMUExpeditionAgentComponent agent, ref EntityCoordinates destination)
    {
        var now = _timing.CurTime;
        // A requested yield already validates the entire short body corridor every think.
        // It must not recursively ask the requester to move out of its own passing pocket.
        if (agent.TrafficNudgeRequester != null && now < agent.TrafficNudgeUntil)
            return true;
        var start = Transform(uid).Coordinates;
        var origin = _transform.ToMapCoordinates(start);
        var goal = _transform.ToMapCoordinates(destination);
        var delta = goal.Position - origin.Position;
        if (delta.LengthSquared() < 0.01f || agent.RushTarget != null || GrenadeDanger(start))
        {
            ClearTraffic(agent);
            return true;
        }
        if (agent.TrafficActiveUntil <= now)
            agent.TrafficTicket = now;
        if (agent.TrafficGoal is { } previous && !_transform.InRange(previous, destination, 0.5f))
        {
            // A new route leg or investigation must not inherit a pocket for the old
            // approach. Keep entry priority, but choose clearance for the new direction.
            agent.TrafficYieldTo = null;
            agent.TrafficYieldPoint = null;
            agent.TrafficPassExit = null;
            agent.TrafficWaitingSince = null;
            agent.NextTrafficPocket = TimeSpan.Zero;
        }
        agent.TrafficGoal = destination;
        agent.TrafficActiveUntil = now + TimeSpan.FromSeconds(0.6);
        var forward = Vector2.Normalize(delta);
        var end = origin.Position + forward * Math.Min(2, delta.Length());

        var underFire = now - agent.LastHit < TimeSpan.FromSeconds(2) ||
            now < agent.SuppressedUntil || KnownDangerCost(agent, start) > 0;
        if (agent.TrafficWaitingSince is { } waited && now - waited > TimeSpan.FromSeconds(underFire ? 2 : 5))
        {
            // A dead end or stationary body needs a different route, not a perpetual queue.
            if (agent.TrafficYieldTo is { } stuck && Exists(stuck))
                agent.TrafficBlockedPoint = Transform(stuck).Coordinates;
            agent.AvoidTrafficUntil = now + TimeSpan.FromSeconds(4);
            agent.OrderRoute.Clear();
            agent.Route.Clear();
            agent.RouteDestination = null;
            agent.NextOrderRoute = now + TimeSpan.FromSeconds(0.3);
            ClearTraffic(agent);
            agent.TrafficDecision = "queue-timeout-repath";
            _steering.Unregister(uid);
            return false;
        }
        if (agent.TrafficYieldTo is { } held && agent.TrafficYieldPoint is { } pocket &&
            TryComp<CMUExpeditionAgentComponent>(held, out var passing) && _mobs.IsAlive(held) &&
            _transform.InRange(start, Transform(held).Coordinates, 3) &&
            TraversablePassage(uid, start, pocket, allowVault: false) && TrafficPassageClear(uid, start, pocket) &&
            KnownDangerPassage(uid, agent, start, pocket))
        {
            PauseTravelClock(agent, now);
            if (_transform.InRange(start, pocket, 0.12f))
            {
                if (passing.TrafficActiveUntil > now)
                {
                    _steering.Unregister(uid);
                    agent.TrafficDecision = "letting-squadmate-pass";
                    return false;
                }
                if (agent.TrafficPassExit is { } exit && TraversablePassage(uid, start, exit, allowVault: false) &&
                    TrafficPassageClear(uid, start, exit) && KnownDangerPassage(uid, agent, start, exit))
                {
                    // Finish the parallel leg before turning back towards the order route.
                    // Otherwise the diagonal rejoin cuts straight through the firing body.
                    agent.TrafficYieldPoint = exit;
                    agent.TrafficPassExit = null;
                    agent.TrafficWaitingSince = now;
                    destination = exit;
                    agent.TrafficDecision = "passing-stationary-squadmate";
                    return true;
                }
                // A stationary firing line will never pass us. Resume from the free lane.
                agent.TrafficYieldPoint = null;
            }
            else
            {
                destination = pocket;
                agent.TrafficDecision = "moving-to-passing-pocket";
                return true;
            }
        }
        agent.TrafficYieldPoint = null;
        agent.TrafficPassExit = null;
        EntityUid? blocker = null;
        var opposing = false;
        var stationary = false;
        var closest = float.MaxValue;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var other, out var buddy, out var transform))
        {
            if (other == uid || transform.MapID != origin.MapId || !_mobs.IsAlive(other) ||
                HasComp<ActorComponent>(other) || !IsFriendly(uid, other))
                continue;
            var otherStart = _transform.GetWorldPosition(transform);
            var separation = Vector2.Distance(origin.Position, otherStart);
            if (separation > 4)
                continue;
            var otherEnd = otherStart;
            var travelling = buddy.TrafficGoal is { } && buddy.TrafficActiveUntil > now;
            var otherDirection = Vector2.Zero;
            if (travelling)
            {
                var offset = _transform.ToMapCoordinates(buddy.TrafficGoal!.Value).Position - otherStart;
                if (offset.LengthSquared() > 0.01f)
                {
                    otherDirection = Vector2.Normalize(offset);
                    otherEnd += otherDirection * Math.Min(2, offset.Length());
                }
            }
            var ahead = Vector2.Dot(otherStart - origin.Position, forward);
            var sameDirection = travelling && Vector2.Dot(forward, otherDirection) > 0.5f;
            var bodyInFront = ahead > 0 && ahead < 1.25f && SegmentDistance(otherStart, origin.Position, end) < 0.8f;
            // Wide open paths retain native local avoidance. Narrow passages receive entry priority.
            var narrow = !BodyFits(uid, start, 0.85f) ||
                !BodyFits(uid, _transform.ToCoordinates(start.EntityId, new MapCoordinates(end, origin.MapId)), 0.85f);
            var conflict = bodyInFront || !sameDirection && narrow && CorridorsConflict(origin.Position, end, otherStart, otherEnd);
            if (!conflict || sameDirection && !bodyInFront)
                continue;
            var otherFirst = !travelling || buddy.TrafficTicket < agent.TrafficTicket ||
                buddy.TrafficTicket == agent.TrafficTicket && other.CompareTo(uid) < 0;
            if (!bodyInFront && !otherFirst || separation >= closest)
                continue;
            blocker = other;
            closest = separation;
            opposing = travelling && !sameDirection && otherFirst;
            stationary = !travelling;
        }
        if (blocker is not { } waitingFor)
        {
            agent.TrafficYieldTo = null;
            agent.TrafficWaitingSince = null;
            agent.TrafficDecision = "clear";
            return true;
        }
        agent.TrafficYieldTo = waitingFor;
        agent.TrafficWaitingSince ??= now;
        PauseTravelClock(agent, now);
        if (now >= agent.NextTrafficPocket && (opposing || stationary || underFire ||
                now - agent.TrafficWaitingSince.Value >= TimeSpan.FromSeconds(1.2)))
        {
            agent.NextTrafficPocket = now + TimeSpan.FromSeconds(0.6);
            if (FindPassingPocket(uid, agent, start, forward, waitingFor, stationary, out var exit) is { } yield)
            {
                agent.TrafficYieldPoint = yield;
                agent.TrafficPassExit = exit;
                destination = yield;
                agent.TrafficDecision = underFire ? "leaving-exposed-queue" : "moving-to-passing-pocket";
                return true;
            }
            if (stationary && TryRequestTrafficYield(uid, agent, waitingFor, forward, now))
            {
                agent.TrafficDecision = "requesting-squadmate-clearance";
                _steering.Unregister(uid);
                return false;
            }
        }
        agent.TrafficDecision = "queued-behind-squadmate";
        _steering.Unregister(uid);
        return false;
    }

    private static void PauseTravelClock(CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        agent.MoveProgressAt = now;
        // Queueing is bounded above and cannot extend a combat manoeuvre's support deadline.
        agent.MoveUntil += ThinkInterval;
    }

    private EntityCoordinates? FindPassingPocket(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates start, Vector2 forward, EntityUid other, bool stationary, out EntityCoordinates? exit)
    {
        exit = null;
        var localForward = _transform.ToCoordinates(start.EntityId,
            _transform.ToMapCoordinates(start).Offset(forward)).Position - start.Position;
        var otherPoint = _transform.ToCoordinates(start.EntityId, _transform.GetMapCoordinates(other));
        var side = new Vector2(-localForward.Y, localForward.X);
        var exposure = ExposureScore(uid, agent, start);
        EntityCoordinates? best = null;
        var bestScore = float.MinValue;
        foreach (var distance in new[] { 1.05f, 1.4f, 1.75f, 2.5f })
        foreach (var direction in new[] { side, -side, -localForward, Vector2.Normalize(side - localForward),
                     Vector2.Normalize(-side - localForward), Vector2.Normalize(side + localForward), Vector2.Normalize(-side + localForward) })
        {
            var point = start.Offset(direction * distance);
            var candidateExposure = ExposureScore(uid, agent, point);
            // Travel needs body clearance, not the larger spacing between firing positions.
            // The latter rejects the entire second lane of a two-tile corridor.
            if (!TraversablePassage(uid, start, point, allowVault: false) || !TrafficPassageClear(uid, start, point) ||
                GrenadeDanger(point) || !KnownDangerPassage(uid, agent, start, point) ||
                agent.Home is { } home && agent.OrderedDestination == null && !_transform.InRange(home, point, agent.LeashRange) ||
                _transform.InRange(point, Transform(other).Coordinates, 0.8f) || candidateExposure > exposure + 0.5f)
                continue;
            EntityCoordinates? passExit = null;
            if (stationary)
            {
                var advance = Vector2.Dot(otherPoint.Position - point.Position, localForward) + 1.05f;
                var beyond = point.Offset(localForward * Math.Clamp(advance, 0, 2.5f));
                if (TraversablePassage(uid, point, beyond, allowVault: false) && TrafficPassageClear(uid, point, beyond) &&
                    KnownDangerPassage(uid, agent, point, beyond) &&
                    ExposureScore(uid, agent, beyond) <= exposure + 0.5f)
                    passExit = beyond;
            }
            var score = (exposure - candidateExposure) * 3 - distance * 0.5f - KnownDangerCost(agent, point) +
                Vector2.Dot(direction, localForward) * 0.25f + (passExit != null ? 1.5f : 0);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = point;
            exit = passExit;
        }
        return best;
    }

    private bool TrafficPassageClear(EntityUid uid, EntityCoordinates start, EntityCoordinates destination)
    {
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(destination);
        var query = EntityQueryEnumerator<NpcFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var other, out _, out var transform))
        {
            if (other == uid || transform.MapID != from.MapId || !_mobs.IsAlive(other) || !IsFriendly(uid, other))
                continue;
            var point = _transform.GetWorldPosition(transform);
            var clearance = AgentBodyRadius * 2 + 0.1f;
            var initialDistance = Vector2.Distance(point, from.Position);
            if (initialDistance > Vector2.Distance(from.Position, to.Position) + clearance &&
                (!TryComp<CMUExpeditionAgentComponent>(other, out var distant) ||
                    distant.TrafficYieldPoint == null && distant.TrafficNudgeDestination == null))
                continue;
            if (Vector2.Distance(point, to.Position) < clearance ||
                SegmentDistance(point, from.Position, to.Position) < Math.Min(clearance, initialDistance - 0.01f))
                return false;
            if (TryComp<CMUExpeditionAgentComponent>(other, out var buddy) && buddy.TrafficYieldPoint is { } pocket &&
                buddy.TrafficActiveUntil > _timing.CurTime && _transform.InRange(destination, pocket, clearance))
                return false;
            if (buddy?.TrafficNudgeDestination is { } yield && buddy.TrafficNudgeUntil > _timing.CurTime &&
                _transform.InRange(destination, yield, clearance))
                return false;
        }
        return true;
    }

    private static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var amount = delta.LengthSquared() < 0.0001f ? 0 : Math.Clamp(Vector2.Dot(point - from, delta) / delta.LengthSquared(), 0, 1);
        return Vector2.Distance(point, from + amount * delta);
    }

    private static bool CorridorsConflict(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var ab = b - a;
        var cd = d - c;
        var denominator = ab.X * cd.Y - ab.Y * cd.X;
        if (Math.Abs(denominator) > 0.0001f)
        {
            var ca = c - a;
            var t = (ca.X * cd.Y - ca.Y * cd.X) / denominator;
            var u = (ca.X * ab.Y - ca.Y * ab.X) / denominator;
            if (t is >= 0 and <= 1 && u is >= 0 and <= 1)
                return true;
        }
        return Math.Min(Math.Min(SegmentDistance(a, c, d), SegmentDistance(b, c, d)),
            Math.Min(SegmentDistance(c, a, b), SegmentDistance(d, a, b))) < 0.8f;
    }
}
