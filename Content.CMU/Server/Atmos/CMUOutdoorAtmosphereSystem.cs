using System.Diagnostics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Relaxes sky-exposed tile air on planet maps toward the map's immutable
/// MapAtmosphere mixture. The sky is an infinite reservoir: excess is carried
/// off, deficits refilled. This is the planet counterpart of a breach
/// equalizing with space, and upper z-levels are only ever read as geometry.
/// Coverage comes from the roof data (the CMUSharedRoofSystem cascade plus
/// IsRoof entities), the same source weather uses, so roofed rooms are never
/// normalized on any deck.
/// </summary>
public sealed class CMUOutdoorAtmosphereSystem : EntitySystem
{
    // Seconds between relaxation passes. The strength is a CVar so recovery
    // speed can be tuned live.
    private const float PassPeriod = 0.5f;

    // Per-pass time budget. Work that does not fit resumes next pass through
    // the cursors, so oversized maps slow the sweep instead of spiking a tick.
    private const float PassBudgetMs = 3f;

    private const int BudgetCheckInterval = 64;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<GridAtmosphereComponent> _atmosQuery = default!;
    private EntityQuery<MapGridComponent> _gridQuery = default!;
    private EntityQuery<MapAtmosphereComponent> _mapAtmosQuery = default!;
    private EntityQuery<GasTileOverlayComponent> _overlayQuery = default!;
    private EntityQuery<RoofComponent> _roofQuery = default!;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery = default!;

    private sealed class Cache
    {
        public readonly HashSet<Vector2i> Outdoor = new();

        // Order plus Position give a resumable cursor over a mutating set.
        public readonly List<Vector2i> Order = new();
        public readonly Dictionary<Vector2i, int> Position = new();

        // Indices queued for re-evaluation, applied at pass start.
        public readonly List<Vector2i> Pending = new();

        public int PendingCursor;
        public int RelaxCursor;
        public bool FullQueued;
    }

    private readonly Dictionary<EntityUid, Cache> _caches = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly List<EntityUid> _passList = new();
    private float _accumulator;
    private int _gridCursor;

