// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.CMU14.Power;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Power;

// The cables that go up and down. A shaft joins its node to the nodes of the same network that stand in the same place on the level above and the level below
// (through the direct links of the nodes, the way the cables run by hand do), and takes the joints back when the other side goes. It looks again every
// couple of seconds, so a cable laid on the other level later is found, and one that was cut is let go.
public sealed class CableShaftSystem : EntitySystem
{
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly DirectPowerSystem _direct = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    // What each shaft is joined to now, to take back when it is not wanted.
    private readonly Dictionary<EntityUid, HashSet<Node>> _joined = new();
    private readonly HashSet<Node> _wanted = new();
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CableShaftComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CableShaftComponent, AnchorStateChangedEvent>(OnAnchor);
    }

    private void OnShutdown(Entity<CableShaftComponent> shaft, ref ComponentShutdown args)
    {
        Release(shaft.Owner, shaft.Comp);
    }

    private void OnAnchor(Entity<CableShaftComponent> shaft, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            Refresh(shaft.Owner, shaft.Comp);
        else
            Release(shaft.Owner, shaft.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Every;

        var query = EntityQueryEnumerator<CableShaftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shaft, out var xform))
        {
            if (xform.Anchored)
                Refresh(uid, shaft);
        }
    }

    public void Refresh(EntityUid uid, CableShaftComponent shaft)
    {
        if (!_nodeContainer.TryGetNode<Node>(uid, shaft.Node, out var node) || Transform(uid).MapUid is not { } map)
            return;

        _wanted.Clear();
        if (shaft.Up && _zLevels.TryMapUp(new Entity<CMUZLevelMapComponent?>(map, null), out var above))
            Collect(uid, node, above.Value.Owner);

        if (shaft.Down && _zLevels.TryMapDown(new Entity<CMUZLevelMapComponent?>(map, null), out var below))
            Collect(uid, node, below.Value.Owner);

        var joined = _joined.GetValueOrDefault(uid) ?? (_joined[uid] = new HashSet<Node>());
        foreach (var other in _wanted)
        {
            if (!joined.Add(other))
                continue;

            node.DirectLinks.Add(other);
            other.DirectLinks.Add(node);
            _nodeGroup.QueueReflood(node);
            _nodeGroup.QueueReflood(other);

            // The machines of the low voltage take their APC through the joint too.
            _direct.Linked(uid, other.Owner);
        }

        joined.RemoveWhere(other =>
        {
            if (_wanted.Contains(other))
                return false;

            node.DirectLinks.Remove(other);
            other.DirectLinks.Remove(node);
            _nodeGroup.QueueReflood(node);
            _nodeGroup.QueueReflood(other);
            _direct.Unlinked(uid, other.Owner);
            return true;
        });
    }

    // The nodes of the network of the shaft that are anchored in the same place on the level.
    private void Collect(EntityUid uid, Node node, EntityUid level)
    {
        if (!TryComp<MapComponent>(level, out var mapComp))
            return;

        // The ground of a level is a grid of its own (the stone of the caves), or the level itself (the levels that are maps and grids at once).
        var world = _transform.GetWorldPosition(uid);
        var coordinates = new MapCoordinates(world, mapComp.MapId);
        if (!_map.TryFindGridAt(coordinates, out var gridUid, out var grid))
        {
            if (!TryComp(level, out grid))
                return;

            gridUid = level;
        }

        var tile = _map.WorldToTile(gridUid, grid, world);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (!TryComp<NodeContainerComponent>(other.Value, out var container))
                continue;

            foreach (var candidate in container.Nodes.Values)
            {
                if (candidate.NodeGroupID == node.NodeGroupID && candidate != node)
                    _wanted.Add(candidate);
            }
        }
    }

    private void Release(EntityUid uid, CableShaftComponent shaft)
    {
        if (!_joined.Remove(uid, out var joined))
            return;

        if (!_nodeContainer.TryGetNode<Node>(uid, shaft.Node, out var node))
            return;

        foreach (var other in joined)
        {
            node.DirectLinks.Remove(other);
            other.DirectLinks.Remove(node);
            _nodeGroup.QueueReflood(other);
            _direct.Unlinked(uid, other.Owner);
        }

        _nodeGroup.QueueReflood(node);
    }
}
