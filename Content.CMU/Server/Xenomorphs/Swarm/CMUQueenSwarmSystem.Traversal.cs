using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.NPC.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem
{
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private readonly List<CMUSwarmStairRoute> _stairRoutes = new();
    private TimeSpan _nextStairScan;

    private bool TraverseToAnchor(EntityUid member, EntityUid anchor)
    {
        var map = Transform(member).MapUid;
        var targetMap = Transform(anchor).MapUid;
        TryComp<CMUSwarmTraversalComponent>(member, out var traversal);
        if (map == targetMap && traversal?.Route == null)
            return false;

        RemComp<NPCMeleeCombatComponent>(member);
        if (map is not { } from || targetMap is not { } to || !_zLevels.IsSameZNetwork(from, to))
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
            if (!TryComp<CMUZLevelMapComponent>(from, out var start) || !TryComp<CMUZLevelMapComponent>(to, out var end))
                return true;
            var best = float.MaxValue;
            foreach (var route in _stairRoutes)
            {
                if (route.FromMap != from || TerminatingOrDeleted(route.Stair) ||
                    traversal.FailedStairs.GetValueOrDefault(route.Stair) > _timing.CurTime ||
                    !TryComp<CMUZLevelMapComponent>(route.ToMap, out var next) ||
                    !_zLevels.IsSameZNetwork(route.ToMap, to) ||
                    Math.Abs((long) next.Depth - end.Depth) >= Math.Abs((long) start.Depth - end.Depth))
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

        if (!traversal.Crossing && (from == selected.ToMap || Vector2.Distance(position, selected.Approach) < 0.3f))
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

    private void ClearTraversal(EntityUid member, CMUSwarmTraversalComponent traversal)
    {
        traversal.Route = null;
        traversal.Crossing = false;
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
    }
}
