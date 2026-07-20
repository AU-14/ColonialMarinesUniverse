// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Construction;
using Content.Client._CMU14.ZLevels.Core;
using Content.Shared._AU14.SavedBuilds;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared.Administration;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Popups;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Drives saved-build placement. Hold Alt to snap to the grid; the vanilla rotate key rotates; left-click
/// places; right-click cancels. Admins place the build instantly & free (server-side); everyone else
/// places vanilla construction ghosts for each entity, which they then build normally (consuming
/// materials) — i.e. building the whole saved structure the vanilla way.
/// </summary>
public sealed class SavedBuildPlacementSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly CMUClientZLevelsSystem _zLevels = default!;

    public bool Active { get; private set; }
    public bool IsAdmin { get; private set; }
    public Angle Rotation { get; private set; }

    /// <summary>
    /// Grid-aligned placement is a TOGGLE (you can't hold Alt and left-click at the same time): tapping
    /// Alt flips it. <see cref="Update"/> does the edge detection.
    /// </summary>
    public bool GridAligned { get; private set; }

    /// <summary>
    /// Set by the construction menu's build-mode dropdown (Player / Admin / Mapper). Admin and Mapper both place
    /// instantly &amp; free (re-validated server-side); Player places costed construction ghosts. The client only
    /// ever lets you pick a mode you hold the flag for, and the server re-checks anyway.
    /// </summary>
    public BuildSaveMode Mode { get; set; } = BuildSaveMode.Player;

    private SavedBuildInfo _current;
    private BuildPlaceOverlay? _overlay;
    private bool _altWasDown;

    // target entity prototype id -> the recipe that builds it (for the player ghost path).
    private Dictionary<string, ConstructionPrototype>? _recipeByTarget;
    private Dictionary<string, ConstructionPrototype>? _recipeByTile;

    public Vector2 RelMin => new(_current.RelMinX, _current.RelMinY);
    public Vector2 RelMax => new(_current.RelMaxX, _current.RelMaxY);
    public IReadOnlyList<BuildPreviewEntity> Preview => _current.Preview ?? new List<BuildPreviewEntity>();
    public IReadOnlyList<BuildPreviewTile> Tiles => _current.Tiles ?? new List<BuildPreviewTile>();

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, outsidePrediction: true))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnCancel, outsidePrediction: true))
            .Bind(EngineKeyFunctions.EditorRotateObject, new PointerInputCmdHandler(OnRotate, outsidePrediction: true))
            .Register<SavedBuildPlacementSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SavedBuildPlacementSystem>();
    }

    public override void Update(float frameTime)
    {
        if (!Active)
            return;

        // Alt is a toggle, not a hold — flip on the press edge.
        var altDown = _input.IsKeyDown(Keyboard.Key.Alt);
        if (altDown && !_altWasDown)
            GridAligned = !GridAligned;
        _altWasDown = altDown;
    }

    /// <summary>Whether this placement acts as the instant/free flow (Admin or Mapper, with the matching flag).</summary>
    private bool UseAdminPlacement =>
        (Mode == BuildSaveMode.Admin && _admin.HasFlag(AdminFlags.Spawn)) ||
        (Mode == BuildSaveMode.Mapper && (_admin.HasFlag(AdminFlags.Mapping) || _admin.HasFlag(AdminFlags.Spawn)));

    public void BeginPlacement(SavedBuildInfo info)
    {
        _current = info;
        Rotation = Angle.Zero;
        GridAligned = false;
        _altWasDown = _input.IsKeyDown(Keyboard.Key.Alt);
        IsAdmin = UseAdminPlacement;
        Active = true;

        _overlay ??= new BuildPlaceOverlay(this, _eye, _input,
            EntityManager.System<SpriteSystem>(), _proto, _cache);
        if (!_overlays.HasOverlay<BuildPlaceOverlay>())
            _overlays.AddOverlay(_overlay);
    }

    /// <summary>
    /// Place the build at its original recorded location. Admins place it instantly & free (server-side);
    /// everyone else (and admins in player mode) get vanilla construction ghosts at the original grid + anchor.
    /// </summary>
    public void PlaceAtOriginal(SavedBuildInfo info)
    {
        if (UseAdminPlacement)
        {
            // The build file is LOCAL: upload its YAML for the server to load (admin-gated server-side).
            if (!EntityManager.System<SavedBuildListSystem>().TryGetYaml(info.Id, out var yaml))
            {
                _popup.PopupCursor(Loc.GetString("saved-build-error-load"));
                return;
            }

            RaiseNetworkEvent(new RequestPlaceBuildEvent { Id = info.Id, Yaml = yaml, AtOriginal = true });
            return;
        }

        // Player ghost flow: the original grid must still exist this round.
        if (!TryGetEntity(info.SourceGrid, out var grid) || !HasComp<MapGridComponent>(grid))
        {
            _popup.PopupCursor(Loc.GetString("saved-build-error-noorigin"));
            return;
        }

        _current = info;
        Rotation = Angle.Zero;
        IsAdmin = false;
        var anchor = new EntityCoordinates(grid.Value, new Vector2(info.AnchorX, info.AnchorY));
        PlaceGhosts(_transform.ToMapCoordinates(anchor));
    }

    private void Cancel()
    {
        Active = false;
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    /// <summary>The placement origin under the cursor — snapped to the tile centre while Alt is held.</summary>
    public MapCoordinates GetTargetMap()
    {
        var cursor = _eye.PixelToMap(_input.MouseScreenPosition);
        if (GridAligned && _mapManager.TryFindGridAt(cursor, out var gridUid, out var grid))
        {
            var tile = _mapSystem.CoordinatesToTile(gridUid, grid, cursor);
            return _mapSystem.GridTileToWorld(gridUid, grid, tile);
        }

        return cursor;
    }

    /// <summary>Projects a placement point onto an existing linked level without changing its world X/Y.</summary>
    public bool TryGetLevelTarget(MapCoordinates target, int zOffset, out MapCoordinates levelTarget)
    {
        levelTarget = target;
        if (zOffset == 0)
            return true;

        var mapUid = _mapManager.GetMapEntityId(target.MapId);
        if (!mapUid.Valid || !_zLevels.TryMapOffset(mapUid, zOffset, out _, out var mapComp))
            return false;

        levelTarget = new MapCoordinates(target.Position, mapComp.MapId);
        return true;
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!Active || args.State != BoundKeyState.Down)
            return false;

        var target = GetTargetMap();
        if (IsAdmin)
            PlaceInstant(target);
        else
            PlaceGhosts(target);

        Cancel();
        return true;
    }

    private bool OnCancel(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!Active || args.State != BoundKeyState.Down)
            return false;

        Cancel();
        return true;
    }

    private bool OnRotate(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!Active || args.State != BoundKeyState.Down)
            return false;

        Rotation += Angle.FromDegrees(90);
        return true;
    }

    private void PlaceInstant(MapCoordinates target)
    {
        if (!_mapManager.TryFindGridAt(target, out var gridUid, out _))
            return;

        // The build file is LOCAL: upload its YAML for the server to load (admin-gated server-side).
        if (!EntityManager.System<SavedBuildListSystem>().TryGetYaml(_current.Id, out var yaml))
        {
            _popup.PopupCursor(Loc.GetString("saved-build-error-load"));
            return;
        }

        RaiseNetworkEvent(new RequestPlaceBuildEvent
        {
            Id = _current.Id,
            Yaml = yaml,
            Target = GetNetCoordinates(_transform.ToCoordinates(gridUid, target)),
            Rotation = Rotation.Theta,
        });
    }

    private void PlaceGhosts(MapCoordinates target)
    {
        var construction = EntityManager.System<ConstructionSystem>();
        EnsureRecipeMap();

        var placed = 0;
        foreach (var ent in Preview)
        {
            if (_recipeByTarget == null || !_recipeByTarget.TryGetValue(ent.Proto, out var recipe))
                continue;

            if (!TryGetLevelTarget(target, ent.Z, out var levelTarget))
                continue;

            var world = levelTarget.Position + Rotation.RotateVec(new Vector2(ent.X, ent.Y));
            var coords = new MapCoordinates(world, levelTarget.MapId);
            if (!TryGetPlacementGrid(coords, out var gridUid))
                continue;

            var dir = (Rotation + new Angle(ent.Rot)).GetDir();
            if (construction.TrySpawnGhost(recipe, _transform.ToCoordinates(gridUid, coords), dir, out _))
                placed++;
        }

        foreach (var tile in Tiles)
        {
            if (_recipeByTile == null || !_recipeByTile.TryGetValue(tile.Tile, out var recipe))
                continue;

            if (!TryGetLevelTarget(target, tile.Z, out var levelTarget))
                continue;

            var world = levelTarget.Position + Rotation.RotateVec(new Vector2(tile.X, tile.Y));
            var coords = new MapCoordinates(world, levelTarget.MapId);
            if (!TryGetPlacementGrid(coords, out var gridUid))
                continue;

            if (construction.TrySpawnGhost(recipe, _transform.ToCoordinates(gridUid, coords), Direction.South, out _))
                placed++;
        }

        _popup.PopupCursor(Loc.GetString("saved-build-ghosts-placed", ("count", placed)));
    }

    /// <summary>
    /// Finds the target grid even on a sparse generated z-level whose map-grid has no tile at this position yet.
    /// Saved tile/stair ghosts are what populate that empty region, so requiring an existing tile here drops the
    /// entire adjacent layer from player-mode placement.
    /// </summary>
    private bool TryGetPlacementGrid(MapCoordinates coords, out EntityUid gridUid)
    {
        if (_mapManager.TryFindGridAt(coords, out gridUid, out _))
            return true;

        var mapUid = _mapManager.GetMapEntityId(coords.MapId);
        if (mapUid.Valid && HasComp<MapGridComponent>(mapUid))
        {
            gridUid = mapUid;
            return true;
        }

        gridUid = default;
        return false;
    }

    private void EnsureRecipeMap()
    {
        if (_recipeByTarget != null)
            return;

        var construction = EntityManager.System<ConstructionSystem>();
        _recipeByTarget = new Dictionary<string, ConstructionPrototype>();
        _recipeByTile = new Dictionary<string, ConstructionPrototype>();
        foreach (var recipe in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (construction.TryGetRecipePrototype(recipe.ID, out var targetId) && targetId != null)
            {
                // Prefer the direct/native recipe when several recipes resolve to the same target. This matters
                // for setup entities such as z-stairs: their own recipe creates the one ghost that regenerates
                // the support beam and platform, while an arbitrary alternate recipe may not be placeable here.
                if (!_recipeByTarget.ContainsKey(targetId) || recipe.ID == targetId)
                    _recipeByTarget[targetId] = recipe;

                if (_proto.TryIndex<EntityPrototype>(targetId, out var targetProto) &&
                    targetProto.TryGetComponent<TileApplierComponent>(out var applier, _componentFactory))
                    _recipeByTile.TryAdd(applier.Tile, recipe);
            }
        }
    }
}
