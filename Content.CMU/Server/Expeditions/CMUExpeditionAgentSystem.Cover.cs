using System.Numerics;
using System.Diagnostics;
using System.Linq;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static readonly (int X, int Y)[] Neighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    [Dependency] private SharedPhysicsSystem _physics = default!;
    private readonly Dictionary<EntityCoordinates, bool> _bodyClearCache = new();

    private bool BodyFits(EntityUid uid, EntityCoordinates point)
    {
        if (_bodyClearCache.TryGetValue(point, out var clear))
            return clear;
        var location = _transform.ToMapCoordinates(point);
        // Rays starting inside a wall do not report an entry hit. Test the body footprint instead.
        var bounds = new Box2Rotated(Box2.CenteredAround(location.Position, new Vector2(0.58f)), Angle.Zero);
        var fixtures = new HashSet<FixtureProxy>();
        _lookup.GetFixturesIntersecting(location.MapId, bounds, fixtures, new FixtureQueryArgs(new QueryFilter
        {
            LayerBits = 0,
            MaskBits = (long) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable),
            Flags = QueryFlags.Dynamic | QueryFlags.Static,
        }));
        clear = !fixtures.Any(fixture => fixture.Entity != uid && !HasComp<NpcFactionMemberComponent>(fixture.Entity));
        _bodyClearCache[point] = clear;
        return clear;
    }

    private bool RayClear(EntityUid uid, MapCoordinates from, MapCoordinates to)
    {
        if (from.MapId != to.MapId)
            return false;
        var delta = to.Position - from.Position;
        if (delta.LengthSquared() < 0.0001f)
            return true;
        var ray = new CollisionRay(from.Position, Vector2.Normalize(delta),
            (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable | CollisionGroup.BulletImpassable));
        // Only the existence of an obstruction matters. Do not collect every hit behind the first wall.
        return !_physics.IntersectRayWithPredicate(from.MapId, ray, delta.Length(),
            entity => entity == uid || HasComp<NpcFactionMemberComponent>(entity), true).Any();
    }

    private bool FiringLaneClear(EntityUid uid, EntityCoordinates from, EntityCoordinates to)
    {
        if (!_guns.TryGetGun(uid, out var gun))
            return false;
        var distance = Vector2.Distance(_transform.ToMapCoordinates(from).Position, _transform.ToMapCoordinates(to).Position);
        var width = 0.3f + (float) Math.Tan(Math.Min(gun.Comp.MaxAngleModified.Theta, Math.PI / 2) / 2) * distance;
        return ClearLane(uid, from, to, width);
    }

    /// <summary>Three rays leave room for the body's width and the weapon's scatter around a corner.</summary>
    private bool ClearLane(EntityUid uid, EntityCoordinates from, EntityCoordinates to, float endWidth)
    {
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        var delta = end.Position - start.Position;
        if (start.MapId != end.MapId || delta.LengthSquared() < 0.01f)
            return false;
        var perpendicular = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        return RayClear(uid, start, end) &&
               RayClear(uid, new MapCoordinates(start.Position + perpendicular * 0.3f, start.MapId),
                   new MapCoordinates(end.Position + perpendicular * endWidth, end.MapId)) &&
               RayClear(uid, new MapCoordinates(start.Position - perpendicular * 0.3f, start.MapId),
                   new MapCoordinates(end.Position - perpendicular * endWidth, end.MapId));
    }

    private bool Sheltered(EntityUid uid, EntityCoordinates location, EntityCoordinates threat)
    {
        var start = _transform.ToMapCoordinates(location);
        var end = _transform.ToMapCoordinates(threat);
        var delta = end.Position - start.Position;
        if (start.MapId != end.MapId || delta.LengthSquared() < 0.01f)
            return false;
        var perpendicular = Vector2.Normalize(new Vector2(-delta.Y, delta.X)) * 0.35f;
        return !RayClear(uid, start, end) &&
               !RayClear(uid, new MapCoordinates(start.Position + perpendicular, start.MapId), end) &&
               !RayClear(uid, new MapCoordinates(start.Position - perpendicular, start.MapId), end);
    }

    private bool ShelteredFromKnownThreats(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates location)
    {
        if (agent.LastSeen is { } threat && _timing.CurTime < agent.ForgetAt && !Sheltered(uid, location, threat))
            return false;
        foreach (var visible in agent.VisibleThreats)
        {
            if (agent.LastSeen is { } known && _transform.InRange(known, visible, 0.1f))
                continue;
            if (!Sheltered(uid, location, visible))
                return false;
        }
        return true;
    }

    private (EntityCoordinates Anchor, EntityCoordinates? Peek)? FindPosition(EntityUid uid,
        CMUExpeditionAgentComponent agent, TransformComponent transform, bool retreat)
    {
        if (agent.LastSeen is not { } threat || transform.GridUid is not { } grid ||
            !TryComp<CMUExpeditionMapComponent>(grid, out var expedition))
            return null;
        var plan = expedition.Plan;
        var started = Stopwatch.GetTimestamp();
        var origin = _transform.GetGridOrMapTilePosition(uid, transform);
        var pending = new Queue<(int X, int Y, int Steps)>();
        var visited = new HashSet<int>();
        var candidates = new List<(EntityCoordinates Position, int Steps)>();
        pending.Enqueue((origin.X, origin.Y, 0));
        // Fixed work bound per search; native steering handles live pathfinding and failure.
        while (pending.TryDequeue(out var point) && visited.Count < 256)
        {
            if (point.X < 1 || point.Y < 1 || point.X >= plan.Size - 1 || point.Y >= plan.Size - 1 || point.Steps > 8)
                continue;
            var index = plan.Index(point.X, point.Y);
            if (!visited.Add(index) ||
                plan.Terrain[index] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                continue;
            var safe = true;
            foreach (var fire in plan.FirePockets)
                safe &= Math.Abs(point.X - fire.X) > 3 || Math.Abs(point.Y - fire.Y) > 3;
            if (!safe)
                continue;
            var coordinates = new EntityCoordinates(grid, new Vector2(point.X + 0.5f, point.Y + 0.5f));
            if (GrenadeDanger(coordinates) || !BodyFits(uid, coordinates))
                continue;
            if (agent.Home is not { } home || !_transform.InRange(home, coordinates, agent.LeashRange))
                continue;
            candidates.Add((coordinates, point.Steps));
            foreach (var (dx, dy) in Neighbors)
                pending.Enqueue((point.X + dx, point.Y + dy, point.Steps + 1));
        }

        var anchors = new List<(EntityCoordinates Position, int Steps)>();
        var peeks = new List<(EntityCoordinates Position, float Exposure)>();
        var threatPosition = _transform.ToMapCoordinates(threat).Position;
        foreach (var candidate in candidates)
        {
            if (Reserved(uid, candidate.Position) || agent.FailedPosition is { } failed &&
                _timing.CurTime < agent.AvoidPositionUntil && _transform.InRange(candidate.Position, failed, 1.4f))
                continue;
            if (ShelteredFromKnownThreats(uid, agent, candidate.Position))
            {
                // Breadth-first candidates are ordered by travel distance; the first shelter is the shortest retreat.
                if (retreat)
                {
                    SearchMetrics(agent, visited.Count, started);
                    return (candidate.Position, null);
                }
                anchors.Add(candidate);
            }
            else if (!retreat)
            {
                var distance = Vector2.Distance(_transform.ToMapCoordinates(candidate.Position).Position, threatPosition);
                // Leave room for the target's movement and the body's sub-tile arrival offset.
                if (distance >= agent.MinimumFireRange && distance <= agent.FireRange - 0.75f &&
                    FiringLaneClear(uid, candidate.Position, threat))
                {
                    var exposure = 0f;
                    foreach (var otherThreat in agent.VisibleThreats)
                    {
                        if (!_transform.InRange(otherThreat, threat, 1) && !Sheltered(uid, candidate.Position, otherThreat))
                            exposure += 3;
                    }
                    peeks.Add((candidate.Position, exposure));
                }
            }
        }
        (EntityCoordinates Anchor, EntityCoordinates? Peek)? best = null;
        var bestScore = float.MinValue;
        foreach (var anchor in anchors)
        {
            foreach (var (peek, exposure) in peeks)
            {
                var stepOut = Vector2.Distance(anchor.Position.Position, peek.Position);
                if (stepOut < 0.9f || stepOut > 3.2f)
                    continue;
                var range = Vector2.Distance(_transform.ToMapCoordinates(peek).Position, threatPosition);
                var preferredRange = agent.PreferredFireRange + agent.Stress * 2;
                var score = -anchor.Steps * 0.6f - stepOut - Math.Abs(range - preferredRange) * 0.5f - exposure;
                // Reject inferior pairs before their expensive corridor casts; exposure is cached once per peek.
                if (score <= bestScore || !DryPassage(uid, plan, anchor.Position, peek) || !ClearLane(uid, anchor.Position, peek, 0.35f))
                    continue;
                bestScore = score;
                best = (anchor.Position, peek);
            }
        }
        SearchMetrics(agent, visited.Count, started);
        return best;
    }

    private static void SearchMetrics(CMUExpeditionAgentComponent agent, int cells, long started)
    {
        agent.LastSearchCells = cells;
        agent.LastSearchMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        agent.Searches++;
        agent.TotalSearchMilliseconds += agent.LastSearchMilliseconds;
        agent.MaxSearchMilliseconds = Math.Max(agent.MaxSearchMilliseconds, agent.LastSearchMilliseconds);
    }

    private bool DryPassage(EntityUid uid, CMUExpeditionPlan plan, EntityCoordinates from, EntityCoordinates to)
    {
        for (var step = 0; step <= 16; step++)
        {
            var position = Vector2.Lerp(from.Position, to.Position, step / 16f);
            if (position.X < 1 || position.Y < 1 || position.X >= plan.Size - 1 || position.Y >= plan.Size - 1)
                return false;
            var index = plan.Index((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
            if (plan.Terrain[index] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff ||
                plan.Props[index] != CMUExpeditionProp.None && !BodyFits(uid, new EntityCoordinates(from.EntityId, position)))
                return false;
        }
        return true;
    }


    private bool TryAdjustPeek(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates threat, TimeSpan now)
    {
        if (now < agent.NextPeekAdjustment)
            return false;
        agent.NextPeekAdjustment = now + TimeSpan.FromSeconds(0.8);
        var start = Transform(uid).Coordinates;
        if (!TryComp<CMUExpeditionMapComponent>(start.EntityId, out var expedition))
            return false;
        var localThreat = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(threat));
        var delta = localThreat.Position - start.Position;
        if (delta.LengthSquared() < 0.01f)
            return false;
        var side = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        foreach (var distance in new[] { 0.4f, -0.4f, 0.75f, -0.75f, 1.1f, -1.1f })
        {
            var candidate = start.Offset(side * distance);
            if (agent.Home is not { } home || !_transform.InRange(candidate, home, agent.LeashRange) ||
                agent.CoverAnchor is { } anchor && !_transform.InRange(candidate, anchor, 3.6f) ||
                Reserved(uid, candidate) || !DryPassage(uid, expedition.Plan, start, candidate))
                continue;
            var safe = true;
            foreach (var fire in expedition.Plan.FirePockets)
                safe &= Math.Abs(candidate.X - fire.X - 0.5f) > 3 || Math.Abs(candidate.Y - fire.Y - 0.5f) > 3;
            if (!safe || !BodyFits(uid, candidate) || !ClearLane(uid, start, candidate, 0.3f) ||
                !_guns.TryGetGun(uid, out var gun) || !SafeShot(uid, agent, gun, threat, candidate))
                continue;
            agent.PeekPosition = candidate;
            BeginMove(uid, agent, candidate, CMUExpeditionAgentState.Peeking, now);
            return true;
        }
        return false;
    }

    private bool Reserved(EntityUid uid, EntityCoordinates coordinates)
    {
        var owner = Comp<CMUExpeditionAgentComponent>(uid);
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var agent))
        {
            if (other == uid || agent.State == CMUExpeditionAgentState.Disabled || !IsFriendly(uid, other) ||
                owner.Squad != agent.Squad || Transform(uid).MapID != Transform(other).MapID)
                continue;
            if (agent.CoverAnchor is { } anchor && _transform.InRange(coordinates, anchor, 0.9f) ||
                agent.PeekPosition is { } peek && _transform.InRange(coordinates, peek, 0.9f) ||
                agent.CoverDestination is { } destination && _transform.InRange(coordinates, destination, 0.9f))
                return true;
        }
        return false;
    }
}
