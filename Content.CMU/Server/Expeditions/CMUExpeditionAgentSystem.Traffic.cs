using System.Numerics;
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
        agent.TrafficWaitingSince = null;
        agent.TrafficDecision = "clear";
    }

    /// <summary>
    /// Reserve a short travel corridor, not the whole route. Queue behind leaders; opposing
    /// traffic uses a stable ticket and a physical passing pocket. Hard body clearance is never relaxed.
    /// </summary>
    private bool QueueMovement(EntityUid uid, CMUExpeditionAgentComponent agent, ref EntityCoordinates destination)
    {
        var now = _timing.CurTime;
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
        agent.TrafficGoal = destination;
        agent.TrafficActiveUntil = now + TimeSpan.FromSeconds(0.6);
        var forward = Vector2.Normalize(delta);
        var end = origin.Position + forward * Math.Min(2, delta.Length());

        if (agent.TrafficWaitingSince is { } waited && now - waited > TimeSpan.FromSeconds(5))
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
            passing.TrafficActiveUntil > now && _transform.InRange(start, Transform(held).Coordinates, 3) &&
            TraversablePassage(uid, start, pocket))
        {
            PauseTravelClock(agent, now);
            if (_transform.InRange(start, pocket, ArrivalRange))
            {
                _steering.Unregister(uid);
                agent.TrafficDecision = "letting-squadmate-pass";
                return false;
            }
            destination = pocket;
            agent.TrafficDecision = "moving-to-passing-pocket";
            return true;
        }
        agent.TrafficYieldPoint = null;
        EntityUid? blocker = null;
        var opposing = false;
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
        if (opposing && FindPassingPocket(uid, agent, start, forward, waitingFor) is { } yield)
        {
            agent.TrafficYieldPoint = yield;
            destination = yield;
            agent.TrafficDecision = "moving-to-passing-pocket";
            return true;
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
        EntityCoordinates start, Vector2 forward, EntityUid other)
    {
        var localForward = _transform.ToCoordinates(start.EntityId,
            _transform.ToMapCoordinates(start).Offset(forward)).Position - start.Position;
        var side = new Vector2(-localForward.Y, localForward.X);
        foreach (var distance in new[] { 1f, 1.75f, 2.5f })
        foreach (var direction in new[] { side, -side, -localForward, Vector2.Normalize(side - localForward), Vector2.Normalize(-side - localForward) })
        {
            var point = start.Offset(direction * distance);
            if (Reserved(uid, point) || !TraversablePassage(uid, start, point) || GrenadeDanger(point) ||
                !_transform.InRange(start, point, 2.6f) || _transform.InRange(point, Transform(other).Coordinates, 1.2f) ||
                ExposureScore(uid, agent, point) > ExposureScore(uid, agent, start) + 0.5f)
                continue;
            return point;
        }
        return null;
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
