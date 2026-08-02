// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Shuttles.Systems;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 4 (underground half): cave-ins.
///
/// This is the INVERSE of the above-ground overhang graph (<see cref="ZLevelSupportSystem"/>). Underground, the
/// danger is digging a cavern too WIDE: the roof over a dug-out (open) tile is held up only by nearby solid rock
/// and by built pillars (vertical <see cref="StructuralSupportComponent"/>). Any open tile farther than
/// <see cref="ZBuildableMapComponent.MaxRoofSpan"/> from a support has an unstable roof - after an 8 second
/// warning it caves in: the tile is buried in rock, anyone on it takes brute damage, and nearby players get
/// rumble and vignette feedback while the collapse keeps going.
///
/// Counterplay: don't over-mine, or plant pillars in the middle of big caverns - exactly like real mines.
/// </summary>
public sealed class ZCaveInSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private static readonly TimeSpan WarningTime = TimeSpan.FromSeconds(8);

    // How often the (small) set of dirty + pending tiles is re-evaluated. This is NOT a full scan - it only
    // touches tiles that an event flagged plus tiles already counting down, so it is cheap. The interval just
    // bounds how often warnings advance toward collapse; the maintainer's "every 2s is fine" guidance applies.
    private static readonly TimeSpan EvalInterval = TimeSpan.FromSeconds(1);

    // A triggered cavern collapse buries tiles a batch at a time so it is a big, sustained event.
    private static readonly TimeSpan CollapseStepInterval = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan RumbleInterval = TimeSpan.FromSeconds(0.45);
    private const int TilesPerStep = 6;

    /// <summary>Safety cap on how many tiles one cavern collapse may bury.</summary>
    private const int CollapseTileCap = 600;
    private const float DropshipCollapseDetectionRadius = 1.25f;

    private static readonly Vector2i[] Cardinals =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private TimeSpan _nextEval;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ZGeneratedStoneComponent> _stoneQuery;
    private List<Entity<MapGridComponent>> _overlappingGrids = new();

    // Exact attribution: the last PLAYER to damage something on each underground level (the over-miner), with the
    // time. Used to name the real culprit in the cave-in log/alert instead of just "nearest player".
    private readonly Dictionary<EntityUid, (EntityUid Culprit, TimeSpan Time)> _lastDigger = new();
    private static readonly TimeSpan AttributionWindow = TimeSpan.FromSeconds(15);

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _stoneQuery = GetEntityQuery<ZGeneratedStoneComponent>();

        // Event-driven instead of polling: the ONLY thing that changes a cavern roof's stability is an anchored
        // solid (mined rock / built or destroyed pillar) appearing or disappearing. Hook that and flag a small
        // region dirty, rather than re-scanning every dug tile every second forever (the old TPS sink).
        SubscribeLocalEvent<TransformComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<ShuttleFTLSafetyEvent>(OnShuttleFTLSafety);

        // Load-bearing TERRAIN is indexed by position rather than read from the anchored lookup, because the
        // terrain that needs it (deep water) is deliberately saved unanchored. See ZLoadBearingTerrainComponent.
        SubscribeLocalEvent<ZLoadBearingTerrainComponent, ComponentStartup>(OnLoadBearingTerrainStartup);
        SubscribeLocalEvent<ZLoadBearingTerrainComponent, ComponentShutdown>(OnLoadBearingTerrainShutdown);

        // Attribution entries are keyed by map uid; drop them with the round so stale maps don't accumulate.
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _lastDigger.Clear();
            _loadBearingTerrain.Clear();
            _loadBearingTerrainAt.Clear();
        });
    }

    private void OnShuttleFTLSafety(ref ShuttleFTLSafetyEvent args)
    {
        if (!HasComp<ZCollapseCompromisedComponent>(args.Shuttle))
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("au14-dropship-collapse-compromised");
    }

    /// <summary>Records the last player to deal damage on an underground level (the likely over-miner), for attribution.</summary>
    private void OnDamaged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin || !HasComp<ActorComponent>(origin))
            return;

        if (Transform(ent).MapUid is { } mapUid && _stoneQuery.HasComponent(mapUid))
            _lastDigger[mapUid] = (origin, _timing.CurTime);
    }

    /// <summary>
    /// An anchored entity on an underground stone level changed (a rock was mined away, or a pillar was built or
    /// destroyed). Removing a solid can unstable nearby open tiles; adding one can stabilise them. Flag the tiles
    /// within a roof span of it dirty so the next evaluation pass re-checks just those, not the whole level.
    /// </summary>
    private void OnAnchorChanged(EntityUid uid, TransformComponent xform, ref AnchorStateChangedEvent args)
    {
        // Only a solid being REMOVED (rock mined, pillar destroyed) can destabilise a roof. A solid being ADDED
        // (a pillar built, rocks spawned by generation or during a burial) only ever stabilises, and that case is
        // already handled by the periodic re-check of pending tiles - so ignore anchor-adds and avoid the churn.
        if (args.Anchored)
            return;

        if (xform.MapUid is not { } mapUid || !_stoneQuery.TryComp(mapUid, out var stone))
            return;

        if (xform.GridUid is not { } gridUid || gridUid != stone.StoneGrid || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        // Don't accumulate dirty tiles while the level is mid-collapse; that region is already being handled.
        if (stone.CollapseQueue.Count > 0)
            return;

        var settings = GetSettings(mapUid);
        var span = Math.Max(1, settings.MaxRoofSpan);
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        // The changed solid affects the roof of every open tile within a span of it (and the tile itself, which
        // may have just become open). Diamond (Manhattan) neighbourhood matches HasSupportWithin's metric.
        for (var dx = -span; dx <= span; dx++)
        {
            for (var dy = -span; dy <= span; dy++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) <= span)
                    stone.DirtyTiles.Add(tile + new Vector2i(dx, dy));
            }
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        // Advance any in-progress cavern collapses every tick so rumble/vignette feedback stays sustained. This only does
        // work for maps that are actually mid-collapse (CollapseQueue non-empty); the rest are skipped instantly.
        var collapseQuery = EntityQueryEnumerator<ZGeneratedStoneComponent>();
        while (collapseQuery.MoveNext(out var mapUid, out var stone))
        {
            if (stone.CollapseQueue.Count > 0)
                ProcessCollapse((mapUid, stone), now);
        }

        if (now < _nextEval)
            return;

        _nextEval = now + EvalInterval;

        // Re-evaluate ONLY the tiles an event flagged dirty plus the few already counting down toward collapse.
        // A level with no dirty tiles and no pending warnings costs nothing here.
        var evalQuery = EntityQueryEnumerator<ZGeneratedStoneComponent>();
        while (evalQuery.MoveNext(out var mapUid, out var stone))
        {
            EvaluateDirtyAndPending((mapUid, stone), now);
        }
    }

    /// <summary>
    /// Evaluates the dirty tiles (flagged by an anchor change) and the pending tiles (counting down) for one
    /// underground level. This replaces the old "scan every dug tile every second" pass with an event-driven,
    /// O(changed + pending) check.
    /// </summary>
    private void EvaluateDirtyAndPending(Entity<ZGeneratedStoneComponent> stoneMap, TimeSpan now)
    {
        // Don't evaluate a level that is mid-collapse; the dirty flags are reconsidered once it settles.
        if (stoneMap.Comp.CollapseQueue.Count > 0)
            return;

        if (stoneMap.Comp.DirtyTiles.Count == 0 && stoneMap.Comp.PendingCollapse.Count == 0)
            return;

        if (!_gridQuery.TryComp(stoneMap.Comp.StoneGrid, out var grid))
        {
            stoneMap.Comp.DirtyTiles.Clear();
            return;
        }

        var settings = GetSettings(stoneMap.Owner);
        var span = Math.Max(1, settings.MaxRoofSpan);
        var chunkSize = Math.Max(2, settings.ChunkSize);

        // Candidate set = newly-changed tiles + tiles already counting down (so their warning can elapse into a
        // real collapse, and so a tile shored up in time is cleared). Both sets are small.
        var candidates = new HashSet<Vector2i>(stoneMap.Comp.DirtyTiles);
        candidates.UnionWith(stoneMap.Comp.PendingCollapse.Keys);
        stoneMap.Comp.DirtyTiles.Clear();

        foreach (var tile in candidates)
        {
            if (EvaluateTile(stoneMap, (stoneMap.Comp.StoneGrid, grid), tile, span, chunkSize, now))
                return; // a collapse just started; stop evaluating this level this pass
        }
    }

    /// <summary>Returns true if this tile's warning elapsed and kicked off a cavern collapse.</summary>
    private bool EvaluateTile(
        Entity<ZGeneratedStoneComponent> stoneMap,
        Entity<MapGridComponent> grid,
        Vector2i tile,
        int span,
        int chunkSize,
        TimeSpan now)
    {
        var pending = stoneMap.Comp.PendingCollapse;

        // Solid (or not-yet-dug) tiles can't cave in; clear any stale pending state.
        if (IsSolid(grid, tile, stoneMap.Comp.GeneratedChunks, chunkSize))
        {
            pending.Remove(tile);
            return false;
        }

        var stable = HasSupportWithin(grid, tile, span, stoneMap.Comp.GeneratedChunks, chunkSize);

        if (stable)
        {
            // Player shored it up in time - cancel the warning.
            pending.Remove(tile);
            return false;
        }

        if (!pending.TryGetValue(tile, out var collapseAt))
        {
            // First time unstable - start the 8s warning.
            pending[tile] = now + WarningTime;
            _popup.PopupCoordinates(
                Loc.GetString("au-cavein-warning"),
                _map.GridTileToLocal(grid.Owner, grid.Comp, tile),
                PopupType.LargeCaution);
            return false;
        }

        if (now < collapseAt)
            return false;

        // Warning elapsed - collapse the WHOLE cavern this tile belongs to, not just this one tile.
        StartCavernCollapse(stoneMap, grid, tile, chunkSize);
        return true;
    }

    /// <summary>
    /// Flood-fills the connected open region (the cavern) containing <paramref name="origin"/> and queues every
    /// tile in it to be buried - including the still-supported tiles - so the whole cavern caves in, not one tile.
    /// </summary>
    private void StartCavernCollapse(Entity<ZGeneratedStoneComponent> stoneMap, Entity<MapGridComponent> grid, Vector2i origin, int chunkSize)
    {
        if (stoneMap.Comp.CollapseQueue.Count > 0)
            return;

        var settings = GetSettings(stoneMap.Owner);
        var span = Math.Max(1, settings.MaxRoofSpan);

        var region = new List<Vector2i>();
        var seen = new HashSet<Vector2i> { origin };
        var frontier = new Queue<Vector2i>();
        frontier.Enqueue(origin);

        while (frontier.TryDequeue(out var t) && region.Count < CollapseTileCap)
        {
            if (IsSolid(grid, t, stoneMap.Comp.GeneratedChunks, chunkSize))
                continue;

            // Built support beams shut off a spreading collapse like a valve: every open tile within a beam's
            // protected radius is neither buried NOR traversed, so a beam line that fully seals a passage stops
            // the cave-in dead there, and a lone beam still keeps its own protected pocket standing even while
            // the collapse flows around it.
            if (HasBuiltSupportWithin(grid, t, span))
                continue;

            region.Add(t);

            foreach (var dir in Cardinals)
            {
                var n = t + dir;
                if (seen.Add(n))
                    frontier.Enqueue(n);
            }
        }

        if (region.Count == 0)
            return;

        // Accountability: log the cave-in (location + size) and alert admins, attributing it to the nearest
        // player on the level (the most likely over-miner). A whole cavern collapses as ONE event, so this fires
        // once per cave-in, not per tile.
        var originWorld = _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, origin)).Position;
        var culprit = DescribeCulprit(stoneMap.Owner, originWorld);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Underground cave-in started at {origin} on {ToPrettyString(stoneMap.Owner)} ({region.Count} tiles); likely caused by {culprit}.");
        _chat.SendAdminAlert(Loc.GetString("au-cavein-admin-alert", ("count", region.Count), ("culprit", culprit)));

        // Before the roof buries the cavern, fling loose rocks and lay see-through fog for atmosphere.
        ThrowDebris(grid, region, settings);
        SpawnFog(grid, region, settings);

        foreach (var t in region)
            stoneMap.Comp.PendingCollapse.Remove(t);

        // BFS order buries from the centre outward for a nice spreading cave-in.
        stoneMap.Comp.CollapseQueue.AddRange(region);
        stoneMap.Comp.CollapseNextStep = _timing.CurTime;
        stoneMap.Comp.CollapseNextRumble = TimeSpan.Zero;

        // Save the region so we can trigger surface effects when this collapse finishes.
        stoneMap.Comp.LastCollapseRegion.Clear();
        stoneMap.Comp.LastCollapseRegion.AddRange(region);
    }

    /// <summary>Spawns a handful of loose rocks across the doomed cavern and flings them 1-2 tiles in random directions.</summary>
    private void ThrowDebris(Entity<MapGridComponent> grid, List<Vector2i> region, ZBuildableMapComponent settings)
    {
        var count = Math.Min(region.Count, 10);
        for (var i = 0; i < count; i++)
        {
            var tile = region[_random.Next(region.Count)];
            var coords = _map.GridTileToLocal(grid.Owner, grid.Comp, tile);
            var rock = Spawn(settings.RockDebris, coords);

            var direction = _random.NextAngle().ToVec() * _random.NextFloat(1f, 2f);
            _throwing.TryThrow(rock, direction, baseThrowSpeed: 5f);
        }
    }

    /// <summary>Lays see-through fog over roughly every third cavern tile while it collapses.</summary>
    private void SpawnFog(Entity<MapGridComponent> grid, List<Vector2i> region, ZBuildableMapComponent settings)
    {
        for (var i = 0; i < region.Count; i += 3)
            Spawn(settings.CollapseFog, _map.GridTileToLocal(grid.Owner, grid.Comp, region[i]));
    }

    /// <summary>Buries the next batch of queued cavern tiles, damaging anyone caught, with sustained feedback.</summary>
    private void ProcessCollapse(Entity<ZGeneratedStoneComponent> stoneMap, TimeSpan now)
    {
        if (now < stoneMap.Comp.CollapseNextStep)
            return;

        if (!_gridQuery.TryComp(stoneMap.Comp.StoneGrid, out var grid))
        {
            stoneMap.Comp.CollapseQueue.Clear();
            return;
        }

        var settings = GetSettings(stoneMap.Owner);
        var queue = stoneMap.Comp.CollapseQueue;
        var count = Math.Min(TilesPerStep, queue.Count);

        for (var i = 0; i < count; i++)
            BuryTile(stoneMap, (stoneMap.Comp.StoneGrid, grid), queue[i], settings);

        queue.RemoveRange(0, count);

        RumbleAndVignette(stoneMap, now, settings);

        stoneMap.Comp.CollapseNextStep = now + CollapseStepInterval;

        // When the collapse finishes, propagate surface effects to the level above.
        // This only triggers at the END of a cave-in, not continuously, so the ground level scan does not
        // instantly destabilise all maps that happen to have no underground generated yet.
        if (queue.Count == 0 && count > 0)
            TriggerSurfaceEffects(stoneMap, settings);
    }

    private void BuryTile(Entity<ZGeneratedStoneComponent> stoneMap, Entity<MapGridComponent> grid, Vector2i tile, ZBuildableMapComponent settings)
    {
        // Keep a stable platform around any staircase: if a walk-through stair is on this tile or an adjacent
        // one, do not bury it and do not pull the floor out above it. Otherwise the tile a stair drops you onto
        // would vanish and you would instantly fall (and risk clipping through to the level below).
        if (HasStairWithin(grid, tile, 1))
            return;

        var coords = _map.GridTileToLocal(grid.Owner, grid.Comp, tile);

        // Brute damage to anyone caught under the falling roof.
        var brute = new DamageSpecifier();
        brute.DamageDict.Add("Blunt", FixedPoint2.New(25));

        var entities = new HashSet<Entity<DamageableComponent>>();
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, tile, entities);
        foreach (var ent in entities)
            _damage.TryChangeDamage(ent.Owner, brute, origin: grid.Owner);

        // Animated falling-rock effect as the roof comes down on this tile.
        Spawn(settings.CollapseRockProp, coords);

        // Bury the tile in rock (the roof falling in).
        Spawn(settings.StoneRockEntity, coords);

        // The ground/default level directly above loses its floor tile in the SAME spot, so the surface caves
        // into the pit exactly where the underground gave way.
        RemoveSurfaceTileAbove(stoneMap, grid, tile);
    }

    /// <summary>True if a walk-through staircase (CMUZLevelHighGround) sits on this tile or within <paramref name="range"/> tiles.</summary>
    private bool HasStairWithin(Entity<MapGridComponent> grid, Vector2i tile, int range)
    {
        for (var dx = -range; dx <= range; dx++)
        {
            for (var dy = -range; dy <= range; dy++)
            {
                foreach (var anchored in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile + new Vector2i(dx, dy)))
                {
                    if (HasComp<CMUZLevelHighGroundComponent>(anchored))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>Removes the floor tile on the level directly above this stone tile, at the same world position.</summary>
    private void RemoveSurfaceTileAbove(Entity<ZGeneratedStoneComponent> stoneMap, Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!TryComp<CMUZLevelMapComponent>(stoneMap.Owner, out var zMap) || zMap.MapAbove is not { } aboveMap)
            return;

        if (!TryComp<MapComponent>(aboveMap, out var aboveMapComp))
            return;

        var worldPos = _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, tile)).Position;
        var surfaceCoords = new MapCoordinates(worldPos, aboveMapComp.MapId);

        // Dropships are their own grids. If one is parked over this cave-in, don't try to pull its tiles or
        // entities down into the cavern; just trip the flight-safety lockout and leave the ship grid alone.
        if (MarkDropshipsOverCollapse(surfaceCoords))
            return;

        if (!_mapManager.TryFindGridAt(surfaceCoords, out var surfaceGridUid, out var surfaceGridComp))
            return;

        var surfaceTile = _map.TileIndicesFor(surfaceGridUid, surfaceGridComp, surfaceCoords);

        // Indestructible map-border walls (the CMBaseWallInvincible family) are the playfield boundary: never
        // pull the tile out from under one and never drop it into the cavern.
        foreach (var anchored in _map.GetAnchoredEntities(surfaceGridUid, surfaceGridComp, surfaceTile))
        {
            if (IsIndestructibleWall(anchored))
                return;
        }

        _map.SetTile(surfaceGridUid, surfaceGridComp, surfaceTile, Tile.Empty);

        // The floor that just gave way drops whatever was built on it into the cavern: every anchored (wrenched/
        // constructed) structure on the now-floorless surface tile is unanchored and moved down to the same spot
        // on this stone level, where there is no floor under it.
        DropBuiltEntitiesToLevelBelow(stoneMap.Owner, surfaceGridUid, surfaceGridComp, surfaceTile, worldPos);
    }

    /// <summary>
    /// Unanchors every built structure on <paramref name="surfaceTile"/> and moves it down to <paramref name="belowMap"/>
    /// at the same world position - the "tile collapsed, so what was built on it falls through" behaviour.
    /// Staircases are skipped (they are kept intact by <see cref="HasStairWithin"/>).
    /// </summary>
    private void DropBuiltEntitiesToLevelBelow(EntityUid belowMap, EntityUid surfaceGridUid, MapGridComponent surfaceGrid, Vector2i surfaceTile, Vector2 worldPos)
    {
        if (!TryComp<MapComponent>(belowMap, out var belowMapComp))
            return;

        // Snapshot first: unanchoring mutates the grid's anchored-entity set.
        var anchored = new List<EntityUid>(_map.GetAnchoredEntities(surfaceGridUid, surfaceGrid, surfaceTile));
        var belowCoords = new MapCoordinates(worldPos, belowMapComp.MapId);

        foreach (var uid in anchored)
        {
            if (HasComp<CMUZLevelHighGroundComponent>(uid))
                continue; // never drop staircases

            if (IsIndestructibleWall(uid))
                continue; // never drop map-border walls

            if (!TryComp<TransformComponent>(uid, out var xform))
                continue;

            _transform.Unanchor(uid, xform);
            _transform.SetMapCoordinates(uid, belowCoords);

            // Fallen structures are rubble: strip fixture hardness so a wall that lands inside (or later gets
            // buried under) a cave rock can never sit there grinding contacts forever. That constant jitter was
            // a physics drain that degraded server TPS (felt as ever-worsening UI lag) as the round ran on.
            MakeFallenDebrisNonHard(uid);
        }
    }

    // How far (in tiles) collapse rumble and vignette feedback reach from the collapsing region.
    // Players further away feel nothing.
    private const int CollapseEffectRange = 33;

    /// <summary>Minimum Chebyshev tile distance from <paramref name="tile"/> to any tile in the region
    /// (0 = standing in it). Returns int.MaxValue for an empty region.</summary>
    private static int DistanceToRegion(Vector2i tile, HashSet<Vector2i> region)
    {
        if (region.Contains(tile))
            return 0;

        var best = int.MaxValue;
        foreach (var t in region)
        {
            var d = Math.Max(Math.Abs(t.X - tile.X), Math.Abs(t.Y - tile.Y));
            if (d < best)
                best = d;
        }
        return best;
    }

    /// <summary>World-space AABB of the collapsed region's tiles (used to range-limit surface effects).</summary>
    private (Vector2 Min, Vector2 Max) RegionWorldBounds(EntityUid gridUid, MapGridComponent grid, List<Vector2i> region)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var tile in region)
        {
            var world = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, tile)).Position;
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world);
        }
        return (min, max);
    }

    /// <summary>An indestructible map-border wall: tagged as a wall but with no Damageable at all (the
    /// CMBaseWallInvincible family). These are map boundaries and must never fall or be moved.</summary>
    private bool IsIndestructibleWall(EntityUid uid)
    {
        return _tag.HasTag(uid, "Wall") && !HasComp<DamageableComponent>(uid);
    }

    /// <summary>Turns a structure that has fallen through a collapsed floor into inert rubble: every fixture's
    /// collision layer/mask is zeroed (non-hard alone still raises contact events, so bullets would still "hit"
    /// the fallen wall - with no collision bits projectiles pass straight through and cave rocks never grind
    /// contacts against it). The networked ZFallenDebris marker makes the client draw it battered.</summary>
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

    /// <summary>A tile holds up the roof if it is untouched rock, mined rock, or a built vertical pillar/anchor.</summary>
    private bool IsSolid(Entity<MapGridComponent> grid, Vector2i tile, HashSet<Vector2i> generatedChunks, int chunkSize)
    {
        // Tiles in chunks we haven't generated yet are still solid bedrock.
        var chunk = new Vector2i(FloorDiv(tile.X, chunkSize), FloorDiv(tile.Y, chunkSize));
        if (!generatedChunks.Contains(chunk))
            return true;

        // Mapper-authored load-bearing terrain (see ZLoadBearingTerrainComponent). Checked from the position
        // index rather than the anchored lookup below, because this terrain is intentionally unanchored - the
        // reason Shepherd's underground river read as one giant open cavern and kept dropping its bridges.
        if (_loadBearingTerrain.TryGetValue(grid.Owner, out var terrainTiles) && terrainTiles.Contains(tile))
            return true;

        // Only genuinely load-bearing anchored entities hold the roof: natural/mined rock and real walls (both
        // occlude and/or carry the Wall tag) and built support pillars. A merely wrenched-down entity (a chair,
        // a table, a vending machine) is NOT a natural support and must not stabilise a cavern.
        foreach (var anchored in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
        {
            if (IsLoadBearing(anchored))
                return true;
        }

        return false;
    }

    // Grid -> tiles carrying load-bearing terrain, plus the reverse map so an entity can be removed again.
    // Maintained on component start/shutdown because the terrain is unanchored and so never appears in the
    // grid's anchored lookup that IsSolid otherwise relies on.
    private readonly Dictionary<EntityUid, HashSet<Vector2i>> _loadBearingTerrain = new();
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile)> _loadBearingTerrainAt = new();

    private void OnLoadBearingTerrainStartup(Entity<ZLoadBearingTerrainComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var gridComp))
            return;

        var tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        if (!_loadBearingTerrain.TryGetValue(gridUid, out var tiles))
            _loadBearingTerrain[gridUid] = tiles = new HashSet<Vector2i>();

        tiles.Add(tile);
        _loadBearingTerrainAt[ent.Owner] = (gridUid, tile);
    }

    private void OnLoadBearingTerrainShutdown(Entity<ZLoadBearingTerrainComponent> ent, ref ComponentShutdown args)
    {
        if (!_loadBearingTerrainAt.Remove(ent.Owner, out var at))
            return;

        // Another terrain entity may share the tile (overlapping water sprites), so only clear the tile once
        // nothing marked is left on it.
        if (!_loadBearingTerrain.TryGetValue(at.Grid, out var tiles))
            return;

        foreach (var (_, other) in _loadBearingTerrainAt)
        {
            if (other.Grid == at.Grid && other.Tile == at.Tile)
                return;
        }

        tiles.Remove(at.Tile);
        if (tiles.Count == 0)
            _loadBearingTerrain.Remove(at.Grid);
    }

    /// <summary>True if an anchored entity genuinely holds up a cave roof: a built structural support/pillar, or
    /// an actual WALL - anything whose prototype inherits the vanilla BaseWall (covers mined rock, which loses
    /// the Wall tag by overriding its Tag list) or the RMC invincible-wall root (covers every CM wall family).</summary>
    private bool IsLoadBearing(EntityUid uid)
    {
        if (HasComp<StructuralSupportComponent>(uid))
            return true;

        return MetaData(uid).EntityPrototype is { } proto && IsWallPrototype(proto.ID);
    }

    // Prototype id -> "inherits a wall base" verdict, cached because the parent walk runs for every anchored
    // entity a stability check touches.
    private readonly Dictionary<string, bool> _wallProtoCache = new();

    private bool IsWallPrototype(string id)
    {
        if (_wallProtoCache.TryGetValue(id, out var cached))
            return cached;

        // EnumerateALLParents, not EnumerateParents: the wall roots (BaseWall, RMCBaseWallInvincibleNoIcon) are
        // ABSTRACT prototypes, and the plain variant silently skips abstract ancestors - which made mined rock
        // (mineablesolarisrock -> BaseWall) read as "not a wall" and let whole caves collapse on generation.
        var isWall = false;
        foreach (var (parentId, _) in _protoManager.EnumerateAllParents<EntityPrototype>(id, includeSelf: true))
        {
            if (parentId is "BaseWall" or "RMCBaseWallInvincibleNoIcon")
            {
                isWall = true;
                break;
            }
        }

        _wallProtoCache[id] = isWall;
        return isWall;
    }

    /// <summary>True if a built vertical support / anchor stands within <paramref name="span"/> tiles (Manhattan)
    /// of this tile - the beam's protected radius.</summary>
    private bool HasBuiltSupportWithin(Entity<MapGridComponent> grid, Vector2i tile, int span)
    {
        for (var dx = -span; dx <= span; dx++)
        {
            for (var dy = -span; dy <= span; dy++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > span)
                    continue;

                foreach (var anchored in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile + new Vector2i(dx, dy)))
                {
                    if (TryComp<StructuralSupportComponent>(anchored, out var sup) && (sup.IsVerticalSupport || sup.IsAnchor))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>True if a solid support tile exists within <paramref name="span"/> tiles (BFS, Chebyshev-ish).</summary>
    private bool HasSupportWithin(Entity<MapGridComponent> grid, Vector2i tile, int span, HashSet<Vector2i> generatedChunks, int chunkSize)
    {
        for (var dx = -span; dx <= span; dx++)
        {
            for (var dy = -span; dy <= span; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (Math.Abs(dx) + Math.Abs(dy) > span)
                    continue;

                if (IsSolid(grid, tile + new Vector2i(dx, dy), generatedChunks, chunkSize))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Plays collapse feedback for nearby players. Rumble SFX is throttled so it does not stack into noise.
    /// </summary>
    private void RumbleAndVignette(Entity<ZGeneratedStoneComponent> stoneMap, TimeSpan now, ZBuildableMapComponent settings)
    {
        var playRumble = now >= stoneMap.Comp.CollapseNextRumble;
        if (playRumble)
            stoneMap.Comp.CollapseNextRumble = now + RumbleInterval;

        // Effects are local: only players near the collapsing region get feedback. Engulfed players (their own
        // tile is in the doomed region) additionally get the rapid black vignette, re-sent each rumble so it
        // lasts the collapse.
        _gridQuery.TryComp(stoneMap.Comp.StoneGrid, out var stoneGridComp);
        var regionTiles = new HashSet<Vector2i>(stoneMap.Comp.LastCollapseRegion);

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        var played = false;
        while (query.MoveNext(out var uid, out var actor, out var xform))
        {
            if (xform.MapUid != stoneMap.Owner || stoneGridComp == null)
                continue;

            var actorCoords = _transform.GetMapCoordinates(uid, xform);
            var actorTile = _map.TileIndicesFor(stoneMap.Comp.StoneGrid, stoneGridComp, actorCoords);
            var dist = DistanceToRegion(actorTile, regionTiles);
            if (dist > CollapseEffectRange)
                continue;

            if (playRumble)
                RaiseNetworkEvent(new ZCollapseVignetteEvent { Engulfed = dist <= 1 }, actor.PlayerSession);

            if (playRumble && !played)
            {
                // Guarded: a missing/misconfigured RumbleSound path must never crash the tick (GetAudioLength throws).
                try
                {
                    _audio.PlayPvs(new SoundPathSpecifier(settings.RumbleSound), uid);
                }
                catch (Exception e)
                {
                    Log.Warning($"[zcavein] Could not play rumble sound '{settings.RumbleSound}': {e.Message}");
                }

                played = true;
            }
        }
    }

    /// <summary>
    /// At the END of an underground cave-in, damages entities on the surface directly above the collapsed
    /// region. This only fires once per cave-in event (not from the continuous stability scan), so
    /// ground-level maps whose underground has not yet been generated are never affected.
    ///
    /// The z-level BELOW the underground is implicitly stable: IsSolid treats ungenerated chunks as solid
    /// bedrock, so the underground level itself never reports an unstable floor for the deep void beneath it.
    /// </summary>
    private void TriggerSurfaceEffects(Entity<ZGeneratedStoneComponent> stoneMap, ZBuildableMapComponent settings)
    {
        if (!TryComp<CMUZLevelMapComponent>(stoneMap.Owner, out var zMap) || zMap.MapAbove is not { } aboveMap)
            return;

        if (!TryComp<MapComponent>(aboveMap, out var aboveMapComp))
            return;

        if (!_gridQuery.TryComp(stoneMap.Comp.StoneGrid, out var stoneGrid))
            return;

        // Sample up to 80 tiles from the collapsed region to limit CPU cost on large collapses.
        var region = stoneMap.Comp.LastCollapseRegion;
        var sampleCount = Math.Min(region.Count, 80);
        var step = region.Count <= sampleCount ? 1 : region.Count / sampleCount;

        var brute = new DamageSpecifier();
        brute.DamageDict.Add("Blunt", FixedPoint2.New(15));

        var damaged = new HashSet<Entity<DamageableComponent>>();

        for (var i = 0; i < region.Count; i += step)
        {
            var localCoords = _map.GridTileToLocal(stoneMap.Comp.StoneGrid, stoneGrid, region[i]);
            var worldPos = _transform.ToMapCoordinates(localCoords).Position;
            var surfaceCoords = new MapCoordinates(worldPos, aboveMapComp.MapId);

            // If this surface point overlaps a landed dropship grid, only mark/disable the ship. Do not damage
            // or spawn debris on the dropship grid; shuttle grids have their own tile layer and should not be
            // treated as collapsible terrain.
            if (MarkDropshipsOverCollapse(surfaceCoords))
                continue;

            if (!_mapManager.TryFindGridAt(surfaceCoords, out var surfaceGridUid, out var surfaceGridComp))
                continue;

            var surfaceTile = _map.TileIndicesFor(surfaceGridUid, surfaceGridComp, surfaceCoords);

            // Damage entities on the surface tile.
            damaged.Clear();
            _lookup.GetLocalEntitiesIntersecting(surfaceGridUid, surfaceTile, damaged);
            foreach (var ent in damaged)
                _damage.TryChangeDamage(ent.Owner, brute, origin: stoneMap.Owner);

            // Scatter some debris on the surface (30% chance per sampled tile).
            if (_random.Prob(0.30f))
                Spawn(settings.RockDebris, _map.GridTileToLocal(surfaceGridUid, surfaceGridComp, surfaceTile));
        }

        // Brief grey vignette blink for players on the surface NEAR the cave-in - not map-wide.
        // Region tiles map 1:1 to surface world positions, so distance is measured against the region's
        // world bounds.
        var (boundsMin, boundsMax) = RegionWorldBounds(stoneMap.Comp.StoneGrid, stoneGrid, region);
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out var uid, out var actor, out var xform))
        {
            if (xform.MapUid != aboveMap)
                continue;

            var pos = _transform.GetWorldPosition(uid);
            var clamped = Vector2.Clamp(pos, boundsMin, boundsMax);
            if ((pos - clamped).Length() > CollapseEffectRange)
                continue;

            RaiseNetworkEvent(new ZCollapseVignetteEvent(), actor.PlayerSession);
        }

        stoneMap.Comp.LastCollapseRegion.Clear();
    }

    /// <summary>
    /// Marks any dropship grid overlapping a cave-in's surface position. This deliberately scans all grids
    /// around the point instead of relying on TryFindGridAt, because landed dropships can overlap the planet
    /// grid and TryFindGridAt may return the ground grid first.
    /// </summary>
    private bool MarkDropshipsOverCollapse(MapCoordinates impact)
    {
        _overlappingGrids.Clear();
        var min = impact.Position - new Vector2(DropshipCollapseDetectionRadius);
        var max = impact.Position + new Vector2(DropshipCollapseDetectionRadius);
        _mapManager.FindGridsIntersecting(impact.MapId, new Box2(min, max), ref _overlappingGrids, approx: true, includeMap: false);

        var marked = false;
        foreach (var grid in _overlappingGrids)
        {
            if (!HasComp<DropshipComponent>(grid.Owner))
                continue;

            marked = true;
            if (!HasComp<ZCollapseCompromisedComponent>(grid.Owner))
            {
                EnsureComp<ZCollapseCompromisedComponent>(grid.Owner);
                Log.Info($"[zcavein] Dropship {ToPrettyString(grid.Owner)} structurally compromised by a cave-in below it; takeoff disabled.");

                var computers = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
                while (computers.MoveNext(out _, out _, out var xform))
                {
                    if (xform.GridUid == grid.Owner)
                        Spawn("EffectSparks", xform.Coordinates);
                }
            }
        }

        if (marked)
            Spawn("EffectSparks", impact);

        _overlappingGrids.Clear();
        return marked;
    }

    private ZBuildableMapComponent GetSettings(EntityUid stoneMap)
    {
        // Settings live on the source map above the stone level.
        if (TryComp<CMUZLevelMapComponent>(stoneMap, out var z) &&
            z.MapAbove is { } above &&
            TryComp<ZBuildableMapComponent>(above, out var aboveSettings))
        {
            return aboveSettings;
        }

        return CompOrNull<ZBuildableMapComponent>(stoneMap) ?? new ZBuildableMapComponent();
    }

    private static int FloorDiv(int a, int b) => (int) Math.Floor((double) a / b);

    /// <summary>
    /// Attributes a cave-in: prefers the last player who dealt damage on this level within the attribution window
    /// (the exact over-miner); otherwise falls back to the nearest player.
    /// </summary>
    private string DescribeCulprit(EntityUid mapUid, Vector2 worldPos)
    {
        if (_lastDigger.TryGetValue(mapUid, out var last) &&
            _timing.CurTime - last.Time <= AttributionWindow &&
            !Deleted(last.Culprit))
        {
            return ToPrettyString(last.Culprit).ToString();
        }

        return DescribeNearestPlayer(mapUid, worldPos);
    }

    /// <summary>
    /// Best-effort attribution for an environmental collapse: the nearest player on <paramref name="mapUid"/> to
    /// <paramref name="worldPos"/> (the most likely person responsible, e.g. the over-miner). Returns a pretty
    /// string for logs, or a "no nearby player" note if the level is empty.
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
}
