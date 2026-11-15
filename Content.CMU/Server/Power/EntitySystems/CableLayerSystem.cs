// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Power.Components;
using Content.Shared.CMU14.Cables;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.SubFloor;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Cables;

// Cables lie on the top layer first. A screwdriver click on the tile tucks every cable on it under the tile (so the floor
// covers them), and a click on a tile whose cables are tucked away brings them back out.
public sealed class CableLayerSystem : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSubFloorHideSystem _subFloor = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _cables = new();

    public override void Initialize()
    {
        base.Initialize();

        // A click on the bare tile, and a click on a cable that lies on it. (A tool can only be subscribed to for one of
        // these events once in the whole game, and the AfterInteract of a tool is taken, hence these two.)
        SubscribeLocalEvent<ToolComponent, BeforeRangedInteractEvent>(OnTileClick);
        SubscribeLocalEvent<SubFloorHideComponent, AfterInteractUsingEvent>(OnCableClick);
    }

    private void OnTileClick(Entity<ToolComponent> tool, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // The tile itself: a click on anything else keeps its own use of the screwdriver.
        if (args.Target is { } target && !HasComp<MapGridComponent>(target))
            return;

        if (TryToggle(tool, args.User, args.ClickLocation))
            args.Handled = true;
    }

    private void OnCableClick(Entity<SubFloorHideComponent> cable, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !HasComp<CableComponent>(cable) || !TryComp<ToolComponent>(args.Used, out var tool))
            return;

        if (TryToggle((args.Used, tool), args.User, Transform(cable).Coordinates))
            args.Handled = true;
    }

    // Tucks the cables of the tile under it, or brings them out if they are tucked away already.
    private bool TryToggle(Entity<ToolComponent> tool, EntityUid user, EntityCoordinates click)
    {
        if (!tool.Comp.Qualities.Contains("Screwing"))
            return false;

        if (_transform.GetGrid(click) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, click);

        _cables.Clear();
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<CableComponent>(anchored) && HasComp<SubFloorHideComponent>(anchored))
                _cables.Add(anchored);
        }

        if (_cables.Count == 0)
            return false;

        var covered = _subFloor.HasFloorCover(gridUid, grid, tile);
        var anyOnTop = false;
        foreach (var cable in _cables)
        {
            if (!IsBuried(cable, covered))
                anyOnTop = true;
        }

        if (anyOnTop && !covered)
        {
            // Bare plating has no tile to put them under.
            _popup.PopupEntity(Loc.GetString("au14-cable-layer-no-tile"), user, user);
            return true;
        }

        foreach (var cable in _cables)
        {
            var layer = EnsureComp<CableLayerComponent>(cable);
            layer.Buried = anyOnTop;
            Dirty(cable, layer);
            _subFloor.RefreshCover(cable);
        }

        _tool.PlayToolSound(tool, tool.Comp, user);
        _popup.PopupEntity(Loc.GetString(anyOnTop ? "au14-cable-layer-buried" : "au14-cable-layer-raised"), user, user);
        return true;
    }

    // A cable that was never touched is buried exactly when a floor tile covers it.
    private bool IsBuried(EntityUid cable, bool covered)
    {
        return TryComp<CableLayerComponent>(cable, out var layer) ? layer.Buried : covered;
    }
}
