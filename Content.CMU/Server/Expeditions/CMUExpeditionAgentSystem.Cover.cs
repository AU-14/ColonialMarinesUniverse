using System.Numerics;
using System.Diagnostics;
using System.Linq;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static readonly (int X, int Y)[] Neighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    // RMC humans have a 0.35 m hard body fixture. Planned routes leave extra turning room.
    private const float AgentBodyRadius = 0.35f;
    private const float RouteClearance = 0.4f;
    private const CollisionGroup MovementMask = CollisionGroup.MobMask | CollisionGroup.InteractImpassable |
        CollisionGroup.BarricadeImpassable | CollisionGroup.BarbedBarricade;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    private readonly Dictionary<(EntityCoordinates Point, float Radius), bool> _bodyClearCache = new();

    private bool BodyFits(EntityUid uid, EntityCoordinates point, float radius = AgentBodyRadius)
    {
        if (_bodyClearCache.TryGetValue((point, radius), out var clear))
            return clear;
        var location = _transform.ToMapCoordinates(point);
        // Rays starting inside a wall do not report an entry hit. Test the body footprint instead.
        // Ignore polygon skin so ordinary physics contact does not invalidate a stance beside a wall.
        var fixtures = new HashSet<FixtureProxy>();
        _lookup.GetFixturesIntersecting(location.MapId, new PhysShapeCircle(radius),
            new Robust.Shared.Physics.Transform(location.Position, Angle.Zero), fixtures, new FixtureQueryArgs(new QueryFilter
        {
            LayerBits = 0,
            MaskBits = (long) MovementMask,
            Flags = QueryFlags.Dynamic | QueryFlags.Static,
        }, IgnoreShapeSkin: true));
        clear = !fixtures.Any(fixture => fixture.Fixture.Hard && fixture.Body.CanCollide &&
            fixture.Entity != uid && !HasComp<NpcFactionMemberComponent>(fixture.Entity));
        _bodyClearCache[(point, radius)] = clear;
        return clear;
    }

    private bool RayClear(EntityUid uid, MapCoordinates from, MapCoordinates to, bool movement = false)
    {
        if (from.MapId != to.MapId)
            return false;
        var delta = to.Position - from.Position;
        if (delta.LengthSquared() < 0.0001f)
            return true;
        var mask = CollisionGroup.Impassable | CollisionGroup.InteractImpassable;
        if (!movement)
            mask |= CollisionGroup.BulletImpassable;
        var ray = new CollisionRay(from.Position, Vector2.Normalize(delta), (int) mask);
        // Only the existence of an obstruction matters. Do not collect every hit behind the first wall.
        return !_physics.IntersectRayWithPredicate(from.MapId, ray, delta.Length(),
            entity => entity == uid || HasComp<NpcFactionMemberComponent>(entity), true).Any();
    }

    private bool FiringLaneClear(EntityUid uid, EntityCoordinates from, EntityCoordinates to)
    {
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        var delta = end.Position - start.Position;
        if (start.MapId != end.MapId || delta.LengthSquared() < 0.01f || !BodyFits(uid, from) || !RayClear(uid, start, end))
            return false;
        // Clear the body and muzzle around nearby corners, then require a direct line to the
        // target. Trees beside a distant target may catch stray rounds without blocking the shot.
        var muzzleEnd = _transform.ToCoordinates(from.EntityId,
            new MapCoordinates(start.Position + Vector2.Normalize(delta) * Math.Min(1.25f, delta.Length()), start.MapId));
        return ClearLane(uid, from, muzzleEnd, 0.3f);
    }

    /// <summary>Three rays leave room for the body's width and the weapon's scatter around a corner.</summary>
    private bool ClearLane(EntityUid uid, EntityCoordinates from, EntityCoordinates to, float endWidth, bool movement = false)
    {
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        var delta = end.Position - start.Position;
        if (start.MapId != end.MapId || delta.LengthSquared() < 0.01f)
            return false;
        var perpendicular = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        return RayClear(uid, start, end, movement) &&
               RayClear(uid, new MapCoordinates(start.Position + perpendicular * 0.3f, start.MapId),
                   new MapCoordinates(end.Position + perpendicular * endWidth, end.MapId), movement) &&
               RayClear(uid, new MapCoordinates(start.Position - perpendicular * 0.3f, start.MapId),
                   new MapCoordinates(end.Position - perpendicular * endWidth, end.MapId), movement);
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

    private void ValidateCover(EntityUid uid, CMUExpeditionAgentComponent agent, bool hit, TimeSpan now)
    {
        if (agent.CoverAnchor is not { } anchor)
            return;
        var atShelter = _transform.InRange(Transform(uid).Coordinates, anchor, 0.7f);
        // A hit while supposedly hidden invalidates even cover our geometry considers solid
        // (penetrable scenery, changed firing angles, or an attacker not yet observed).
        var hitWhileHidden = hit && atShelter && agent.State is
            CMUExpeditionAgentState.Recover or CMUExpeditionAgentState.Healing or CMUExpeditionAgentState.Retreat;
        if (!hitWhileHidden && ShelteredFromKnownThreats(uid, agent, anchor) &&
            (!atShelter || agent.State != CMUExpeditionAgentState.Recover ||
                ShelteredFromKnownThreats(uid, agent, Transform(uid).Coordinates)))
            return;
        agent.FailedPosition = anchor;
        agent.RejectedCover++;
        agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(8);
        ClearCover(agent);
        // Give an exposed rifleman a chance to return fire before searching for another shelter.
        agent.NextReposition = now + agent.BurstDuration;
        agent.NextSuppressionResponse = now + agent.BurstDuration;
        if (agent.Action == null && agent.State is CMUExpeditionAgentState.Reposition or
            CMUExpeditionAgentState.Withdraw or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Retreat)
        {
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Guard;
        }
    }

    private (EntityCoordinates Anchor, EntityCoordinates? Peek)? FindPosition(EntityUid uid,
        CMUExpeditionAgentComponent agent, TransformComponent transform, bool retreat)
    {
        if (agent.LastSeen is not { } threat || transform.GridUid is not { } grid)
            return null;
        var started = Stopwatch.GetTimestamp();
        var origin = _transform.GetGridOrMapTilePosition(uid, transform);
        var pending = new Queue<(int X, int Y, int Steps)>();
        var visited = new HashSet<Vector2i>();
        var candidates = new List<(EntityCoordinates Position, int Steps)>();
        pending.Enqueue((origin.X, origin.Y, 0));
        // Fixed work bound per search; native steering handles live pathfinding and failure.
        while (pending.TryDequeue(out var point) && visited.Count < 256)
        {
            if (point.Steps > 8 || !visited.Add(new Vector2i(point.X, point.Y)))
                continue;
            var coordinates = new EntityCoordinates(grid, new Vector2(point.X + 0.5f, point.Y + 0.5f));
            if (!ValidOrderPoint(uid, coordinates))
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
                    // Score a bounded set of attack bearings. Shelter eligibility above
                    // still checks every visible threat; a cheap score never certifies safety.
                    var exposure = ExposureScore(uid, agent, candidate.Position) * 2;
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
                if (score <= bestScore || !TraversablePassage(uid, anchor.Position, peek) || !ClearLane(uid, anchor.Position, peek, 0.35f, movement: true))
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

    private bool TraversablePassage(EntityUid uid, EntityCoordinates from, EntityCoordinates to, float radius = AgentBodyRadius)
    {
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        if (start.MapId != end.MapId || !BodyFits(uid, from, radius) || !BodyFits(uid, to, radius))
            return false;
        to = _transform.ToCoordinates(from.EntityId, end);
        var steps = Math.Max(1, (int) MathF.Ceiling(Vector2.Distance(start.Position, end.Position) * 4));
        for (var step = 0; step <= steps; step++)
        {
            var position = Vector2.Lerp(from.Position, to.Position, step / (float) steps);
            if (!GroundSafe(new EntityCoordinates(from.EntityId, position)))
                return false;
        }
        var translation = end.Position - start.Position;
        if (translation.LengthSquared() < 0.0001f)
            return true;
        // The rectangle between the two endpoint circles covers the entire swept body,
        // including diagonal corner grazes between terrain samples.
        var center = (start.Position + end.Position) / 2;
        var bounds = new Box2Rotated(Box2.CenteredAround(center, new Vector2(translation.Length(), radius * 2)),
            translation.ToAngle(), center);
        var fixtures = new HashSet<FixtureProxy>();
        _lookup.GetFixturesIntersecting(start.MapId, bounds, fixtures, new FixtureQueryArgs(new QueryFilter
        {
            MaskBits = (long) MovementMask,
            IsIgnored = entity => entity == uid || HasComp<NpcFactionMemberComponent>(entity),
        }, IgnoreShapeSkin: true));
        // Sensors (including RMC water) do not become static route walls.
        return !fixtures.Any(fixture => fixture.Fixture.Hard && fixture.Body.CanCollide);
    }


    private bool TryAdjustPeek(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates threat, TimeSpan now)
    {
        if (now < agent.NextPeekAdjustment)
            return false;
        agent.NextPeekAdjustment = now + TimeSpan.FromSeconds(1.2);
        var start = Transform(uid).Coordinates;
        var localThreat = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(threat));
        var delta = localThreat.Position - start.Position;
        if (delta.LengthSquared() < 0.01f)
            return false;
        var side = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        var currentExposure = ExposureScore(uid, agent, start);
        foreach (var distance in new[] { 0.75f, -0.75f, 1.25f, -1.25f, 2f, -2f })
        {
            var candidate = start.Offset(side * distance);
            if (agent.Home is not { } home || !_transform.InRange(candidate, home, agent.LeashRange) ||
                agent.CoverAnchor is { } anchor && !_transform.InRange(candidate, anchor, 3.6f) ||
                agent.FailedPosition is { } failed && now < agent.AvoidPositionUntil && _transform.InRange(candidate, failed, 0.6f) ||
                Reserved(uid, candidate) || !TraversablePassage(uid, start, candidate))
                continue;
            if (!ClearLane(uid, start, candidate, 0.3f, movement: true) ||
                agent.Crossfire && ExposureScore(uid, agent, candidate) > currentExposure + 0.5f ||
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
            if (other == uid || !_mobs.IsAlive(other) || !IsFriendly(uid, other) ||
                Transform(uid).MapID != Transform(other).MapID)
                continue;
            // Bodies need personal space even across squads. Future-position reservations
            // remain squad-local so unrelated squads cannot reserve each other's whole area.
            if (_transform.InRange(coordinates, Transform(other).Coordinates, 1.3f))
                return true;
            if (owner.Squad != agent.Squad || agent.State is CMUExpeditionAgentState.Disabled or CMUExpeditionAgentState.Incapacitated)
                continue;
            if (agent.CoverAnchor is { } anchor && _transform.InRange(coordinates, anchor, 1.6f) ||
                agent.PeekPosition is { } peek && _transform.InRange(coordinates, peek, 1.6f) ||
                agent.InvestigationDestination is { } support && _transform.InRange(coordinates, support, 1.5f) ||
                agent.CoverDestination is { } destination && _transform.InRange(coordinates, destination, 1.5f) ||
                agent.SpacingDestination is { } escape && _transform.InRange(coordinates, escape, 1.5f))
                return true;
        }
        return false;
    }
}
