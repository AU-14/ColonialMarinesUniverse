// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared.Maps; // RUMC: the tile of a built floor that gives way
using Robust.Shared.Prototypes; // RUMC: the prototype of what it drops
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Chat.Managers;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core; // CMU14: IsFallThroughTile
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 1: the structural support graph.
///
/// A structure carrying <see cref="StructuralSupportComponent"/> is "supported" only if it can trace a
/// cardinal path back to an anchor within the available cantilever budget. Anchors are either explicitly
/// flagged (<see cref="StructuralSupportComponent.IsAnchor"/>) or auto-detected: a support sitting on a
/// solid (non-empty) tile of the lowest z-level (a map with no <see cref="CMUZLevelMapComponent.MapBelow"/>)
/// is rooted in bedrock.
///
/// Phase 1 is intentionally NON-DESTRUCTIVE: it only computes <see cref="StructuralSupportComponent.Supported"/>
/// (visible in ViewVariables), logs transitions, and popups newly-unsupported structures. Collapse
/// scheduling (the 8s warning, lower-z effects, despawn+debris on upper z) lands in a later phase.
/// </summary>
public sealed partial class ZLevelSupportSystem : EntitySystem
{
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Tag.TagPrototype> WallTag = "Wall";

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ZLandingSystem _landing = default!; // RUMC: where a falling thing lands
    [Dependency] private ITileDefinitionManager _tileDefs = default!; // RUMC: the item of a floor tile
    [Dependency] private ZLevelBuildingSystem _building = default!; // CMU14: authored-level checks for IsAuthoredCell
    [Dependency] private ZFootprintCollapseSystem _footprint = default!; // CMU14: multi-tile structures collapse whole

    // Per-map cooldown so a cascading floor collapse (many structures at once) logs/alerts admins once, not per
    // tile. Maps the collapsing level -> the time after which the next collapse there alerts again.
    private readonly Dictionary<EntityUid, TimeSpan> _nextCollapseAlert = new();
    private static readonly TimeSpan CollapseAlertCooldown = TimeSpan.FromSeconds(3);

    // CMU14: collapses on each map that happened inside the alert cooldown, reported with the next structured log.
    private readonly Dictionary<EntityUid, int> _collapsesSinceAlert = new();

    // Exact attribution: the last PLAYER to damage a support on each map, with the time. Used to name the real
    // culprit in the collapse log/alert (e.g. whoever shot the beam out) instead of just "nearest player".
    private readonly Dictionary<EntityUid, (EntityUid Culprit, TimeSpan Time)> _lastSupportDamager = new();
    private static readonly TimeSpan AttributionWindow = TimeSpan.FromSeconds(15);

    /// <summary>Crash SFX when an unsupported structure caves in (guarded against a missing path).</summary>
    private static readonly SoundSpecifier CollapseSound = new SoundPathSpecifier("/Audio/Effects/explosion3.ogg");

    // RUMC: the debris prototype is switched off, not deleted (the prototype AU14RockDebris is commented out in stone.yml): the material of the floor comes down instead.
    // RUMC: /// <summary>Debris that rains onto the level below when a structure caves in.</summary>
    // RUMC: private const string DebrisProto = "AU14RockDebris";

    private static readonly Vector2i[] Cardinals =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;

    // Grids whose support graph needs recomputing next tick (debounce so a multi-entity build only solves once).
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    // CMU14: the list that was here (kept): private readonly List<EntityUid> _processing = new();

    // CMU14 Begin: ordered, bounded re-solves (GraphAudit ECS-01). Dirty grids solve lowest level first, so a level
    // reads the settled state of the level below it; a solve that dirties the level above is followed up in the same
    // tick, at most MaxSolvePasses times.
    private const int MaxSolvePasses = 8;
    private readonly List<(int Depth, EntityUid Grid)> _solveOrder = new();
    private static readonly Comparison<(int Depth, EntityUid Grid)> ByDepth = (a, b) => a.Depth.CompareTo(b.Depth);
    // Grids still to solve later in the current pass; marking one dirty again would only repeat its solve.
    private readonly HashSet<EntityUid> _pendingThisPass = new();
    // Grids where a support that projects upward (vertical, anchor or wall) was added or removed since their last solve.
    private readonly HashSet<EntityUid> _projectionChangedGrids = new();
    // CMU14 End

    // CMU14 Begin: pooled collections for the solve and the collapse drop (GraphAudit ECS-04). Each use rents its own
    // instance and returns it cleared, so nested use (a solve or a drop inside another one's callbacks) never shares one.
    private sealed class SolveBuffers
    {
        public readonly Dictionary<Vector2i, List<Entity<StructuralSupportComponent>>> ByTile = new();
        public readonly Dictionary<EntityUid, bool> Previous = new();
        public readonly Queue<(Vector2i Tile, int Budget)> Queue = new();
        public readonly Dictionary<Vector2i, int> Best = new();
        private readonly Stack<List<Entity<StructuralSupportComponent>>> _lists = new();

        public List<Entity<StructuralSupportComponent>> RentList()
            => _lists.Count > 0 ? _lists.Pop() : new List<Entity<StructuralSupportComponent>>();

        public void Clear()
        {
            foreach (var list in ByTile.Values)
            {
                list.Clear();
                _lists.Push(list);
            }

            ByTile.Clear();
            Previous.Clear();
            Queue.Clear();
            Best.Clear();
        }
    }

    private readonly Stack<SolveBuffers> _solveBufferPool = new();
    private readonly Stack<List<EntityUid>> _entityListPool = new();

    private List<EntityUid> RentEntityList()
        => _entityListPool.Count > 0 ? _entityListPool.Pop() : new List<EntityUid>();

    private void ReturnEntityList(List<EntityUid> list)
    {
        list.Clear();
        _entityListPool.Push(list);
    }
    // CMU14 End

    // Event-maintained support index. Recomputing one grid must never enumerate every support entity in the
    // world, and propagating upward only needs the grids that actually contain supports on the map above.
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _supportsByGrid = new();
    private readonly Dictionary<EntityUid, EntityUid> _supportGrid = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _supportGridsByMap = new();
    private readonly Dictionary<EntityUid, EntityUid> _indexedGridMap = new();
    private readonly List<EntityUid> _staleIndexedSupports = new();

    // Structural entities that have lost support: maps entity uid -> time at which it will collapse.
    // Cleared if the entity regains support before the deadline (counterplay via building more pillars/anchors).
    private readonly Dictionary<EntityUid, PendingCollapse> _pendingUnsupported = new();
    private readonly PriorityQueue<EntityUid, TimeSpan> _unsupportedDeadlines = new();
    private readonly record struct PendingCollapse(TimeSpan Deadline, EntityUid Grid, Vector2i Tile);

    /// <summary>Seconds a structure remains standing after losing its last support before collapsing.</summary>
    private const float CollapseWarningSeconds = 5f;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();

        SubscribeLocalEvent<StructuralSupportComponent, ComponentStartup>(OnSupportStartup);
        SubscribeLocalEvent<StructuralSupportComponent, MapInitEvent>(OnSupportMapInit);
        SubscribeLocalEvent<StructuralSupportComponent, ComponentShutdown>(OnSupportShutdown);
        SubscribeLocalEvent<StructuralSupportComponent, AnchorStateChangedEvent>(OnSupportAnchorChanged);
        SubscribeLocalEvent<StructuralSupportComponent, EntParentChangedMessage>(OnSupportParentChanged);
        SubscribeLocalEvent<StructuralSupportComponent, DamageChangedEvent>(OnSupportDamaged);
        SubscribeLocalEvent<ZLevelWallSupportComponent, MapInitEvent>(OnWallSupportMapInit);
        SubscribeLocalEvent<ZLevelWallSupportComponent, ComponentShutdown>(OnWallSupportShutdown);
        SubscribeLocalEvent<ZLevelWallSupportComponent, AnchorStateChangedEvent>(OnWallSupportAnchorChanged);
        SubscribeLocalEvent<PlayerBuiltComponent, ComponentStartup>(OnPlayerBuiltStartup);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
        SubscribeLocalEvent<CMUZLevelMapLeftNetworkEvent>(OnMapLeftNetwork); // CMU14
        SubscribeLocalEvent<ZFloorSupportQueryEvent>(OnFloorSupportQuery); // CMU14

