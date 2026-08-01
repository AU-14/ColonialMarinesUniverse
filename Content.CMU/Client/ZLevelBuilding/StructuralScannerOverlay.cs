// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): holder-only structural heat-map for the structural scanner. Drawn entirely
/// client-side; it only renders while the local player holds an enabled <see cref="StructuralScannerComponent"/>.
///
/// Two modes, depending where the holder stands:
///  - UNDERGROUND (a map with <see cref="ZGeneratedStoneComponent"/>): the cave-in view. A dug-out (open) tile is
///    shaded red when no solid rock/pillar sits within <see cref="RoofSpan"/> tiles (it will cave in), yellow at
///    the limit.
///  - UPPER Z-LEVEL (a level above the ground, <see cref="CMUZLevelMapComponent.Depth"/> &gt; 0): the inverse,
///    "where can I build" view. Tiles within reach of a support beam on the level BELOW are shaded green - stable
///    ground you can floor/build on - regardless of whether a tile is there yet, so you can plan before placing.
/// </summary>
public sealed class StructuralScannerOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>Fallback when no <see cref="ZBuildableMapComponent"/> is resolvable (matches its default).</summary>
    private const int DefaultRoofSpan = 3;

    /// <summary>How many tiles around the player to evaluate; bounds the per-frame cost.</summary>
    private const int DrawRadius = 14;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;

    private static readonly Color UnstableColor = new(0.85f, 0.1f, 0.1f, 0.35f);
    private static readonly Color MarginalColor = new(0.9f, 0.75f, 0.1f, 0.3f);
    private static readonly Color StableColor = new(0.15f, 0.85f, 0.25f, 0.28f);

    public StructuralScannerOverlay()
    {
        IoCManager.InjectDependencies(this);
        _entMan = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _map = _entMan.System<SharedMapSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } player && HoldingEnabledScanner(player);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var xform = _entMan.GetComponent<TransformComponent>(player);
        if (xform.MapUid is not { } mapUid)
            return;

        if (xform.GridUid is not { } gridUid || !_entMan.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var handle = args.WorldHandle;

        if (_entMan.HasComponent<ZGeneratedStoneComponent>(mapUid))
        {
            DrawUndergroundInstability(handle, gridUid, grid, center, GetRoofSpan(mapUid));
            return;
        }

        // Upper z-level: show the region a beam on the level below holds up (where you can safely build).
        if (_entMan.TryGetComponent<CMUZLevelMapComponent>(mapUid, out var zMap) &&
            zMap.Depth > 0 &&
            zMap.MapBelow is { } below)
        {
            DrawUpperStable(handle, gridUid, grid, center, below);
        }
    }

    /// <summary>
    /// The map's real roof span. Settings live on the source map ABOVE a stone level (mirrors the server's
    /// GetSettings); falls back to the stone map itself, then the default.
    /// </summary>
    private int GetRoofSpan(EntityUid mapUid)
    {
        if (_entMan.TryGetComponent(mapUid, out CMUZLevelMapComponent? z) && z.MapAbove is { } above &&
            _entMan.TryGetComponent(above, out ZBuildableMapComponent? aboveSettings))
        {
            return Math.Max(1, aboveSettings.MaxRoofSpan);
        }

        return _entMan.TryGetComponent(mapUid, out ZBuildableMapComponent? own)
            ? Math.Max(1, own.MaxRoofSpan)
            : DefaultRoofSpan;
    }

    /// <summary>Underground: shade open tiles whose roof is unsupported (red) or marginal (yellow).</summary>
    private void DrawUndergroundInstability(DrawingHandleWorld handle, EntityUid gridUid, MapGridComponent grid, Vector2i center, int roofSpan)
    {
        for (var dx = -DrawRadius; dx <= DrawRadius; dx++)
        {
            for (var dy = -DrawRadius; dy <= DrawRadius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);
                if (IsSolid(gridUid, grid, tile))
                    continue;

                var nearest = NearestSolidDistance(gridUid, grid, tile, roofSpan);
                Color color;
                if (nearest > roofSpan)
                    color = UnstableColor;
                else if (nearest == roofSpan)
                    color = MarginalColor;
                else
                    continue;

                DrawTile(handle, gridUid, grid, tile, color);
            }
        }
    }

    /// <summary>Upper z-level: shade every tile within a below-level beam's span green (stable build ground).</summary>
    private void DrawUpperStable(DrawingHandleWorld handle, EntityUid gridUid, MapGridComponent grid, Vector2i center, EntityUid below)
    {
        // Collect the support beams on the level below, projected into this grid's tile frame (the levels are
        // world-aligned, so a below-level beam covers the tile directly above it out to its cantilever span).
        var beams = new List<(Vector2i Tile, int Span)>();
        var query = _entMan.EntityQueryEnumerator<StructuralSupportComponent, TransformComponent>();
        while (query.MoveNext(out var beamUid, out var support, out var beamXform))
        {
            if (beamXform.MapUid != below || (!support.IsVerticalSupport && !support.IsAnchor))
                continue;

            // A staircase's own support beam only holds up the stair tiles, not general build ground;
            // shading its span green would confuse players into thinking those tiles are buildable.
            if (support.HideFromScanner)
                continue;

            var beamTile = _map.WorldToTile(gridUid, grid, _transform.GetWorldPosition(beamUid));
            beams.Add((beamTile, support.CantileverSpan));
        }

        if (beams.Count == 0)
            return;

        for (var dx = -DrawRadius; dx <= DrawRadius; dx++)
        {
            for (var dy = -DrawRadius; dy <= DrawRadius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);

                var stable = false;
                foreach (var beam in beams)
                {
                    if (Math.Abs(tile.X - beam.Tile.X) + Math.Abs(tile.Y - beam.Tile.Y) <= beam.Span)
                    {
                        stable = true;
                        break;
                    }
                }

                if (stable)
                    DrawTile(handle, gridUid, grid, tile, StableColor);
            }
        }
    }

    private void DrawTile(DrawingHandleWorld handle, EntityUid gridUid, MapGridComponent grid, Vector2i tile, Color color)
    {
        var world = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, tile)).Position;
        handle.DrawRect(new Box2(world - new Vector2(0.5f, 0.5f), world + new Vector2(0.5f, 0.5f)), color);
    }

    private bool HoldingEnabledScanner(EntityUid player)
    {
        var query = _entMan.EntityQueryEnumerator<StructuralScannerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var scanner, out var xform))
        {
            if (scanner.Enabled && xform.ParentUid == player)
                return true;
        }

        return false;
    }

    /// <summary>Client-side roof support: an empty/ungenerated tile, or any anchored entity (rock/wall/pillar).</summary>
    private bool IsSolid(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        foreach (var _ in _map.GetAnchoredEntities(gridUid, grid, tile))
            return true;

        return false;
    }

    /// <summary>Manhattan distance to the nearest solid tile, capped at roofSpan + 1.</summary>
    private int NearestSolidDistance(EntityUid gridUid, MapGridComponent grid, Vector2i tile, int roofSpan)
    {
        for (var r = 1; r <= roofSpan; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                var dy = r - Math.Abs(dx);
                if (IsSolid(gridUid, grid, tile + new Vector2i(dx, dy)) ||
                    (dy != 0 && IsSolid(gridUid, grid, tile + new Vector2i(dx, -dy))))
                {
                    return r;
                }
            }
        }

        return roofSpan + 1;
    }
}
