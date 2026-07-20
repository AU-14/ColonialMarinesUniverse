// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared._AU14.ZLevelBuilding;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): drives setup when a <see cref="ZStairComponent"/> stair is placed.
///
/// Traversal itself is handled by the inherited CMUZLevelHighGround component. The hard-won rule (see
/// z_stairs.yml) is that a walk-through stair must sit on the LOWER of the two levels it connects, and the
/// tile directly above it on the upper level must be an OPEN shaft (empty tile) - the movement code stops at
/// the first solid tile on your current level and never checks the stair below, so descent only works through
/// a hole. This system does the once-on-placement work to satisfy that rule.
///
/// UP stair (direction +1, built on the lower level - the built entity IS the traversal stair):
///   - Ensures the level above exists.
///   - Lays a standing platform RING on the upper level and leaves the shaft tile open so you can descend.
///   - Places a structural support beam next to the stair on this (lower) level.
///
/// DOWN stair (direction -1, built on the current level - the built entity is only a frame over the shaft):
///   - Generates a stone underground level below via <see cref="ZLevelBuildingSystem.PrepareStoneForStair"/>.
///   - Drops the real traversal stair (<see cref="ZStairComponent.PartnerProto"/>) onto that lower level.
///   - Punches the shaft hole on THIS level so you can walk down onto the stair below.
///   - Places a structural support beam next to the stair on this level.
///
/// The traversal stair / companion (<see cref="ZStairComponent.PartnerProto"/>) carries NO ZStairComponent,
/// so this system never fires for it and there is no recursive setup.
/// </summary>
public sealed class ZStairSystem : EntitySystem
{
    [Dependency] private readonly ZLevelBuildingSystem _building = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private int _deferSetup;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        SubscribeLocalEvent<ZStairComponent, MapInitEvent>(OnStairMapInit);
        SubscribeLocalEvent<ZStairBeamLinkComponent, EntityTerminatingEvent>(OnBeamDestroyed);
    }

    /// <summary>When a staircase's support beam is destroyed, collapse the staircase it held up: delete the stair
    /// (and its companion on the connected level) and clear the standing platform tiles.</summary>
    private void OnBeamDestroyed(Entity<ZStairBeamLinkComponent> ent, ref EntityTerminatingEvent args)
    {
        var link = ent.Comp;

        // Only clear the tiles the stair actually LAID - never player-built or mapped floor that happened to
        // sit inside the platform radius (laying skipped those, so destruction must skip them too).
        if (link.HasPlatform && !TerminatingOrDeleted(link.PlatformGrid) && _gridQuery.TryComp(link.PlatformGrid, out var platformGrid))
        {
            foreach (var tile in link.LaidTiles)
                _map.SetTile(link.PlatformGrid, platformGrid, tile, Tile.Empty);
        }

        if (link.Stair is { Valid: true } stair && !TerminatingOrDeleted(stair))
            QueueDel(stair);

        if (link.Companion is { Valid: true } companion && !TerminatingOrDeleted(companion))
            QueueDel(companion);
    }

    private void OnStairMapInit(Entity<ZStairComponent> ent, ref MapInitEvent args)
    {
        if (_deferSetup == 0)
            EnsureSetup(ent);
    }

    /// <summary>Defers setup while saved-build roots load at their serialized positions.</summary>
    public void BeginDeferredSetup() => _deferSetup++;

    public void EndDeferredSetup() => _deferSetup = Math.Max(0, _deferSetup - 1);

    /// <summary>Creates the beam, companion, and platform package at a stair's current final position.</summary>
    public void EnsureSetup(Entity<ZStairComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        // Safety net for the ZBuildAllowed construction condition: if a map is opted out of vertical building
        // (ZBuildableMap { enabled: false }, e.g. the UNS Almayer), never generate a level / dig under it even if
        // a stair was somehow placed (admin spawn). The stair just sits there inert.
        if (!_building.IsEnabledOn(mapUid))
            return;

        var stairTile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var stairWorld = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, stairTile)).Position;
        var beamTile = stairTile + ent.Comp.BeamOffset;
        var beamWorld = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, beamTile)).Position;

        if (ent.Comp.Direction > 0)
            SetupUpStair(ent, mapUid, gridUid, grid, stairTile, stairWorld, beamWorld);
        else
            SetupDownStair(ent, mapUid, gridUid, grid, stairTile, stairWorld, beamWorld);
    }

    // UP stair: ensure level above, lay a platform ring with an open shaft above, support beam on this level.
    // The built entity itself is the traversal stair on this (lower) level - we do NOT place a stair above.
    private void SetupUpStair(
        Entity<ZStairComponent> ent,
        EntityUid mapUid,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i stairTile,
        Vector2 stairWorld,
        Vector2 beamWorld)
    {
        if (!_building.EnsureNeighborLevel(mapUid, 1, gridUid, stairWorld, out _, out var upperGrid))
            return;

        var laid = LayPlatformRingWithShaft(upperGrid, stairWorld, ent.Comp.ReflectFloorTile, ent.Comp.PlatformRadius);

        // Link the beam to this stair + its platform so destroying the beam collapses both.
        if (PlaceBeamWall(gridUid, beamWorld, ent.Comp.ReflectBeam) is { } beam
            && _gridQuery.TryComp(upperGrid, out var upperGridComp)
            && Transform(upperGrid).MapUid is { } upperMapUid
            && TryComp<MapComponent>(upperMapUid, out var upperMap))
        {
            var link = EnsureComp<ZStairBeamLinkComponent>(beam);
            link.Stair = ent.Owner;
            link.PlatformGrid = upperGrid;
            link.PlatformCenter = _map.TileIndicesFor(upperGrid, upperGridComp, new MapCoordinates(stairWorld, upperMap.MapId));
            link.PlatformRadius = ent.Comp.PlatformRadius;
            link.HasPlatform = true;
            link.LaidTiles.AddRange(laid);
        }
    }

    // DOWN stair: generate the level below, drop the real traversal stair there, open the shaft on THIS level,
    // and place a support beam on the LOWER (stone) level. The built entity stays as a frame over the open shaft.
    private void SetupDownStair(
        Entity<ZStairComponent> ent,
        EntityUid mapUid,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i stairTile,
        Vector2 stairWorld,
        Vector2 beamWorld)
    {
        if (!_building.PrepareStoneForStair(mapUid, gridUid, stairWorld, out var stoneGrid))
            return;

        var companion = PlaceTraversalStair(stoneGrid, stairWorld, ent.Comp.PartnerProto);

        // Guarantee solid ground at and around the landing BEFORE the shaft opens. Stone chunk generation is
        // lazy (per-chunk-on-approach), so a landing spot near a chunk edge can still have EMPTY neighbour
        // tiles when the player drops in - and falling onto an empty tile punches them through to the level
        // below the intended one. A small always-laid floor patch closes that hole; the cave streams in
        // around it later.
        LayLandingPatch(stoneGrid, stairWorld, ent.Comp.ReflectFloorTile, LandingPatchRadius);

        // Open the shaft on this level so walking onto the frame tile drops you onto the stair below.
        _map.SetTile(gridUid, grid, stairTile, Tile.Empty);

        // The down-stair's support beam belongs on the LOWER (stone) level it descends to, not on this upper
        // level - it is the column holding up the shaft from below. (Levels are world-aligned, so beamWorld maps
        // to the same tile on the stone grid.) Link it to this stair + its companion so destroying it collapses both.
        if (PlaceBeamWall(stoneGrid, beamWorld, ent.Comp.ReflectBeam) is { } beam)
        {
            var link = EnsureComp<ZStairBeamLinkComponent>(beam);
            link.Stair = ent.Owner;
            link.Companion = companion ?? EntityUid.Invalid;
        }
    }

    // Spawns the bare traversal stair on the connected LOWER level. It has no ZStair, so this never recurses.
    private EntityUid? PlaceTraversalStair(EntityUid gridUid, Vector2 worldPos, string proto)
    {
        if (!_gridQuery.TryComp(gridUid, out var grid) || string.IsNullOrEmpty(proto))
            return null;

        if (Transform(gridUid).MapUid is not { } mapUid || !TryComp<MapComponent>(mapUid, out var mapComp))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));

        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (MetaData(anchored).EntityPrototype?.ID == proto)
                return anchored;
        }

        return Spawn(proto, _map.GridTileToLocal(gridUid, grid, tile));
    }

    private EntityUid? PlaceBeamWall(EntityUid gridUid, Vector2 worldPos, string beam)
    {
        if (!_gridQuery.TryComp(gridUid, out var grid) || string.IsNullOrEmpty(beam))
            return null;

        if (Transform(gridUid).MapUid is not { } mapUid || !TryComp<MapComponent>(mapUid, out var mapComp))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));

        EntityUid? beamUid = null;
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (MetaData(anchored).EntityPrototype?.ID == beam)
            {
                beamUid = anchored;
                break;
            }
        }

        beamUid ??= Spawn(beam, _map.GridTileToLocal(gridUid, grid, tile));

        // A staircase beam only holds up the staircase tiles, so hide it from the structural scanner's
        // "where can I build" heat-map - otherwise its span would wrongly read as buildable ground.
        if (TryComp<StructuralSupportComponent>(beamUid, out var support))
        {
            support.HideFromScanner = true;
            Dirty(beamUid.Value, support);
        }

        return beamUid;
    }

    // 🔧 TUNABLE: radius (in tiles) of the guaranteed floor patch laid under a down-stair's landing.
    private const int LandingPatchRadius = 1;

    /// <summary>
    /// Lays a solid floor patch (center INCLUDED - this is the landing, not a shaft ring) on the lower level,
    /// skipping tiles that already have real floor. Ensures the player always lands on something even when the
    /// surrounding cave chunk has not generated yet.
    /// </summary>
    private void LayLandingPatch(EntityUid gridUid, Vector2 worldPos, string tileId, int radius)
    {
        if (!_gridQuery.TryComp(gridUid, out var grid) || !_tileDef.TryGetDefinition(tileId, out var def))
            return;

        if (Transform(gridUid).MapUid is not { } mapUid || !TryComp<MapComponent>(mapUid, out var mapComp))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);
                if (_map.TryGetTileRef(gridUid, grid, tile, out var existing) && !existing.Tile.IsEmpty)
                    continue;

                _map.SetTile(gridUid, grid, tile, new Tile(def.TileId));
            }
        }
    }

    /// <summary>
    /// Lays a floor ring on the upper level so the player has somewhere to stand when they surface, but leaves
    /// the CENTER (shaft) tile open so descent works. Any pre-existing tile on the shaft tile is cleared.
    /// </summary>
    private List<Vector2i> LayPlatformRingWithShaft(EntityUid gridUid, Vector2 worldPos, string tileId, int radius)
    {
        var laid = new List<Vector2i>();
        if (!_gridQuery.TryComp(gridUid, out var grid) || !_tileDef.TryGetDefinition(tileId, out var def))
            return laid;

        if (Transform(gridUid).MapUid is not { } mapUid || !TryComp<MapComponent>(mapUid, out var mapComp))
            return laid;

        var center = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);

                // The shaft tile must stay open so the player can descend through it onto the stair below.
                if (dx == 0 && dy == 0)
                {
                    _map.SetTile(gridUid, grid, tile, Tile.Empty);
                    continue;
                }

                // Never overwrite pre-built / mapped content on the ring.
                if (_map.TryGetTileRef(gridUid, grid, tile, out var existing) && !existing.Tile.IsEmpty)
                    continue;

                _map.SetTile(gridUid, grid, tile, new Tile(def.TileId));
                laid.Add(tile);
            }
        }

        return laid;
    }
}