        // All of these are keyed by round-scoped uids; drop them with the round so stale entries never accumulate.
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _lastSupportDamager.Clear();
            _nextCollapseAlert.Clear();
            _collapsesSinceAlert.Clear();
            _pendingUnsupported.Clear();
            _unsupportedDeadlines.Clear();
            _dirtyGrids.Clear();
            _supportsByGrid.Clear();
            _supportGrid.Clear();
            _supportGridsByMap.Clear();
            _indexedGridMap.Clear();
            _solveOrder.Clear(); // CMU14
            _pendingThisPass.Clear(); // CMU14
            _projectionChangedGrids.Clear(); // CMU14
        });
    }

    // CMU14: only the levels of the changed network re-solve, not every enrolled grid (GraphAudit ECS-01). The version
    // that was here (kept):
    // CMU14: private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    // CMU14: {
    // CMU14:     // Removal notifications contain the surviving network only. Invalidate enrolled grids as well as
    // CMU14:     // current members so detached levels stop using their former depth and projected support.
    // CMU14:     foreach (var grid in _supportsByGrid.Keys)
    // CMU14:     {
    // CMU14:         EnsureGridMapIndexed(grid);
    // CMU14:         _dirtyGrids.Add(grid);
    // CMU14:     }
    // CMU14: }
    private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        // Removal notifications contain the surviving network only; a detached level is re-solved by
        // OnMapLeftNetwork, so it stops using its former depth and projected support.
        foreach (var map in args.Network.Comp.ZLevelByEntity.Keys)
            DirtySupportGridsOnMap(map);
    }

    // CMU14: a level that left its network (detach, network teardown or deletion) re-solves at its new depth.
    private void OnMapLeftNetwork(ref CMUZLevelMapLeftNetworkEvent args)
        => DirtySupportGridsOnMap(args.Map);

    private void DirtySupportGridsOnMap(EntityUid map)
    {
        if (!_supportGridsByMap.TryGetValue(map, out var grids))
            return;

        foreach (var grid in grids)
        {
            if (IsStillOnMap(grid, map))
                _dirtyGrids.Add(grid);
        }

        FlushGridsThatChangedMap();
    }

    // CMU14: a grid that changed map (FTL, MultiDeck, tactical landing, an admin move) without a support event keeps
    // its old map in the per-map index. Candidates found under the wrong map are skipped, then reindexed and
    // re-solved on their new map.
    private readonly List<EntityUid> _gridsThatChangedMap = new();

    private bool IsStillOnMap(EntityUid grid, EntityUid map)
    {
        if (TryComp(grid, out TransformComponent? xform) && xform.MapUid == map)
            return true;

        _gridsThatChangedMap.Add(grid);
        return false;
    }

    private void FlushGridsThatChangedMap()
    {
        if (_gridsThatChangedMap.Count == 0)
            return;

        foreach (var grid in _gridsThatChangedMap)
        {
            EnsureGridMapIndexed(grid);
            if (!Deleted(grid))
                _dirtyGrids.Add(grid);
        }

        _gridsThatChangedMap.Clear();
    }

    /// <summary>Records the last player to damage a support on a level, so a resulting collapse names the real culprit.</summary>
    private void OnSupportDamaged(Entity<StructuralSupportComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin || !HasComp<ActorComponent>(origin))
            return;

        if (Transform(ent).MapUid is { } mapUid)
            _lastSupportDamager[mapUid] = (origin, _timing.CurTime);

        // Cave-in attribution shares this component event. Robust permits only one directed subscription for a
        // component/event pair, so relay structural-only supports rather than making ZCaveInSystem subscribe too.
        // Walls carry their own marker and are handled by the cave system's wall-scoped damage subscription.
        if (!HasComp<ZLevelWallSupportComponent>(ent))
        {
            var caveEvent = new ZCaveSupportDamagedEvent(ent.Owner, origin);
            RaiseLocalEvent(ref caveEvent);
        }
    }

    private void OnSupportStartup(Entity<StructuralSupportComponent> ent, ref ComponentStartup args)
    {
        ReindexSupport(ent);
        MarkGridDirty(ent);
    }

    private void OnSupportMapInit(Entity<StructuralSupportComponent> ent, ref MapInitEvent args)
    {
        // Startup can precede final map/grid parenting during map load. Reindex at MapInit to capture the final
        // grid; HashSet membership keeps the common already-correct case idempotent.
        ReindexSupport(ent);
        MarkGridDirty(ent);
    }

    private void OnSupportShutdown(Entity<StructuralSupportComponent> ent, ref ComponentShutdown args)
    {
        _pendingUnsupported.Remove(ent.Owner);

        if (RemoveIndexedSupport(ent, out var oldGrid))
            _dirtyGrids.Add(oldGrid);
        else
            MarkGridDirty(ent);
    }

    private void OnSupportAnchorChanged(Entity<StructuralSupportComponent> ent, ref AnchorStateChangedEvent args)
    {
        ReindexSupport(ent);
        MarkGridDirty(ent);

        // Player-built walls carry both markers; the wall handler below relays them once.
        if (!args.Anchored && !HasComp<ZLevelWallSupportComponent>(ent))
        {
            var caveEvent = new ZCaveSupportRemovedEvent(ent.Owner);
            RaiseLocalEvent(ref caveEvent);
        }
    }

    private void OnSupportParentChanged(Entity<StructuralSupportComponent> ent, ref EntParentChangedMessage args)
    {
        ReindexSupport(ent);
        MarkGridDirty(ent);
    }

    // Walls only project support upward. CMU14: so a wall change re-solves only a grid above that has a support at
    // the wall's cell (GraphAudit ECS-01). Its own grid is not dirtied: an own-level solve reads only
    // StructuralSupport entities, and a wall that is also one (a player-built wall) dirties its grid through the
    // StructuralSupport events. The handlers that were here (kept):
    // CMU14: private void OnWallSupportMapInit(Entity<ZLevelWallSupportComponent> ent, ref MapInitEvent args)
    // CMU14:     => MarkGridDirty(ent);
    // CMU14: (OnWallSupportShutdown and OnWallSupportAnchorChanged began with MarkGridDirty(ent);)
    private void OnWallSupportMapInit(Entity<ZLevelWallSupportComponent> ent, ref MapInitEvent args)
        => DirtySupportsAbove(ent);

    private void OnWallSupportShutdown(Entity<ZLevelWallSupportComponent> ent, ref ComponentShutdown args)
    {
        // CMU14: generation replacing or clearing its own rock is not a lost support (GraphAudit ECS-02).
        if (Transform(ent).Anchored && !_building.IsGenerationRemoval(ent))
            DirtySupportsAbove(ent);

        if (Transform(ent).Anchored)
        {
            var ev = new ZLevelSupportChangedEvent(ent.Owner);
            RaiseLocalEvent(ref ev);
        }
    }

    private void OnWallSupportAnchorChanged(Entity<ZLevelWallSupportComponent> ent, ref AnchorStateChangedEvent args)
    {
        // CMU14: see OnWallSupportShutdown.
        if (args.Anchored || !_building.IsGenerationRemoval(ent))
            DirtySupportsAbove(ent);

        if (!args.Anchored)
        {
            var ev = new ZLevelSupportChangedEvent(ent.Owner);
            RaiseLocalEvent(ref ev);
            var caveEvent = new ZCaveSupportRemovedEvent(ent.Owner);
            RaiseLocalEvent(ref caveEvent);
        }
    }

    /// <summary>
    /// Mapper walls carry only the lightweight upward-support marker. Once a wall is actually constructed by a
    /// player, enroll that particular instance in the collapsible graph.
    /// </summary>
    private void OnPlayerBuiltStartup(Entity<PlayerBuiltComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<ZLevelWallSupportComponent>(ent))
            return;

        var support = EnsureComp<StructuralSupportComponent>(ent);
        support.IsVerticalSupport = true;
        support.CantileverSpan = ZLevelWallSupportComponent.CantileverSpan;
        ReindexSupport(ent);
        MarkGridDirty(ent);
        if (Transform(ent).GridUid is { } grid)
            _projectionChangedGrids.Add(grid); // CMU14: it now projects through the graph
    }

    // CMU14 Begin: non-player enrolment (GraphAudit ARCH-01).
    /// <summary>
    /// Enrolls a structure built by something other than a player (an admin tool, the Grand Machine, a scripted
    /// event) in the collapsible support graph, exactly as a player-built wall is enrolled, but without
    /// <c>PlayerBuilt</c>. It gets a <see cref="ZEnrolledSupportComponent"/>, so its cell is never treated as
    /// authored and it can still collapse on an authored level.
    /// <para>Rules for callers:</para>
    /// <list type="bullet">
    /// <item>spawn the structure anchored, after the tile under it exists (nothing anchors on an empty tile);</item>
    /// <item>never give it <c>PlayerBuilt</c>: that records it as a player's construction (builder on examine);</item>
    /// <item>lay a floor marker (<c>AU14TileFloorSupport</c>) only over an empty or fall-through tile, as
    /// <c>TileApplierSystem</c> does; a solid upper tile without one counts as self-supporting authored floor;</item>
    /// <item>give it <c>ZDestroyedOnCollapse</c> if it should vanish rather than fall when its floor gives way;</item>
    /// <item>spawn a bulk build in one tick, so the debounced solver re-solves each grid once;</item>
    /// <item><b>never raise <c>ConstructionCompletedEvent</c> with a player as the builder</b>: that marks it
    /// <c>PlayerBuilt</c> through <c>PlayerBuiltSystem</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="uid">The anchored structure.</param>
    /// <param name="span">Its cantilever span (how far it carries support).</param>
    /// <param name="isVertical">True for a column (girder, wall): it projects its span to the level above only.</param>
    public void Enroll(EntityUid uid, int span, bool isVertical)
    {
        var support = EnsureComp<StructuralSupportComponent>(uid);
        support.CantileverSpan = span;
        support.IsVerticalSupport = isVertical;
        EnsureComp<ZEnrolledSupportComponent>(uid);
        ReindexSupport(uid);
        MarkGridDirty(uid);
        if (Transform(uid).GridUid is { } grid)
            _projectionChangedGrids.Add(grid);
        Dirty(uid, support);
    }
    // CMU14 End

    /// <summary>Moves a support's index entry to its current grid and dirties both sides of a grid change.</summary>
    private void ReindexSupport(EntityUid uid)
    {
        var newGrid = !Deleted(uid) && TryComp(uid, out TransformComponent? xform) && xform.Anchored
            ? xform.GridUid
            : null;
        if (newGrid == null)
            _pendingUnsupported.Remove(uid);
        _supportGrid.TryGetValue(uid, out var oldGrid);
        var hadOldGrid = _supportGrid.ContainsKey(uid);

        if (hadOldGrid && newGrid == oldGrid)
        {
            EnsureGridMapIndexed(oldGrid);
            return;
        }

        if (hadOldGrid)
        {
            _pendingUnsupported.Remove(uid);
            RemoveIndexedSupport(uid, out _);
            _dirtyGrids.Add(oldGrid);
        }

        if (newGrid is not { } grid)
            return;

        if (!_supportsByGrid.TryGetValue(grid, out var supports))
        {
            supports = new HashSet<EntityUid>();
            _supportsByGrid.Add(grid, supports);
        }

        supports.Add(uid);
        _supportGrid[uid] = grid;
        EnsureGridMapIndexed(grid);
        _dirtyGrids.Add(grid);
        if (ProjectsUpward(uid))
            _projectionChangedGrids.Add(grid); // CMU14
    }

    private bool RemoveIndexedSupport(EntityUid uid, out EntityUid oldGrid)
    {
        if (!_supportGrid.Remove(uid, out oldGrid))
            return false;

        if (ProjectsUpward(uid))
            _projectionChangedGrids.Add(oldGrid); // CMU14

        if (!_supportsByGrid.TryGetValue(oldGrid, out var supports))
            return true;

        supports.Remove(uid);
        if (supports.Count > 0)
            return true;

        _supportsByGrid.Remove(oldGrid);
        if (_indexedGridMap.Remove(oldGrid, out var mapUid) &&
            _supportGridsByMap.TryGetValue(mapUid, out var grids))
        {
            grids.Remove(oldGrid);
            if (grids.Count == 0)
                _supportGridsByMap.Remove(mapUid);
        }

        return true;
    }

    private void EnsureGridMapIndexed(EntityUid grid)
    {
        var mapUid = !Deleted(grid) && TryComp(grid, out TransformComponent? gridXform)
            ? gridXform.MapUid
            : null;

        if (mapUid is not { } currentMap)
            return;

        if (_indexedGridMap.TryGetValue(grid, out var indexedMap))
        {
            if (indexedMap == currentMap)
                return;

            if (_supportGridsByMap.TryGetValue(indexedMap, out var oldGrids))
            {
                oldGrids.Remove(grid);
                if (oldGrids.Count == 0)
                    _supportGridsByMap.Remove(indexedMap);
            }
        }

        _indexedGridMap[grid] = currentMap;
        if (!_supportGridsByMap.TryGetValue(currentMap, out var currentGrids))
        {
            currentGrids = new HashSet<EntityUid>();
            _supportGridsByMap.Add(currentMap, currentGrids);
        }

        currentGrids.Add(grid);
    }

    // CMU14 Begin: read-only diagnostics and the authored-cell invariant (GraphAudit TEST-01, FLOW-01).
    /// <summary>How many structures on <paramref name="grid"/> are counting down to a support collapse. Read-only.</summary>
    public int PendingCollapseCount(EntityUid grid)
    {
        var count = 0;
        foreach (var pending in _pendingUnsupported.Values)
        {
            if (pending.Grid == grid)
                count++;
        }

        return count;
    }

    /// <summary>
    /// True when <paramref name="tile"/> on <paramref name="grid"/> is mapper-authored content of an authored level:
    /// the floor is authored (<see cref="IsAuthoredFloor"/>) and no player-built or enrolled structure is anchored on
    /// it. Used for the blocks of a cell (what holds a roof up): collapses must never take or schedule on such a cell.
    /// </summary>
    public bool IsAuthoredCell(EntityUid map, Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!IsAuthoredFloor(map, grid, tile))
            return false;

        foreach (var anchored in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
        {
            if (HasComp<PlayerBuiltComponent>(anchored) || HasComp<ZEnrolledSupportComponent>(anchored))
                return false;
        }

        return true;
    }

    /// <summary>
    /// True when the FLOOR of <paramref name="tile"/> is mapper-authored: the map is an authored level, the tile was not
    /// created by stone generation, and it carries no player floor marker. What stands on the floor does not matter:
    /// a barricade or a table a player put on a colony floor never makes that floor collapsible.
    /// </summary>
    public bool IsAuthoredFloor(EntityUid map, Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_building.IsAuthoredLevel(map))
            return false;

        // Generated tiles are indexed on the stone grid only.
        if (TryComp<ZGeneratedStoneComponent>(map, out var stone) &&
            stone.StoneGrid == grid.Owner &&
            stone.GeneratedTiles.Contains(tile))
            return false;

        return !HasPlayerFloorMarker(grid, tile);
    }

    private bool IsLocalizedLevel(EntityUid? map)
        => map is { } uid && TryComp<ZGeneratedStoneComponent>(uid, out var stone) && stone.LocalizedToAuthoredLevel;

    private int DepthOf(EntityUid? map)
        => map is { } uid && _zMapQuery.TryComp(uid, out var z) ? z.Depth : 0;

    /// <summary>True if <paramref name="grid"/> is queued for a support re-solve. Read-only diagnostics.</summary>
    public bool IsGridDirty(EntityUid grid) => _dirtyGrids.Contains(grid);

    // CMU14 Begin: build-time support prediction (GraphAudit FLOW-03).
    /// <summary>
    /// Predicts whether a new non-anchor support placed on <paramref name="tile"/> would be held up, using the same
    /// rules as <see cref="RecomputeGrid"/>: it is rooted when the level is ground or below, when it stands on a solid
    /// upper tile that carries no player floor marker (authored floor), or when a supported column or wall stands
    /// below it; otherwise it must be reached through the current supports of the grid within their spans.
    /// A floor laid over an empty or fall-through upper tile gets a player floor marker (TileApplierSystem), so such
    /// a tile never counts as authored floor here. For a new floor marker (<c>AU14TileFloorSupport</c>) pass
    /// <paramref name="span"/> 1 and <paramref name="vertical"/> false.
    /// </summary>
    /// <param name="span">The new support's cantilever span. It decides what the support would hold up, not whether
    /// it is held, so it does not change the answer; it is accepted so callers describe the support fully.</param>
    /// <param name="vertical">True for a column; a column is held exactly like any other support.</param>
    public bool WouldBeSupported(EntityUid map, Entity<MapGridComponent> grid, Vector2i tile, int span, bool vertical)
    {
        if (IsGroundOrBelow(map))
            return true;

        var current = _map.GetTileRef(grid.Owner, grid.Comp, tile).Tile;
        var willHaveFloorMarker = HasPlayerFloorMarker(grid, tile) ||
                                  CMUZLevelOpeningCache.IsFallThroughTile(current, _tileDefs);
        if (!willHaveFloorMarker)
            return true;

        if (_zMapQuery.TryComp(map, out var zMap) &&
            zMap.MapBelow is { } below &&
            TryGetSupportSpanBelow(below, _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, tile)).Position, out _))
            return true;

        // Reached on its own level: run the solver's flood over the current supports without changing their state.
        var byTile = new Dictionary<Vector2i, List<Entity<StructuralSupportComponent>>>();
        if (_supportsByGrid.TryGetValue(grid.Owner, out var indexedSupports))
        {
            foreach (var uid in indexedSupports)
            {
                if (!TryComp<StructuralSupportComponent>(uid, out var comp) ||
                    !TryComp(uid, out TransformComponent? xform) ||
                    !xform.Anchored || xform.GridUid != grid.Owner)
                    continue;

                var supportTile = _map.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);
                if (!byTile.TryGetValue(supportTile, out var list))
                    byTile[supportTile] = list = new List<Entity<StructuralSupportComponent>>();
                list.Add((uid, comp));
            }
        }

        var queue = new Queue<(Vector2i Tile, int Budget)>();
        var best = new Dictionary<Vector2i, int>();
        foreach (var (supportTile, supports) in byTile)
        {
            foreach (var ent in supports)
            {
                if (!TryGetSeedBudget(ent, grid, supportTile, out var budget))
                    continue;

                if (!best.TryGetValue(supportTile, out var existing) || existing < budget)
                {
                    best[supportTile] = budget;
                    queue.Enqueue((supportTile, budget));
                }
            }
        }

        while (queue.TryDequeue(out var node))
        {
            if (!best.TryGetValue(node.Tile, out var cur) || cur != node.Budget)
                continue;

            foreach (var dir in Cardinals)
            {
                var next = node.Tile + dir;
                var isTarget = next == tile;
                if (!byTile.TryGetValue(next, out var nextSupports) && !isTarget)
                    continue;

                var nextBudget = node.Budget - 1;
                if (nextSupports != null)
                {
                    foreach (var nextSupport in nextSupports)
                    {
                        if (nextSupport.Comp.IsAnchor && !nextSupport.Comp.IsVerticalSupport)
                            nextBudget = Math.Max(nextBudget, nextSupport.Comp.CantileverSpan);
                    }
                }

                if (nextBudget < 0)
                    continue;

                if (isTarget)
                    return true;

                if (best.TryGetValue(next, out var seen) && seen >= nextBudget)
                    continue;

                best[next] = nextBudget;
                queue.Enqueue((next, nextBudget));
            }
        }

        return best.ContainsKey(tile);
    }

    /// <summary>
    /// Answers the plating recipe's <see cref="ZFloorWouldBeSupported"/> condition. It refuses only a floor on an
    /// upper level, over an empty or fall-through tile (the tiles TileApplierSystem gives a floor marker), that
    /// nothing would hold up.
    /// </summary>
    private void OnFloorSupportQuery(ref ZFloorSupportQueryEvent args)
    {
        if (!args.Allowed || IsGroundOrBelow(args.Map) || !_gridQuery.TryComp(args.Grid, out var grid))
            return;

        var current = _map.GetTileRef(args.Grid, grid, args.Tile).Tile;
        if (!CMUZLevelOpeningCache.IsFallThroughTile(current, _tileDefs))
            return;

        if (!WouldBeSupported(args.Map, (args.Grid, grid), args.Tile, span: 1, vertical: false))
            args.Allowed = false;
    }
    // CMU14 End

    /// <summary>
    /// True if the support can change what the level above rests on (TryGetSupportSpanBelow): a vertical support, an
    /// anchor or a wall. Unknown (already removed) components count as projecting, to stay conservative.
    /// </summary>
    private bool ProjectsUpward(EntityUid uid)
    {
        if (HasComp<ZLevelWallSupportComponent>(uid))
            return true;

        return !TryComp<StructuralSupportComponent>(uid, out var support) || support.IsVerticalSupport || support.IsAnchor;
    }

    /// <summary>
    /// Queues every support grid on the level above <paramref name="wall"/> that has a support at the wall's cell.
    /// The per-tile lookup goes through the existing per-map support-grid index, so it stays correct for grids that
    /// move and needs no extra bookkeeping.
    /// </summary>
    private void DirtySupportsAbove(EntityUid wall)
    {
        var xform = Transform(wall);
        if (xform.MapUid is not { } map ||
            TerminatingOrDeleted(map) ||
            !_zMapQuery.TryComp(map, out var z) ||
            z.MapAbove is not { } above ||
            !_supportGridsByMap.TryGetValue(above, out var grids) ||
            !TryComp<MapComponent>(above, out var aboveMap))
            return;

        var coords = new MapCoordinates(_transform.GetWorldPosition(xform), aboveMap.MapId);
        foreach (var supportGrid in grids)
        {
            if (!IsStillOnMap(supportGrid, above) ||
                _pendingThisPass.Contains(supportGrid) ||
                !_gridQuery.TryComp(supportGrid, out var gridComp))
                continue;

            var tile = _map.TileIndicesFor(supportGrid, gridComp, coords);
            foreach (var anchored in _map.GetAnchoredEntities(supportGrid, gridComp, tile))
            {
                if (!HasComp<StructuralSupportComponent>(anchored))
                    continue;

                _dirtyGrids.Add(supportGrid);
                break;
            }
        }

        FlushGridsThatChangedMap();
    }
    // CMU14 End

    /// <summary>Queues the grid the entity currently sits on for a recompute next update.</summary>
    public void MarkGridDirty(EntityUid uid)
    {
        var grid = Transform(uid).GridUid;
        if (grid != null)
            _dirtyGrids.Add(grid.Value);
    }

    public override void Update(float frameTime)
    {
        // Recompute support graphs for grids that changed this tick.
        // CMU14: lowest level first, and the levels above a changed one in the same tick (GraphAudit ECS-01). The
        // block that was here (kept):
        // CMU14: if (_dirtyGrids.Count > 0)
        // CMU14: {
        // CMU14:     _processing.Clear();
        // CMU14:     _processing.AddRange(_dirtyGrids);
        // CMU14:     _dirtyGrids.Clear();
        // CMU14:
        // CMU14:     foreach (var grid in _processing)
        // CMU14:     {
        // CMU14:         if (_gridQuery.TryComp(grid, out var gridComp))
        // CMU14:             RecomputeGrid((grid, gridComp));
        // CMU14:     }
        // CMU14: }
        for (var pass = 0; pass < MaxSolvePasses && _dirtyGrids.Count > 0; pass++)
        {
            _solveOrder.Clear();
            foreach (var grid in _dirtyGrids)
            {
                var depth = TryComp(grid, out TransformComponent? gridXform) ? DepthOf(gridXform.MapUid) : int.MaxValue;
                _solveOrder.Add((depth, grid));
            }

            _dirtyGrids.Clear();
            _solveOrder.Sort(ByDepth);

            _pendingThisPass.Clear();
            foreach (var (_, grid) in _solveOrder)
                _pendingThisPass.Add(grid);

            foreach (var (_, grid) in _solveOrder)
            {
                _pendingThisPass.Remove(grid);
                if (_gridQuery.TryComp(grid, out var gridComp))
                    RecomputeGrid((grid, gridComp));
            }
        }

        _pendingThisPass.Clear();

        // Collapse only expired structures. The dictionary is authoritative (support recovery removes from it);
        // stale priority-queue entries are discarded when their old deadline reaches the head.
        if (_unsupportedDeadlines.Count > 0)
        {
            var now = _timing.CurTime;
            while (_unsupportedDeadlines.TryPeek(out var uid, out var collapseAt) && now >= collapseAt)
            {
                _unsupportedDeadlines.Dequeue();

                if (!_pendingUnsupported.TryGetValue(uid, out var pending) || pending.Deadline != collapseAt)
                    continue;

                _pendingUnsupported.Remove(uid);

                if (Deleted(uid))
                    continue;
                // Skip if it lost its support component (already collapsed as part of another tile's drop), or if
                // it was shored up before the deadline (it would then have been removed from the dict).
                if (!TryComp<StructuralSupportComponent>(uid, out var sup) || sup.Supported)
                    continue;

                // A warning belongs to the structural cell that lost support, never to a loose/moved object.
                var xform = Transform(uid);
                if (!xform.Anchored || xform.GridUid != pending.Grid ||
                    !_gridQuery.TryComp(pending.Grid, out var grid) ||
                    _map.TileIndicesFor(pending.Grid, grid, xform.Coordinates) != pending.Tile)
                {
                    ReindexSupport(uid);
                    MarkGridDirty(uid);
                    continue;
                }

                CollapseUnsupportedStructure(uid);
            }
        }
    }

    /// <summary>
    /// Multi-source cantilever BFS from every anchor on the grid. Each tile carries a remaining "budget";
    /// stepping onto another support costs 1. Vertical girders participate in the graph so they can themselves
    /// be supported, but never refresh or extend support on their own level; their span projects upward only.
    /// </summary>
    public void RecomputeGrid(Entity<MapGridComponent> grid)
    {
        // CMU14: the solve's collections come from a pool and go back cleared (GraphAudit ECS-04). Renting rather
        // than sharing one set keeps a re-entrant solve from reusing buffers an outer solve still iterates.
        var buffers = _solveBufferPool.Count > 0 ? _solveBufferPool.Pop() : new SolveBuffers();
        try
        {
            RecomputeGrid(grid, buffers);
        }
        finally
        {
            buffers.Clear();
            _solveBufferPool.Push(buffers);
        }
    }

    private void RecomputeGrid(Entity<MapGridComponent> grid, SolveBuffers buffers)
    {
        // CMU14: keep the per-map index current for a grid that changed map since its last solve.
        EnsureGridMapIndexed(grid.Owner);

        // Gather all supports on this grid, indexed by tile, and reset their state.
        // CMU14: pooled; the collections that were allocated here (kept):
        // CMU14: var byTile = new Dictionary<Vector2i, List<Entity<StructuralSupportComponent>>>();
        // CMU14: var previous = new Dictionary<EntityUid, bool>();
        var byTile = buffers.ByTile;
        var previous = buffers.Previous;

        _staleIndexedSupports.Clear();
        if (_supportsByGrid.TryGetValue(grid.Owner, out var indexedSupports))
        {
            foreach (var uid in indexedSupports)
            {
                if (!TryComp<StructuralSupportComponent>(uid, out var comp) ||
                    !TryComp(uid, out TransformComponent? xform) ||
                    !xform.Anchored || xform.GridUid != grid.Owner)
                {
                    _staleIndexedSupports.Add(uid);
                    continue;
                }

                var tile = _map.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);
                if (!byTile.TryGetValue(tile, out var supports))
                {
                    supports = buffers.RentList(); // CMU14: was new List<Entity<StructuralSupportComponent>>()
                    byTile.Add(tile, supports);
                }

                supports.Add((uid, comp));
                previous[uid] = comp.Supported;
                comp.Supported = false;
            }
        }

        // Lifecycle/parent events normally keep the index exact. Repair any stale entries discovered here so an
        // unusual deletion/reparent sequence cannot leave permanent garbage or omit the destination grid.
        foreach (var uid in _staleIndexedSupports)
        {
            RemoveIndexedSupport(uid, out _);
            if (!Deleted(uid) && HasComp<StructuralSupportComponent>(uid))
                ReindexSupport(uid);
        }

        // CMU14: the level above re-solves only when what it rests on may have changed (GraphAudit ECS-01).
        var projectionChanged = _projectionChangedGrids.Remove(grid.Owner);

        if (byTile.Count == 0)
        {
            // Removing the last beam is the most important change to propagate. Returning here without dirtying
            // the level above left its previously-supported floor markers alive forever.
            // CMU14: the removal marked this grid's projection as changed; the call that was here (kept): MarkAboveDirty(grid.Owner);
            if (projectionChanged)
                MarkAboveDirty(grid.Owner);
            return;
        }

        // Seed the flood from anchors.
        // CMU14: pooled; the collections that were allocated here (kept):
        // CMU14: var queue = new Queue<(Vector2i Tile, int Budget)>();
        // CMU14: var best = new Dictionary<Vector2i, int>();
        var queue = buffers.Queue;
        var best = buffers.Best;

        foreach (var (tile, supports) in byTile)
        {
            foreach (var ent in supports)
            {
                if (!TryGetSeedBudget(ent, grid, tile, out var budget))
                    continue;

                if (!best.TryGetValue(tile, out var existing) || existing < budget)
                {
                    best[tile] = budget;
                    queue.Enqueue((tile, budget));
                }
            }
        }

        // Flood.
        while (queue.TryDequeue(out var node))
        {
            // A stale entry (a better budget was queued later) - skip.
            if (!best.TryGetValue(node.Tile, out var cur) || cur != node.Budget)
                continue;

            if (byTile.TryGetValue(node.Tile, out var here))
            {
                foreach (var ent in here)
                    ent.Comp.Supported = true;
            }

            foreach (var dir in Cardinals)
            {
                var next = node.Tile + dir;
                if (!byTile.TryGetValue(next, out var nextSupports))
                    continue;

                // Every colocated support participates, but crossing the tile always consumes one unit.
                // Girders project their span to the level above in TryGetSupportSpanBelow; they deliberately
                // do not relay support horizontally on this level.
                var nextBudget = node.Budget - 1;
                foreach (var nextSupport in nextSupports)
                {
                    if (nextSupport.Comp.IsAnchor && !nextSupport.Comp.IsVerticalSupport)
                        nextBudget = Math.Max(nextBudget, nextSupport.Comp.CantileverSpan);
                }

                if (nextBudget < 0)
                    continue;

                if (best.TryGetValue(next, out var seen) && seen >= nextBudget)
                    continue;

                best[next] = nextBudget;
                queue.Enqueue((next, nextBudget));
            }
        }

        // Report and schedule/cancel collapses. Only UPPER z-levels (depth > 0) are ever collapsed by the support
        // graph - the ground and everything below it rest on real ground and are handled by cave-ins instead, so
        // a ground/underground structure must never be flung around by this system (that was the "constantly
        // collapsing regardless of support" bug). Scheduling is based on the CURRENT unsupported state, not just
        // the supported->unsupported transition, so a structure that is unsupported from the moment it is built
        // still collapses (that was the "actual lack of support does not collapse" bug).
        var now = _timing.CurTime;
        var gridMap = Transform(grid.Owner).MapUid; // CMU14
        var isUpperLevel = !IsGroundOrBelow(gridMap);
        var scheduled = 0; // CMU14
        var sparedAuthored = 0; // CMU14

        foreach (var (tile, supports) in byTile)
        {
            foreach (var ent in supports)
            {
                // CMU14: a support that projects upward and flipped changes what the level above rests on.
                if (!projectionChanged && previous[ent.Owner] != ent.Comp.Supported && ProjectsUpward(ent))
                    projectionChanged = true;

                if (!isUpperLevel || ent.Comp.Supported)
                {
                    // Ground/underground, or genuinely supported: never pending.
                    _pendingUnsupported.Remove(ent.Owner);
                    continue;
                }

                // Upper-z and currently unsupported: schedule a collapse if not already counting down.
                if (!_pendingUnsupported.ContainsKey(ent.Owner))
                {
                    // CMU14: the support graph never schedules a collapse on an authored cell (GraphAudit FLOW-01).
                    if (gridMap is { } authoredMap && IsAuthoredCell(authoredMap, grid, tile))
                    {
                        sparedAuthored++;
                        continue;
                    }

                    scheduled++; // CMU14
                    var collapseAt = now + TimeSpan.FromSeconds(CollapseWarningSeconds);
                    _pendingUnsupported[ent.Owner] = new PendingCollapse(collapseAt, grid.Owner, tile);
                    _unsupportedDeadlines.Enqueue(ent.Owner, collapseAt);

                    // Popup only on the supported -> unsupported transition, to avoid spamming every recompute.
                    if (previous[ent.Owner])
                    {
                        Log.Info($"[zsupport] {ToPrettyString(ent.Owner)} at {tile} lost structural support.");
                        _popup.PopupCoordinates(
                            Loc.GetString("au-zsupport-unsupported"),
                            Transform(ent.Owner).Coordinates,
                            PopupType.MediumCaution);
                    }
                }
            }
        }

        // CMU14: one structured line per recompute that scheduled or refused collapses, never one per structure.
        if (scheduled > 0 || sparedAuthored > 0)
        {
            Log.Info($"[zsupport] collapse-scheduled path=support map={ToPrettyString(gridMap)} grid={ToPrettyString(grid.Owner)} " +
                     $"depth={DepthOf(gridMap)} localized={IsLocalizedLevel(gridMap)} scheduled={scheduled} " +
                     $"sparedAuthored={sparedAuthored} pending={PendingCollapseCount(grid.Owner)} warning={CollapseWarningSeconds}s");
        }

        // The level directly ABOVE depends on this one (beams here hold up its floors), so queue its supports for
        // a recompute whenever this grid changes. Propagation is unconditional: a destroyed beam may not flip any
        // Supported state on its own level yet still unroot the floor it was holding up one level higher. This
        // walks one level up per change and stops at the top (or at a level with no supports), so it terminates.
        // CMU14: propagation is now conditional: a destroyed beam is a removal, which marks the projection as
        // changed; otherwise only a flip of a vertical, anchor or wall support can change the level above.
        // The call that was here (kept): MarkAboveDirty(grid.Owner);
        if (projectionChanged)
            MarkAboveDirty(grid.Owner);
    }

    /// <summary>
    /// Queues every support on the z-level directly above <paramref name="grid"/> for a recompute, so upper-z
    /// structures re-evaluate when the level below them changes (a destroyed column unroots what it held up).
    /// </summary>
    private void MarkAboveDirty(EntityUid grid)
    {
        var mapUid = Transform(grid).MapUid;
        if (mapUid == null || !_zMapQuery.TryComp(mapUid.Value, out var z) || z.MapAbove is not { } above)
            return;

        if (!_supportGridsByMap.TryGetValue(above, out var grids))
            return;

        foreach (var supportGrid in grids)
        {
            // CMU14: a grid still to solve later in this pass will read this level's fresh state anyway.
            if (IsStillOnMap(supportGrid, above) && !_pendingThisPass.Contains(supportGrid))
                _dirtyGrids.Add(supportGrid);
        }

        FlushGridsThatChangedMap(); // CMU14
    }

    /// <summary>
    /// Handles an upper-z structure that remained unsupported past its warning window: the floor gives way. The
    /// tile it sits on is pulled out and EVERYTHING anchored on that tile - the structure itself, the floor's
    /// invisible support marker, and anything else built there - falls to the level directly below (where there
    /// is now no floor under it). Tile-floor markers are deleted (which removes their floor tile); the structure
    /// also gets a small horizontal shove so it tumbles as it drops. This is the "tiles and entities fall when an
    /// upper level collapses" behaviour.
    /// </summary>
    private void CollapseUnsupportedStructure(EntityUid uid)
    {
        Log.Info($"[zsupport] {ToPrettyString(uid)} lost structural support - the floor gives way.");

        var xform = Transform(uid);
        var coords = xform.Coordinates;
        var mapUid = xform.MapUid;

        // Accountability: log the collapse and alert admins, attributing it to the nearest player on the level
        // (most likely the one who knocked out the support below). Throttled per map so a whole floor caving in
        // is one alert, not dozens.
        if (mapUid is { } collapseMap)
        {
            var now = _timing.CurTime;
            _collapsesSinceAlert[collapseMap] = _collapsesSinceAlert.GetValueOrDefault(collapseMap) + 1; // CMU14
            if (!_nextCollapseAlert.TryGetValue(collapseMap, out var next) || now >= next)
            {
                _nextCollapseAlert[collapseMap] = now + CollapseAlertCooldown;
                var worldPos = _transform.ToMapCoordinates(coords).Position;
                var culprit = DescribeCulprit(collapseMap, worldPos, now);

                // CMU14: structured, throttled with the admin alert; region counts collapses since the previous line.
                _collapsesSinceAlert.Remove(collapseMap, out var region);
                Log.Info($"[zsupport] collapse-executed path=support map={ToPrettyString(collapseMap)} depth={DepthOf(collapseMap)} " +
                         $"localized={IsLocalizedLevel(collapseMap)} region={region} capHit=n/a " +
                         $"pending={(xform.GridUid is { } pendingGrid ? PendingCollapseCount(pendingGrid) : 0)} " +
                         $"origin={ToPrettyString(uid)} culprit={culprit}");
                _adminLog.Add(LogType.Action, LogImpact.High,
                    $"Upper z-level collapse: {ToPrettyString(uid)} lost structural support on {ToPrettyString(collapseMap)}; likely caused by {culprit}.");
                _chat.SendAdminAlert(Loc.GetString("au-zsupport-admin-alert", ("culprit", culprit)));
            }
        }

        // RUMC Floor Begin: a built floor that gives way brings its own material down (the item of the floor tile), not plain rubble.
        EntProtoId? floorMaterial = null;
        if (xform.GridUid is { } floorGridUid && _gridQuery.TryComp(floorGridUid, out var floorGrid))
        {
            var floorTile = _map.GetTileRef(floorGridUid, floorGrid, coords);
            if (!floorTile.Tile.IsEmpty && _tileDefs[floorTile.Tile.TypeId] is ContentTileDefinition floorDef)
                floorMaterial = floorDef.ItemDropPrototypeName;
        }

        // RUMC End
        // RUMC: the call that was here (kept): PlayCollapseEffects(coords, mapUid);
        PlayCollapseEffects(coords, mapUid, floorMaterial);

        var belowMap = mapUid != null && _zMapQuery.TryComp(mapUid.Value, out var zMap) ? zMap.MapBelow : null;

        if (xform.GridUid is { } gridUid && _gridQuery.TryComp(gridUid, out var grid))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, coords);
            DropTileAndContents((gridUid, grid), tile, belowMap);
        }
        else
        {
            // No grid (shouldn't happen for a built structure) - just stop tracking it.
            RemComp<StructuralSupportComponent>(uid);
        }
    }

    /// <summary>
    /// Pulls the floor <paramref name="tile"/> out and drops everything anchored on it to <paramref name="belowMap"/>
    /// at the same world position. Tile-floor markers are deleted (removing their tile); staircases are spared;
    /// other structures are unanchored, un-tracked, moved down, and given a small tumble shove.
    /// </summary>
    private void DropTileAndContents(Entity<MapGridComponent> grid, Vector2i tile, EntityUid? belowMap)
    {
        MapComponent? belowMapComp = null;
        if (belowMap != null)
            TryComp(belowMap.Value, out belowMapComp);

        var worldPos = _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, tile)).Position;

        // Snapshot first: unanchoring/moving mutates the grid's anchored set.
        // CMU14: a pooled list (GraphAudit ECS-04); the line that was here (kept):
        // CMU14: var anchored = new List<EntityUid>(_map.GetAnchoredEntities(grid.Owner, grid.Comp, tile));
        var anchored = RentEntityList();
        foreach (var uid in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
            anchored.Add(uid);
        // CMU14: a multi-tile structure covering this tile falls with it, once (GraphAudit FLOW-06).
        _footprint.AddCollapsingOwners(anchored);
        foreach (var ent in anchored)
        {
            // The floor's invisible support marker: deleting it removes its tile (TileApplierSystem) - it has no
            // body to fall, so don't move it.
            if (HasComp<TileFloorSupportComponent>(ent))
            {
                if (TryComp(ent, out TransformComponent? supportXform) && supportXform.Anchored)
                    _transform.Unanchor(ent, supportXform);

                QueueDel(ent);
                continue;
            }

            // Never drop a staircase, and never drop an indestructible map-border wall.
            if (HasComp<CMUZLevelHighGroundComponent>(ent) || IsIndestructibleWall(ent))
                continue;

            // RUMC: a pit is part of the ground: when the tile under it collapses it is gone, it does not fall.
            if (HasComp<ZDestroyedOnCollapseComponent>(ent))
            {
                QueueDel(ent);
                continue;
            }

            if (TryComp(ent, out TransformComponent? exf) && exf.Anchored)
                _transform.Unanchor(ent, exf);

            // RUMC: on one level a thing falls only once.
            if (!_landing.TryMarkFall(ent, Transform(ent).MapUid))
                continue;

            // No longer a structural participant once it has broken loose.
            RemComp<StructuralSupportComponent>(ent);

            if (belowMapComp != null)
            {
                var offset = _random.NextAngle().ToVec() * _random.NextFloat(0.2f, 0.8f);

                // RUMC Landing Begin: it falls on until there is a solid tile under it, not only to the next level.
                // RUMC was (kept): _transform.SetMapCoordinates(ent, new MapCoordinates(worldPos + offset, belowMapComp.MapId));
                var landingMap = belowMap != null ? _landing.FindLandingMap(belowMap.Value, worldPos + offset) : default;
                var landingMapId = belowMap != null && TryComp<MapComponent>(landingMap, out var landingComp) ? landingComp.MapId : belowMapComp.MapId;
                _transform.SetMapCoordinates(ent, new MapCoordinates(worldPos + offset, landingMapId));
                _throwing.TryThrow(ent, offset, baseThrowSpeed: 4f);

                // Broken walls remain inert when later debris or generated rock reaches their landing spot.
                if (_tag.HasTag(ent, WallTag) || _landing.LandsInsideSolid(landingMap, worldPos + offset))
                    MakeFallenDebrisNonHard(ent);
                // RUMC End
            }
        }

        ReturnEntityList(anchored); // CMU14

        // RUMC Loose Begin: what has fallen before and lies loose here falls again with the floor: as many times as there are floors to give way.
        if (belowMap != null && belowMapComp != null)
        {
            // CMU14: pooled; the line that was here (kept): var loose = new List<EntityUid>();
            var loose = RentEntityList();
            _landing.GetFallenDebrisOnTile(grid.Owner, grid.Comp, tile, loose);
            foreach (var ent in loose)
            {
                if (!_landing.TryMarkFall(ent, Transform(ent).MapUid))
                    continue;

                var offset = _random.NextAngle().ToVec() * _random.NextFloat(0.2f, 0.8f);
                var landingMap = _landing.FindLandingMap(belowMap.Value, worldPos + offset);
                var landingMapId = TryComp<MapComponent>(landingMap, out var landingComp) ? landingComp.MapId : belowMapComp.MapId;
                _transform.SetMapCoordinates(ent, new MapCoordinates(worldPos + offset, landingMapId));
                if (_tag.HasTag(ent, WallTag) || _landing.LandsInsideSolid(landingMap, worldPos + offset))
                    MakeFallenDebrisNonHard(ent);
            }

            ReturnEntityList(loose); // CMU14
        }

        // Pull the floor tile out so the floor visibly collapses (does nothing if it was a void tile already).
        _map.SetTile(grid.Owner, grid.Comp, tile, Tile.Empty);
    }

    /// <summary>True if a player-built floor marker (TileFloorSupport) is anchored on this tile - i.e. the floor
    /// tile was laid by a player, not authored by a mapper.</summary>
    private bool HasPlayerFloorMarker(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var anchored in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
        {
            if (HasComp<TileFloorSupportComponent>(anchored))
                return true;
        }

        return false;
    }

    /// <summary>An indestructible map-border wall: tagged as a wall but with no Damageable at all (the
    /// CMBaseWallInvincible family). These are map boundaries and must never fall or be moved.</summary>
    private bool IsIndestructibleWall(EntityUid uid)
    {
        return _tag.HasTag(uid, WallTag) && !HasComp<DamageableComponent>(uid);
    }

    /// <summary>Turns a structure that has fallen through a collapsed floor into inert rubble: every fixture's
    /// collision layer/mask is zeroed (non-hard alone still raises contact events, so bullets would still "hit"
    /// the fallen wall - with no collision bits projectiles pass straight through and cave rocks never grind
    /// contacts against it). The networked ZFallenDebris marker records that it has fallen (it may fall again, as often as the floor under it gives way).</summary>
    private void MakeFallenDebrisNonHard(EntityUid uid)
    {
        EnsureComp<ZFallenDebrisComponent>(uid);

        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            _physics.SetHard(uid, fixture, false, manager: fixtures);
            _physics.SetCollisionLayer(uid, id, fixture, 0, manager: fixtures);
            _physics.SetCollisionMask(uid, id, fixture, 0, manager: fixtures);
        }
    }

    /// <summary>A crash sound at the collapse spot plus a brief vignette for nearby players.</summary>
    // RUMC: it takes the material of the floor too; the signature that was here (kept): private void PlayCollapseEffects(EntityCoordinates coords, EntityUid? mapUid)
    private void PlayCollapseEffects(EntityCoordinates coords, EntityUid? mapUid, EntProtoId? floorMaterial)
    {
        // A bad audio path must never crash the support tick.
        try
        {
            _audio.PlayPvs(CollapseSound, coords);
        }
        catch (Exception e)
        {
            Log.Warning($"[zsupport] collapse sfx failed: {e.Message}");
        }

        if (mapUid == null)
            return;

        // Local effect only: one grey vignette blink for players near the collapse, not map-wide.
        // 🔧 TUNABLE: effect radius in tiles.
        const float effectRange = 33f;
        var collapsePos = _transform.ToMapCoordinates(coords).Position;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out var actorXform))
        {
            if (actorXform.MapUid != mapUid)
                continue;

            if ((_transform.GetWorldPosition(uid) - collapsePos).Length() > effectRange)
                continue;

            RaiseNetworkEvent(new ZCollapseVignetteEvent(), actor.PlayerSession);
        }

        // Debris rains onto the level directly below, with its own thud, so the cave-in reads on both
        // levels (rubble actually lands where it would fall).
        if (TryComp<CMUZLevelMapComponent>(mapUid.Value, out var zMap) &&
            zMap.MapBelow is { } below &&
            TryComp<MapComponent>(below, out var belowMap))
        {
            var worldPos = _transform.ToMapCoordinates(coords).Position;
            var belowCoords = new MapCoordinates(worldPos, belowMap.MapId);

            // RUMC Material Begin: the material of the floor comes down with it, on the first solid tile below.
            // RUMC was (kept): var debris = Spawn(DebrisProto, belowCoords);
            var landingMap = _landing.FindLandingMap(below, worldPos);
            var landingCoords = TryComp<MapComponent>(landingMap, out var landingMapComp) ? new MapCoordinates(worldPos, landingMapComp.MapId) : belowCoords;
            var landed = _transform.ToCoordinates(landingCoords);
            if (floorMaterial is { } material)
                Spawn(material, landed);
            // RUMC End

            try
            {
                // RUMC: the sound is played at the place it landed; the call that was here (kept): _audio.PlayPvs(CollapseSound, debris);
                _audio.PlayPvs(CollapseSound, landed);
            }
            catch (Exception e)
            {
                Log.Warning($"[zsupport] below-level collapse sfx failed: {e.Message}");
            }

            // Same local radius as the level above: only players near where the debris lands feel the thud.
            var belowActors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (belowActors.MoveNext(out var uid, out var actor, out var actorXform))
            {
                if (actorXform.MapUid != below)
                    continue;

                if ((_transform.GetWorldPosition(uid) - worldPos).Length() > 33f)
                    continue;

                RaiseNetworkEvent(new ZCollapseVignetteEvent(), actor.PlayerSession);
            }
        }
    }

    /// <summary>
    /// Decides whether a support tile is a ROOT of the graph and, if so, the budget (cantilever reach) to seed
    /// the flood with. Roots are:
    ///  - explicit anchors (seed = own span);
    ///  - a support resting on a solid floor tile on the GROUND level or below (depth &lt;= 0) - the surface and
    ///    everything under it sit on real ground, so they are stable on a solid tile (this also keeps the
    ///    underground itself from collapsing through the support graph; cave-ins are handled separately). Seed =
    ///    own span;
    ///  - the UPPER-Z rule: any other support that has a vertical support beam or wall directly beneath it on
    ///    the level below is held up by that structure, and the seed budget is that structure's span.
    /// Vertical girders receive a zero budget when rooted: this keeps the girder itself stable without allowing
    /// it to support adjacent tiles on its own level. Its full span is consumed only by the level above.
    /// Lower/underground levels never collapse from "missing" support because they root on solid ground.
    /// </summary>
    private bool TryGetSeedBudget(Entity<StructuralSupportComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, out int budget)
    {
        budget = 0;
        var mapUid = Transform(grid.Owner).MapUid;

        if (ent.Comp.IsAnchor)
        {
            budget = OwnLevelSeedBudget(ent.Comp.IsVerticalSupport, ent.Comp.CantileverSpan);
            return true;
        }

        var onSolid = _map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
        if (onSolid && IsGroundOrBelow(mapUid))
        {
            budget = OwnLevelSeedBudget(ent.Comp.IsVerticalSupport, ent.Comp.CantileverSpan);
            return true;
        }

        // A solid floor tile on an UPPER level that carries no player-built floor marker is mapper-authored map
        // content (e.g. the USS Bush's upper decks). Authored floors are self-supporting: anything crafted on
        // them roots there and must never cave in. Only PLAYER-built upper floors (which always carry a
        // TileFloorSupport marker) need a beam below.
        if (onSolid && !IsGroundOrBelow(mapUid) && !HasPlayerFloorMarker(grid, tile))
        {
            budget = OwnLevelSeedBudget(ent.Comp.IsVerticalSupport, ent.Comp.CantileverSpan);
            return true;
        }

        // Upper-z: held up by a beam directly below - reach is the BEAM's span (its quality tier).
        if (mapUid != null &&
            _zMapQuery.TryComp(mapUid.Value, out var zMap) &&
            zMap.MapBelow is { } below &&
            TryGetSupportSpanBelow(below, _transform.GetWorldPosition(ent.Owner), out var belowSpan))
        {
            budget = OwnLevelSeedBudget(ent.Comp.IsVerticalSupport, belowSpan);
            return true;
        }

        return false;
    }

    private static int OwnLevelSeedBudget(bool isVerticalSupport, int sourceBudget)
        => isVerticalSupport ? 0 : sourceBudget;

    /// <summary>
    /// True if the level is the ground/surface (depth 0) or underground (depth &lt; 0), i.e. NOT an upper z-level.
    /// A map with no z-level data is a plain single ground map. Upper levels (depth &gt; 0) must be held up by a
    /// beam on the level below instead of by resting on a floor.
    /// </summary>
    private bool IsGroundOrBelow(EntityUid? mapUid)
    {
        if (mapUid == null)
            return true;

        return !_zMapQuery.TryComp(mapUid.Value, out var z) || z.Depth <= 0;
    }

    /// <summary>
    /// Attributes a collapse: prefers the last player who damaged a support on this level within the attribution
    /// window (the exact culprit, e.g. whoever shot the beam out); otherwise falls back to the nearest player.
    /// </summary>
    private string DescribeCulprit(EntityUid mapUid, Vector2 worldPos, TimeSpan now)
    {
        if (_lastSupportDamager.TryGetValue(mapUid, out var last) &&
            now - last.Time <= AttributionWindow &&
            !Deleted(last.Culprit))
        {
            return ToPrettyString(last.Culprit).ToString();
        }

        return DescribeNearestPlayer(mapUid, worldPos);
    }

    /// <summary>
    /// Best-effort attribution for a collapse: the nearest player on <paramref name="mapUid"/> to
    /// <paramref name="worldPos"/> (most likely the one who removed the support). Returns a pretty string for the
    /// admin log / alert, or a "no nearby player" note if the level is empty.
    /// </summary>
    private string DescribeNearestPlayer(EntityUid mapUid, Vector2 worldPos)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            var dist = (_transform.GetWorldPosition(uid) - worldPos).LengthSquared();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = uid;
            }
        }

        return best is { } b ? ToPrettyString(b).ToString() : "no nearby player";
    }

    /// <summary>
    /// Returns the largest cantilever span of any vertical support, anchor, or wall at the same world position
    /// on the level below, or false if there is none. That span becomes the seed budget for the tile above.
    /// </summary>
    private bool TryGetSupportSpanBelow(EntityUid belowMap, Vector2 worldPos, out int span)
    {
        span = 0;
        if (!TryComp<MapComponent>(belowMap, out var belowMapComp))
            return false;

        var coords = new MapCoordinates(worldPos, belowMapComp.MapId);
        if (!_map.TryFindGridAt(coords, out var belowGridUid, out var belowGrid))
            return false;

        var tile = _map.TileIndicesFor(belowGridUid, belowGrid, coords);
        var best = -1;
        foreach (var anchored in _map.GetAnchoredEntities(belowGridUid, belowGrid, tile))
        {
            if (TryComp<StructuralSupportComponent>(anchored, out var sup))
            {
                // A constructed wall/beam in its warning window no longer has a structural load path.
                // Its still-present physical body must not reset the warning independently on every storey.
                if (!sup.Supported)
                    continue;

                if (sup.IsVerticalSupport || sup.IsAnchor)
                    best = Math.Max(best, sup.CantileverSpan);
            }

            if (HasComp<ZLevelWallSupportComponent>(anchored))
                best = Math.Max(best, ZLevelWallSupportComponent.CantileverSpan);
        }

        if (best < 0)
            return false;

        span = best;
        return true;
    }
}

/// <summary>
/// Relays a load-bearing support's removal to the cave-in system. Robust only permits one directed subscription
/// for each component/event pair, which is owned by <see cref="ZLevelSupportSystem"/>.
/// </summary>
[ByRefEvent]
public readonly record struct ZCaveSupportRemovedEvent(EntityUid Support);

/// <summary>A wall's anchored virtual support changed; consumers snapshot its location before teardown completes.</summary>
[ByRefEvent]
public readonly record struct ZLevelSupportChangedEvent(EntityUid Support);

/// <summary>Relays player damage to a structural-only cave support for cave-in attribution.</summary>
[ByRefEvent]
public readonly record struct ZCaveSupportDamagedEvent(EntityUid Support, EntityUid Origin);
