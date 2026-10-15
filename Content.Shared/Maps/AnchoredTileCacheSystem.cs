using System.Collections.Immutable;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Map.Components;

namespace Content.Shared.Maps;

/// <summary>
/// Bounded, immutable membership snapshots shared by repeated tile queries. Component
/// values are deliberately not cached: callers still observe current height, immunity,
/// damage and permission state. Invalidating a snapshot cannot change an active reader.
/// </summary>
public sealed partial class AnchoredTileCacheSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), LinkedListNode<Entry>> _tiles = new();
    private readonly LinkedList<Entry> _recent = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _admission = new();
    private readonly List<EntityUid> _scratch = new();
    private readonly Dictionary<EntityUid, HashSet<Vector2i>> _gridTiles = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _movingTiles = new();
    private const int Capacity = 4096;
    private const int MaximumRetainedMembers = 256;
    public long CacheHits { get; private set; }
    public long CacheMisses { get; private set; }
    public long CacheEvictions { get; private set; }
    public int CachedTileCount => _tiles.Count;
    public int AdmissionCount => _admission.Count;

    private readonly record struct Entry((EntityUid Grid, Vector2i Tile) Key, ImmutableArray<EntityUid> Members);

    public override void Initialize()
    {
        // Anchor events can precede installation of the destination transform.
        // Other invalidations have a known grid/tile and can preserve unrelated entries.
        _transform.OnGlobalMoveEvent += OnMove;
        SubscribeLocalEvent<TransformComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<TransformComponent, ReAnchorEvent>(OnReanchor);
        SubscribeLocalEvent<TransformComponent, ComponentShutdown>(OnShutdown);
        // Transform startup raises AnchorStateChangedEvent for initially anchored entities too.
        // MapInit does not need a second invalidation or an additional lifecycle subscription.
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
    }

    private void ClearTiles()
    {
        _tiles.Clear();
        _recent.Clear();
        _admission.Clear();
        _gridTiles.Clear();
    }

    private void Invalidate(EntityUid grid, Vector2i tile)
    {
        _admission.Remove((grid, tile));
        if (!_tiles.Remove((grid, tile), out var node)) return;
        _recent.Remove(node);
        if (_gridTiles.TryGetValue(grid, out var tiles))
        {
            tiles.Remove(tile);
            if (tiles.Count == 0) _gridTiles.Remove(grid);
        }
    }

    private void InvalidateGrid(EntityUid grid)
    {
        if (!_gridTiles.Remove(grid, out var tiles)) return;
        foreach (var tile in tiles)
        {
            if (_tiles.Remove((grid, tile), out var node)) _recent.Remove(node);
        }
        // Probation contains no snapshots and is bounded independently.
        _admission.Clear();
    }

    private void InvalidatePosition(TransformComponent xform)
    {
        if (xform.GridUid is { } gridUid && TryComp(gridUid, out MapGridComponent? grid))
            Invalidate(gridUid, _map.CoordinatesToTile(gridUid, grid, xform.Coordinates));
    }

    private void OnAnchor(Entity<TransformComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
        {
            // The usual path has already installed membership at the current position.
            // Direct anchoring to another grid raises this event before installing its
            // destination transform; only that ambiguous case needs the global fallback.
            if (ent.Comp.GridUid is { } gridUid && TryComp(gridUid, out MapGridComponent? grid))
            {
                var tile = _map.CoordinatesToTile(gridUid, grid, ent.Comp.Coordinates);
                var query = _map.GetAnchoredEntities(gridUid, grid, tile);
                while (query.MoveNext(out var uid))
                {
                    if (uid != ent.Owner) continue;
                    Invalidate(gridUid, tile);
                    return;
                }
            }
            ClearTiles();
            return;
        }
        InvalidatePosition(ent.Comp);
    }

    private void OnReanchor(Entity<TransformComponent> ent, ref ReAnchorEvent args)
    {
        InvalidateGrid(args.OldGrid);
        InvalidateGrid(args.Grid);
    }

    private void OnShutdown(Entity<TransformComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Anchored) InvalidatePosition(ent.Comp);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes) Invalidate(args.Entity, change.GridIndices);
    }

    private void OnGridRemoved(GridRemovalEvent args) => InvalidateGrid(args.EntityUid);

    private void OnMove(ref MoveEvent args)
    {
        if (!args.Component.Anchored || args.OnlyRotation) return;
        InvalidateMovingTile(args.OldPosition);
        InvalidateMovingTile(args.NewPosition);
    }

    private void InvalidateMovingTile(EntityCoordinates position)
    {
        if (!TryComp(position.EntityId, out MapGridComponent? grid)) return;
        var tile = _map.CoordinatesToTile(position.EntityId, grid, position);
        Invalidate(position.EntityId, tile);
        // Client state application installs snap membership after MoveEvent. A callback
        // may query during the event: do not retain that intermediate snapshot.
        if (_net.IsClient) _movingTiles.Add((position.EntityId, tile));
    }

    public override void Update(float frameTime)
    {
        _movingTiles.Clear();
    }

    // ImmutableArray preserves value enumeration and stable readers while remaining
    // verifiable by the content sandbox, which rejects the span-returning API.
    public ImmutableArray<EntityUid> Get(EntityUid entity)
    {
        var coordinates = Transform(entity).Coordinates;
        if (_transform.GetGrid(coordinates) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return ImmutableArray<EntityUid>.Empty;
        return Get((gridUid, grid), _map.CoordinatesToTile(gridUid, grid, coordinates));
    }

    public ImmutableArray<EntityUid> Get(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var key = (grid.Owner, tile);
        var moving = _movingTiles.Count > 0 && _movingTiles.Contains(key);
        if (!moving && _tiles.TryGetValue(key, out var hit))
        {
            CacheHits++;
            if (hit != _recent.Last)
            {
                _recent.Remove(hit);
                _recent.AddLast(hit);
            }
            return hit.Value.Members;
        }

        CacheMisses++;
        _scratch.Clear();
        var query = _map.GetAnchoredEntities(grid, grid.Comp, tile);
        while (query.MoveNext(out var uid)) _scratch.Add(uid.Value);
        var cached = _scratch.ToImmutableArray();
        _scratch.Clear();
        if (_scratch.Capacity > MaximumRetainedMembers) _scratch.Capacity = 0;
        // Empty cells need no snapshot. Large cells are still returned in full, but
        // must not make a brief population spike permanently enlarge this cache.
        if (moving || cached.IsEmpty || cached.Length > MaximumRetainedMembers) return cached;
        if (!_admission.Remove(key))
        {
            // Admit on the second occupied query, protecting hot snapshots from a
            // stream of one-off cells. Reset probation without disturbing residents.
            if (_admission.Count >= Capacity) _admission.Clear();
            _admission.Add(key);
            return cached;
        }
        if (_tiles.Count >= Capacity)
        {
            var oldest = _recent.First!.Value.Key;
            Invalidate(oldest.Grid, oldest.Tile);
            CacheEvictions++;
        }
        _tiles.Add(key, _recent.AddLast(new Entry(key, cached)));
        if (!_gridTiles.TryGetValue(grid.Owner, out var tiles)) _gridTiles[grid.Owner] = tiles = new();
        tiles.Add(tile);
        return cached;
    }

    public override void Shutdown()
    {
        _transform.OnGlobalMoveEvent -= OnMove;
        ClearTiles();
        _movingTiles.Clear();
        _scratch.Clear();
        base.Shutdown();
    }
}
