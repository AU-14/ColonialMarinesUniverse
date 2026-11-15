using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Ladder;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem
{
    [Dependency] private ITileDefinitionManager _traversalTiles = default!;
    [Dependency] private CMUZLevelLadderSystem _zLadders = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private readonly List<CMUSwarmStairRoute> _stairRoutes = new();
    private TimeSpan _nextStairScan;

    private bool TraverseToAnchor(EntityUid member, EntityUid anchor)
    {
        var map = Transform(member).MapUid;
        var targetMap = Transform(anchor).MapUid;
        TryComp<CMUSwarmTraversalComponent>(member, out var traversal);
        if (map == targetMap && traversal?.Route == null)
            return TryCrossGridToAnchor(member, anchor);

        RemComp<NPCMeleeCombatComponent>(member);
        if (map is not { } from || targetMap is not { } to)
        {
            _steering.Unregister(member);
            return true;
        }

        traversal ??= EnsureComp<CMUSwarmTraversalComponent>(member);
        var position = _transform.GetWorldPosition(member);
        if (traversal.Route is { } active)
        {
            if (TerminatingOrDeleted(active.Stair) || (from != active.FromMap && from != active.ToMap))
                ClearTraversal(member, traversal);
            else if (from == active.ToMap && Vector2.Distance(position, active.Exit) < 0.3f)
            {
                ClearTraversal(member, traversal);
                return from != to;
            }
            else
            {
                if (Vector2.DistanceSquared(position, traversal.LastPosition) > 0.25f)
                {
                    traversal.LastPosition = position;
                    traversal.ProgressDeadline = _timing.CurTime + TimeSpan.FromSeconds(8);
                }

                if (_timing.CurTime > traversal.ProgressDeadline ||
                    (!traversal.Crossing && TryComp<NPCSteeringComponent>(member, out var failed) && failed.Status == SteeringStatus.NoPath))
                {
                    traversal.FailedStairs[active.Stair] = _timing.CurTime + TimeSpan.FromSeconds(15);
                    ClearTraversal(member, traversal);
                }
            }
        }

        if (traversal.Route == null)
        {
            if (from == to)
                return false;
            RefreshStairRoutes();
            var best = float.MaxValue;
            foreach (var route in _stairRoutes)
            {
                if (route.FromMap != from || TerminatingOrDeleted(route.Stair)
                    || traversal.FailedStairs.GetValueOrDefault(route.Stair) > _timing.CurTime
                    || !RouteApproachesMap(from, route.ToMap, to))
                    continue;
                var score = Vector2.Distance(position, route.Approach) + Vector2.Distance(route.Exit, _transform.GetWorldPosition(anchor));
                if (score >= best)
                    continue;
                traversal.Route = route;
                best = score;
            }
            traversal.LastPosition = position;
            traversal.ProgressDeadline = _timing.CurTime + TimeSpan.FromSeconds(8);
        }

        if (traversal.Route is not { } selected)
        {
            _steering.Unregister(member);
            return true;
        }

        if (selected.LadderOffset is { } ladderOffset)
        {
            MoveTo(member, Transform(selected.Stair).Coordinates, 0.7f);
            if (Vector2.Distance(position, selected.Approach) <= 0.8f
                && _interaction.InRangeUnobstructed(member, selected.Stair))
            {
                _steering.Unregister(member);
                if (_timing.CurTime >= traversal.NextUse)
                {
                    if (TryComp<CMUZLevelLadderComponent>(selected.Stair, out var ladder))
                    {
                        var allowed = ladderOffset > 0 ? ladder.CanMoveUp : ladder.CanMoveDown;
                        if (allowed)
                            _zLadders.StartClimb((selected.Stair, ladder), member, ladderOffset);
                        traversal.NextUse = _timing.CurTime + ladder.Delay + TimeSpan.FromSeconds(0.25);
                    }
                    else if (TryComp<LadderComponent>(selected.Stair, out var linked))
                    {
                        _interaction.InteractionActivate(member, selected.Stair);
                        traversal.NextUse = _timing.CurTime + linked.Delay + TimeSpan.FromSeconds(0.25);
                    }
                }
            }
            return true;
        }

        if (!traversal.Crossing && (from == selected.ToMap || Vector2.Distance(position, selected.Approach) < 0.3f ||
                                    OnStairLane(position, selected)))
        {
            traversal.Crossing = true;
            _steering.Unregister(member);
        }

        // Crossing changes maps between swarm updates. Steering marks its old map's
        // destination NoPath and will not resume just because Register changes the target.
        if (traversal.Crossing && TryComp<NPCSteeringComponent>(member, out var previous) &&
            previous.Status == SteeringStatus.NoPath)
            _steering.Unregister(member);

        // Navigate normally to the entrance. Only the short stair crossing bypasses the
        // 2D navmesh: its upper shaft is empty space, supported by the ramp below.
        // Movement and Z physics perform the transition; no teleport is used here.
        var destination = traversal.Crossing ? selected.Exit : selected.Approach;
        var xform = Transform(member);
        var steering = _steering.Register(member, _transform.ToCoordinates(xform.GridUid ?? from,
            new MapCoordinates(destination, xform.MapID)));
        ConfigureSteering(steering);
        steering.Range = 0.15f;
        if (traversal.Crossing)
        {
            steering.CurrentPath.Clear();
            steering.RepathRange = Vector2.Distance(position, destination) + 1;
        }
        return true;
    }

    private bool RouteApproachesMap(EntityUid from, EntityUid nextMap, EntityUid target)
    {
        if (nextMap == target)
            return true;
        return _zLevels.IsSameZNetwork(from, target) && _zLevels.IsSameZNetwork(nextMap, target)
            && TryComp<CMUZLevelMapComponent>(from, out var start)
            && TryComp<CMUZLevelMapComponent>(nextMap, out var next)
            && TryComp<CMUZLevelMapComponent>(target, out var end)
            && Math.Abs((long) next.Depth - end.Depth) < Math.Abs((long) start.Depth - end.Depth);
    }

    private bool TryCrossGridToAnchor(EntityUid member, EntityUid anchor)
    {
        var xform = Transform(member);
        var target = Transform(anchor);
        if (xform.GridUid == target.GridUid || target.GridUid == null || xform.MapUid == null)
            return false;

        var from = _transform.GetMapCoordinates(member);
        var to = _transform.GetMapCoordinates(anchor);
        if (!_interaction.InRangeUnobstructed(member, anchor, range: 0f))
            return false;

        // Landed ships and terrain have separate navmeshes. Only bypass that seam along
        // an unobstructed, continuously floored path; flight and open hull gaps stay impassable.
        var distance = Vector2.Distance(from.Position, to.Position);
        var steps = Math.Max(1, (int) MathF.Ceiling(distance * 2));
        for (var i = 0; i <= steps; i++)
        {
            var point = Vector2.Lerp(from.Position, to.Position, (float) i / steps);
            if (!_map.TryFindGridAt(new MapCoordinates(point, from.MapId), out var gridUid, out var grid)
                || !_map.TryGetTileRef(gridUid, grid, _map.WorldToTile(gridUid, grid, point), out var tile)
                || CMUZLevelOpeningCache.IsFallThroughTile(tile.Tile, _traversalTiles))
                return false;
        }

        if (TryComp<NPCSteeringComponent>(member, out var previous) && previous.Status == SteeringStatus.NoPath)
            _steering.Unregister(member);
        var steering = _steering.Register(member, new EntityCoordinates(anchor, Vector2.Zero));
        ConfigureSteering(steering);
        steering.Range = 0.8f;
        steering.CurrentPath.Clear();
        steering.RepathRange = distance + 1;
        return true;
    }

    // Already standing on the ramp, the navmesh has no path back down to the approach.
    private static bool OnStairLane(Vector2 position, CMUSwarmStairRoute route)
    {
        var lane = route.Exit - route.Approach;
        var along = Vector2.Dot(position - route.Approach, lane) / lane.LengthSquared();
        if (along <= 0f || along >= 1f)
            return false;
        return Vector2.Distance(position, route.Approach + lane * along) < 0.5f;
    }

    private void ClearTraversal(EntityUid member, CMUSwarmTraversalComponent traversal)
    {
        traversal.Route = null;
        traversal.Crossing = false;
        traversal.NextUse = TimeSpan.Zero;
        _steering.Unregister(member);
    }

    private void RefreshStairRoutes()
    {
        if (_nextStairScan > _timing.CurTime)
            return;
        _nextStairScan = _timing.CurTime + TimeSpan.FromSeconds(1);
        _stairRoutes.Clear();

        var ramps = EntityQueryEnumerator<CMUZLevelHighGroundComponent, TransformComponent>();
        while (ramps.MoveNext(out var uid, out var ramp, out var xform))
        {
            if (!xform.Anchored || !ramp.Stick || ramp.SupportOnlyFromAbove || ramp.HeightCurve.Count < 2 ||
                xform.MapUid is not { } lower || !_zLevels.TryMapUp(lower, out var upper))
                continue;
            var first = ramp.HeightCurve[0];
            var last = ramp.HeightCurve[^1];
            if (Math.Max(first, last) < 0.95f || Math.Abs(first - last) < 0.01f)
                continue;
            var direction = _transform.GetWorldRotation(uid).GetCardinalDir();
            var axis = ramp.Corner
                ? direction switch
                {
                    Direction.North => new Vector2(1, 1),
                    Direction.South => new Vector2(-1, -1),
                    Direction.East => new Vector2(1, -1),
                    _ => new Vector2(-1, 1),
                }
                : direction.ToVec();
            var uphill = first > last ? -axis : axis;
            var center = _transform.GetWorldPosition(uid);
            // Two-piece ramps need their lower half included in the approach.
            var bottom = center - uphill * (Math.Min(first, last) > 0.5f ? 2 : 1);
            var top = center + uphill;
            _stairRoutes.Add(new(uid, lower, upper.Value.Owner, bottom, top));
            _stairRoutes.Add(new(uid, upper.Value.Owner, lower, top, bottom));
        }

        var stairs = EntityQueryEnumerator<CMUZLevelStairsComponent, TransformComponent>();
        while (stairs.MoveNext(out var uid, out var stair, out var xform))
        {
            if (!xform.Anchored || xform.MapUid is not { } from ||
                !_zLevels.TryMapOffset(from, stair.Offset, out var to))
                continue;
            var delta = stair.Direction.ToVec() * (stair.Offset >= 0 ? 1 : -1);
            var center = _transform.GetWorldPosition(uid);
            _stairRoutes.Add(new(uid, from, to.Value.Owner, center - delta, center + delta));
        }

        var ladders = EntityQueryEnumerator<CMUZLevelLadderComponent, TransformComponent>();
        while (ladders.MoveNext(out var uid, out var ladder, out var xform))
        {
            if (!xform.Anchored || xform.MapUid is not { } from)
                continue;
            var magnitude = Math.Max(Math.Abs(ladder.Offset), 1);
            var center = _transform.GetWorldPosition(uid);
            if (ladder.CanMoveUp && _zLevels.TryMapOffset(from, magnitude, out var above))
                _stairRoutes.Add(new(uid, from, above.Value.Owner, center, center, magnitude));
            if (ladder.CanMoveDown && _zLevels.TryMapOffset(from, -magnitude, out var below))
                _stairRoutes.Add(new(uid, from, below.Value.Owner, center, center, -magnitude));
        }

        var linkedLadders = EntityQueryEnumerator<LadderComponent, TransformComponent>();
        while (linkedLadders.MoveNext(out var uid, out var ladder, out var xform))
        {
            if (xform.MapUid is not { } from || ladder.Other is not { } other
                || TerminatingOrDeleted(other) || Transform(other).MapUid is not { } to)
                continue;
            _stairRoutes.Add(new(uid, from, to, _transform.GetWorldPosition(uid), _transform.GetWorldPosition(other), 0));
        }
    }
}