    public override void Initialize()
    {
        _atmosQuery = GetEntityQuery<GridAtmosphereComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mapAtmosQuery = GetEntityQuery<MapAtmosphereComponent>();
        _overlayQuery = GetEntityQuery<GasTileOverlayComponent>();
        _roofQuery = GetEntityQuery<RoofComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();

        SubscribeLocalEvent<TransformComponent, MapInitEvent>(OnGridInit);
        SubscribeLocalEvent<TransformComponent, EntParentChangedMessage>(OnGridParentChanged);
        SubscribeLocalEvent<MapGridComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<IsRoofComponent, ComponentStartup>(OnRoofStartup);
        SubscribeLocalEvent<IsRoofComponent, ComponentShutdown>(OnRoofShutdown);
        SubscribeLocalEvent<IsRoofComponent, AnchorStateChangedEvent>(OnRoofAnchorChanged);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnZNetworkUpdated);
    }

    public override void Update(float frameTime)
    {
        if (!_cfg.GetCVar(CCVars.CMUOutdoorAtmosRelax))
            return;

        _accumulator += frameTime;
        if (_accumulator < PassPeriod)
            return;

        _accumulator = 0f;

        var rate = Math.Clamp(_cfg.GetCVar(CCVars.CMUOutdoorAtmosRelaxRate), 0f, 1f);
        if (rate <= 0f)
            return;

        _passList.Clear();
        _passList.AddRange(_caches.Keys);
        if (_passList.Count == 0)
            return;

        _stopwatch.Restart();

        var exhausted = false;
        for (var i = 0; i < _passList.Count; i++)
        {
            var grid = _passList[(_gridCursor + i) % _passList.Count];
            if (!RunPass(grid, rate))
            {
                _gridCursor = (_gridCursor + i) % _passList.Count;
                exhausted = true;
                break;
            }
        }

        if (!exhausted)
            _gridCursor = 0;
    }

    private bool RunPass(EntityUid grid, float rate)
    {
        if (TerminatingOrDeleted(grid))
        {
            _caches.Remove(grid);
            return true;
        }

        if (!_atmosQuery.TryComp(grid, out var atmos)
            || atmos.LifeStage >= ComponentLifeStage.Stopping
            || !atmos.Simulated
            || Paused(grid))
            return true;

        // Gate is live per pass: a map flipped to space stops relaxing its
        // grids without any further notice.
        var mapUid = Transform(grid).MapUid;
        if (mapUid is not { } map
            || !_mapAtmosQuery.TryComp(map, out var mapAtmos)
            || mapAtmos.Space)
            return true;

        var cache = _caches[grid];
        return FlushPending(grid, cache) && Relax(grid, map, mapAtmos, atmos, cache, rate);
    }

    /// <summary>
    /// Applies queued membership re-evaluations. Deferred to pass start so the
    /// roof cascade from a tile change settles before it is read.
    /// </summary>
    private bool FlushPending(EntityUid grid, Cache cache)
    {
        while (cache.PendingCursor < cache.Pending.Count)
        {
            var indices = cache.Pending[cache.PendingCursor++];
            ApplyMembership(cache, indices, IsSkyExposed(grid, indices));

            if (cache.PendingCursor % BudgetCheckInterval != 0)
                continue;

            if (_stopwatch.Elapsed.TotalMilliseconds >= PassBudgetMs)
                return false;
        }

        cache.Pending.Clear();
        cache.PendingCursor = 0;
        cache.FullQueued = false;
        return true;
    }

    /// <summary>
    /// Whether a grid tile sits under open sky: a real, non-isSpace tile with
    /// no roof above it. The relaxation pass and any device gating on outdoor
    /// placement share this one rule. Evaluated directly, never from the
    /// cache, so callers cannot observe a half-rebuilt sweep.
    /// </summary>
    public bool IsSkyExposed(EntityUid grid, Vector2i indices)
    {
        if (!_gridQuery.TryComp(grid, out var mapGrid))
            return false;

        // No grid tile means a map-atmosphere tile: already coupled to the
        // immutable reservoir, not our business.
        if (!_map.TryGetTile(mapGrid, indices, out var tile) || tile.IsEmpty)
            return false;

        // isSpace content tiles self-normalize and must stay immutable.
        if (((ContentTileDefinition) _tileDefs[tile.TypeId]).MapAtmosphere)
            return false;

        return !_roofQuery.TryComp(grid, out var roof)
            || !_roof.IsRooved((grid, mapGrid, roof), indices);
    }

    private void ApplyMembership(Cache cache, Vector2i indices, bool outdoor)
    {
        if (outdoor)
        {
            if (!cache.Outdoor.Add(indices))
                return;

            cache.Position[indices] = cache.Order.Count;
            cache.Order.Add(indices);
            return;
        }

        if (!cache.Outdoor.Remove(indices))
            return;

        var i = cache.Position[indices];
        cache.Position.Remove(indices);
        var last = cache.Order[^1];
        cache.Order[i] = last;
        cache.Order.RemoveAt(cache.Order.Count - 1);
        if (i < cache.Order.Count)
            cache.Position[last] = i;

        // Keep the relax cursor pointing at unvisited tiles after the swap.
        if (cache.RelaxCursor > i)
            cache.RelaxCursor--;
    }

    private bool Relax(
        EntityUid grid,
        EntityUid map,
        MapAtmosphereComponent mapAtmos,
        GridAtmosphereComponent atmos,
        Cache cache,
        float rate)
    {
        GasTileOverlayComponent? overlay = _overlayQuery.CompOrNull(grid);
        var gridEnt = (grid, atmos, overlay);
        var mapEnt = (map, mapAtmos);
        var target = mapAtmos.Mixture;
        var visited = 0;

        for (var i = cache.RelaxCursor; i < cache.Order.Count; i++)
        {
            cache.RelaxCursor = i + 1;
            var indices = cache.Order[i];

            var air = _atmosphere.GetTileMixture(gridEnt, mapEnt, indices);
            if (air is not { Immutable: false })
                continue;

            var changed = false;
            for (var g = 0; g < Atmospherics.TotalNumberOfGases; g++)
            {
                var delta = (target.GetMoles(g) - air.GetMoles(g)) * rate;
                if (MathF.Abs(delta) >= Atmospherics.GasMinMoles)
                    changed = true;
                air.SetMoles(g, air.GetMoles(g) + delta);
            }

            var tempDelta = (target.Temperature - air.Temperature) * rate;
            if (MathF.Abs(tempDelta) >= Atmospherics.MinimumTemperatureDeltaToConsider)
                changed = true;
            air.Temperature += tempDelta;

            // Only meaningfully changed tiles activate and refresh the gas
            // overlay. Near-normal tiles keep converging silently and stay
            // inactive, which keeps the steady state cheap.
            if (changed)
                _atmosphere.GetTileMixture(gridEnt, mapEnt, indices, true);

            if (++visited % BudgetCheckInterval != 0)
                continue;

            if (_stopwatch.Elapsed.TotalMilliseconds >= PassBudgetMs)
                return false;
        }

        cache.RelaxCursor = 0;
        return true;
    }

    /// <summary>
    /// Only grids authored on a planet map participate. A grid arriving later
    /// (dropship, shuttle) never gains a cache, so its sealed interior is left
    /// alone even while landed. Roundstart-authored ships on planet maps need
    /// painted roofs, the same requirement weather already makes.
    /// </summary>
    private Cache? EnsureCache(EntityUid grid)
    {
        if (_caches.TryGetValue(grid, out var cache))
            return cache;

        var mapUid = Transform(grid).MapUid;
        if (mapUid is not { } map
            || !_mapAtmosQuery.TryComp(map, out var mapAtmos)
            || mapAtmos.Space)
            return null;

        cache = new Cache();
        _caches[grid] = cache;
        return cache;
    }

    private void QueueFullRebuild(EntityUid grid, Cache cache)
    {
        if (cache.FullQueued || !_gridQuery.TryComp(grid, out var mapGrid))
            return;

        cache.FullQueued = true;
        cache.Pending.Clear();
        cache.PendingCursor = 0;

        var enumerator = _map.GetAllTiles(grid, mapGrid);
        while (enumerator.MoveNext(out var tile))
            cache.Pending.Add(tile.Value.GridIndices);
    }

    private void OnGridInit(Entity<TransformComponent> ent, ref MapInitEvent args)
    {
        if (!_atmosQuery.HasComp(ent.Owner))
            return;

        if (EnsureCache(ent.Owner) is { } cache)
            QueueFullRebuild(ent.Owner, cache);
    }

    /// <summary>
    /// Re-runs the participation decision for every grid on a map. Network
    /// atmosphere inheritance can turn a space-read map into a planet map
    /// after its grids already initialized and were skipped.
    /// </summary>
    public void RefreshForMap(EntityUid map)
    {
        var query = EntityQueryEnumerator<GridAtmosphereComponent, TransformComponent>();
        while (query.MoveNext(out var grid, out _, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            if (EnsureCache(grid) is { } cache)
                QueueFullRebuild(grid, cache);
        }
    }

    private void OnGridParentChanged(Entity<TransformComponent> ent, ref EntParentChangedMessage args)
    {
        if (!_atmosQuery.HasComp(ent.Owner))
            return;

        if (!_caches.TryGetValue(ent.Owner, out var cache))
            return;

        var mapUid = args.Transform.MapUid;
        if (mapUid is not { } map
            || !_mapAtmosQuery.TryComp(map, out var mapAtmos)
            || mapAtmos.Space)
        {
            // Left the planet: drop the cache. Returning does not recreate it.
            _caches.Remove(ent.Owner);
            return;
        }

        if (mapUid != args.OldMapId)
            QueueFullRebuild(ent.Owner, cache);
    }

    private void OnTileChanged(Entity<MapGridComponent> ent, ref TileChangedEvent args)
    {
        if (_caches.Count == 0 || args.Changes.Length == 0)
            return;

        QueueIndices(ent.Owner, args.Changes);

        // A tile change on a z-level map re-roofs the same column on every
        // deck below; re-evaluate those tiles as well.
        if (!_zMapQuery.TryComp(ent.Owner, out var zMap))
            return;

        foreach (var below in _zLevels.GetAllMapsBelow((ent.Owner, zMap)))
        {
            if (_gridQuery.HasComp(below))
                QueueIndices(below, args.Changes);
        }
    }

    private void QueueIndices(EntityUid grid, TileChangedEntry[] changes)
    {
        if (!_caches.TryGetValue(grid, out var cache))
            return;

        foreach (var change in changes)
            cache.Pending.Add(change.GridIndices);
    }

    private void OnRoofStartup(Entity<IsRoofComponent> ent, ref ComponentStartup args)
        => QueueRoofTile(ent.Owner);

    private void OnRoofShutdown(Entity<IsRoofComponent> ent, ref ComponentShutdown args)
        => QueueRoofTile(ent.Owner);

    private void OnRoofAnchorChanged(Entity<IsRoofComponent> ent, ref AnchorStateChangedEvent args)
        => QueueRoofTile(ent.Owner);

    private void QueueRoofTile(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid
            || !_caches.ContainsKey(grid)
            || !_gridQuery.TryComp(grid, out var mapGrid))
            return;

        _caches[grid].Pending.Add(_map.CoordinatesToTile(grid, mapGrid, xform.Coordinates));
    }

    private void OnZNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        foreach (var member in args.Network.Comp.ZLevels.Values)
        {
            if (member is not { } map || !_gridQuery.HasComp(map))
                continue;

            if (EnsureCache(map) is { } cache)
                QueueFullRebuild(map, cache);
        }
    }
}
