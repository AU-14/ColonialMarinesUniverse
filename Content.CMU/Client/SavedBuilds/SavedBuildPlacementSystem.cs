// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Construction;
using Content.Shared._AU14.SavedBuilds;
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

    public bool Active { get; private set; }
    public bool IsAdmin { get; private set; }
    public Angle Rotation { get; private set; }

    /// <summary>
    /// Grid-aligned placement is a TOGGLE (you can't hold Alt and left-click at the same time): tapping
    /// Alt flips it. <see cref="Update"/> does the edge detection.
    /// </summary>
    public bool GridAligned { get; private set; }

    /// <summary>
    /// Set by the construction menu's Admin/Player toggle. When true, an admin places vanilla construction
    /// ghosts (the player flow) instead of placing the build instantly & free.
    /// </summary>
    public bool ForcePlayerMode { get; set; }

    private SavedBuildInfo _current;
    private BuildPlaceOverlay? _overlay;
    private bool _altWasDown;

    // target entity prototype id -> the recipe that builds it (for the player ghost path).
    private Dictionary<string, ConstructionPrototype>? _recipeByTarget;

    public Vector2 RelMin => new(_current.RelMinX, _current.RelMinY);
    public Vector2 RelMax => new(_current.RelMaxX, _current.RelMaxY);
    public IReadOnlyList<BuildPreviewEntity> Preview => _current.Preview ?? new List<BuildPreviewEntity>();

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

    /// <summary>Whether this placement acts as the admin (instant/free) flow, honouring the menu mode toggle.</summary>
    private bool UseAdminPlacement => _admin.IsAdmin() && !ForcePlayerMode;

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
            RaiseNetworkEvent(new RequestPlaceBuildEvent { Id = info.Id, AtOriginal = true });
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

        RaiseNetworkEvent(new RequestPlaceBuildEvent
        {
            Id = _current.Id,
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

            var world = target.Position + Rotation.RotateVec(new Vector2(ent.X, ent.Y));
            var coords = new MapCoordinates(world, target.MapId);
            if (!_mapManager.TryFindGridAt(coords, out var gridUid, out _))
                continue;

            var dir = (Rotation + new Angle(ent.Rot)).GetDir();
            if (construction.TrySpawnGhost(recipe, _transform.ToCoordinates(gridUid, coords), dir, out _))
                placed++;
        }

        _popup.PopupCursor(Loc.GetString("saved-build-ghosts-placed", ("count", placed)));
    }

    private void EnsureRecipeMap()
    {
        if (_recipeByTarget != null)
            return;

        var construction = EntityManager.System<ConstructionSystem>();
        _recipeByTarget = new Dictionary<string, ConstructionPrototype>();
        foreach (var recipe in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (construction.TryGetRecipePrototype(recipe.ID, out var targetId) && targetId != null)
                _recipeByTarget.TryAdd(targetId, recipe);
        }
    }
}
