// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.CameraShake;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
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
/// warning it caves in: the tile is buried in rock, anyone on it takes brute damage, and everyone on the level
/// gets a sustained screenshake + rumble for as long as the collapse keeps going.
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
    [Dependency] private readonly RMCCameraShakeSystem _shake = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WarningTime = TimeSpan.FromSeconds(8);

    // A triggered cavern collapse buries tiles a batch at a time so it is a big, sustained event.
    private static readonly TimeSpan CollapseStepInterval = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan RumbleInterval = TimeSpan.FromSeconds(0.45);
    private const int TilesPerStep = 6;

    /// <summary>Safety cap on how many tiles one cavern collapse may bury.</summary>
    private const int CollapseTileCap = 600;

    /// <summary>How far around each player (in tiles) we evaluate roof stability - keeps the scan cheap.</summary>
    private const int ScanRadius = 12;

    private static readonly Vector2i[] Cardinals =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private TimeSpan _nextScan;

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        // Advance any in-progress cavern collapses every tick so the shake + rumble stay sustained.
        var collapseQuery = EntityQueryEnumerator<ZGeneratedStoneComponent>();
        while (collapseQuery.MoveNext(out var mapUid, out var stone))
        {
            if (stone.CollapseQueue.Count > 0)
                ProcessCollapse((mapUid, stone), now);
        }

        if (now < _nextScan)
            return;

        _nextScan = now + ScanInterval;

        var stoneQuery = EntityQueryEnumerator<ZGeneratedStoneComponent>();
        while (stoneQuery.MoveNext(out var mapUid, out var stone))
        {
            ScanLevel((mapUid, stone));
        }
    }

    private void ScanLevel(Entity<ZGeneratedStoneComponent> stoneMap)
    {
        if (!_gridQuery.TryComp(stoneMap.Comp.StoneGrid, out var grid))
            return;

        var settings = GetSettings(stoneMap.Owner);
        var span = Math.Max(1, settings.MaxRoofSpan);
        var chunkSize = Math.Max(2, settings.ChunkSize);

        // Don't keep scanning a level that is mid-collapse.
        if (stoneMap.Comp.CollapseQueue.Count > 0)
            return;

        var now = _timing.CurTime;

        // Evaluate every generated (dug) tile, not just tiles near a player - an over-mined cavern caves in
        // whether or not anyone is standing there to see it. Cost scales with how much has been dug (the
        // generated chunk set), not the whole map; solid tiles are rejected cheaply by EvaluateTile.
        foreach (var chunk in stoneMap.Comp.GeneratedChunks)
        {
            var baseX = chunk.X * chunkSize;
            var baseY = chunk.Y * chunkSize;
            for (var dx = 0; dx < chunkSize; dx++)
            {
                for (var dy = 0; dy < chunkSize; dy++)
                {
                    var tile = new Vector2i(baseX + dx, baseY + dy);
                    if (EvaluateTile(stoneMap, (stoneMap.Comp.StoneGrid, grid), tile, span, chunkSize, now))
                        return; // a collapse just started; stop scanning this level this tick
                }
            }
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

        var region = new List<Vector2i>();
        var seen = new HashSet<Vector2i> { origin };
        var frontier = new Queue<Vector2i>();
        frontier.Enqueue(origin);

        while (frontier.TryDequeue(out var t) && region.Count < CollapseTileCap)
        {
            if (IsSolid(grid, t, stoneMap.Comp.GeneratedChunks, chunkSize))
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

        var settings = GetSettings(stoneMap.Owner);

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

    /// <summary>Buries the next batch of queued cavern tiles, damaging anyone caught, with sustained shake + rumble.</summary>
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

        ShakeAndRumble(stoneMap, now, settings);

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
        if (!_mapManager.TryFindGridAt(surfaceCoords, out var surfaceGridUid, out var surfaceGridComp))
            return;

        var surfaceTile = _map.TileIndicesFor(surfaceGridUid, surfaceGridComp, surfaceCoords);
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

            if (!TryComp<TransformComponent>(uid, out var xform))
                continue;

            _transform.Unanchor(uid, xform);
            _transform.SetMapCoordinates(uid, belowCoords);
        }
    }

    /// <summary>A tile holds up the roof if it is untouched rock, mined rock, or a built vertical pillar/anchor.</summary>
    private bool IsSolid(Entity<MapGridComponent> grid, Vector2i tile, HashSet<Vector2i> generatedChunks, int chunkSize)
    {
        // Tiles in chunks we haven't generated yet are still solid bedrock.
        var chunk = new Vector2i(FloorDiv(tile.X, chunkSize), FloorDiv(tile.Y, chunkSize));
        if (!generatedChunks.Contains(chunk))
            return true;

        // Any anchored entity (the mined rock of any prototype, a wall, or a built support pillar) holds the roof.
        foreach (var _ in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
            return true;

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
    /// Shakes the camera of everyone on the collapsing level (re-applied each step so it stays continuous for the
    /// duration of the collapse) and plays the rumble SFX on a throttle so it does not stack into noise.
    /// </summary>
    private void ShakeAndRumble(Entity<ZGeneratedStoneComponent> stoneMap, TimeSpan now, ZBuildableMapComponent settings)
    {
        var playRumble = now >= stoneMap.Comp.CollapseNextRumble;
        if (playRumble)
            stoneMap.Comp.CollapseNextRumble = now + RumbleInterval;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        var played = false;
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != stoneMap.Owner)
                continue;

            // 8 shakes @ 0.1s = 0.8s, longer than the 0.15s step, so the shake never lapses mid-collapse.
            _shake.ShakeCamera(uid, 8, 3);

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
    /// At the END of an underground cave-in, shakes and damages entities on the surface directly above the
    /// collapsed region. This only fires once per cave-in event (not from the continuous stability scan), so
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

        // Brief shake for players on the surface above the cave-in.
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == aboveMap)
                _shake.ShakeCamera(uid, 5, 2);
        }

        stoneMap.Comp.LastCollapseRegion.Clear();
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
}
