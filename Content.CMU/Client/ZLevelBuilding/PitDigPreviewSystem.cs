// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Client._RMC14.Deploy;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.DoAfter;
using Content.Shared.Input;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ZLevelBuilding;

// With a digging tool in the hand a square follows the cursor over the tiles, the way the ground of a tent does when it is being placed: Z (the use of the item in the hand)
// digs the tile it is on. The square is blue where the dig would reach and red where it would not. While the dig goes on the server keeps its own outline on the tile, and this
// one is gone.
public sealed class PitDigPreviewSystem : EntitySystem
{
    // How far the tool reaches, in tiles; past it the square is red.
    private const float Reach = 2.2f;

    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private PitDigPreviewOverlay? _overlay;

    // Whether the square is shown now (for a test and for whatever looks at it).
    public bool Showing => _overlay != null && _overlays.HasOverlay<PitDigPreviewOverlay>();

    public override void Initialize()
    {
        base.Initialize();

        // Z: the tile under the cursor is asked to be dug (the use of the item in the hand is not stopped: whatever it does with a tool in the hand it does).
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.UseItemInHand,
                InputCmdHandler.FromDelegate(_ => RequestDig(), handle: false, outsidePrediction: false))
            .Register<PitDigPreviewSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<PitDigPreviewSystem>();
        Hide();
    }

    private void RequestDig()
    {
        if (_player.LocalEntity is not { } player || !HoldsDiggingTool(player) || IsDigging(player))
            return;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace || !_map.TryFindGridAt(mouse, out var gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, mouse);
        RaiseNetworkEvent(new PitDigRequestEvent(GetNetCoordinates(_map.GridTileToLocal(gridUid, grid, tile))));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player || !HoldsDiggingTool(player) || IsDigging(player))
        {
            Hide();
            return;
        }

        if (_overlay == null)
        {
            _overlay = new PitDigPreviewOverlay();
            _overlays.AddOverlay(_overlay);
        }

        UpdateSquare(player, _overlay);
    }

    private bool HoldsDiggingTool(EntityUid player)
    {
        return _hands.TryGetActiveItem(player, out var held) && HasComp<PitDiggingToolComponent>(held);
    }

    // The tile is outlined by the server while it is being dug.
    private bool IsDigging(EntityUid player)
    {
        return _overlays.HasOverlay<RMCDeployAreaOverlay>() || TryComp<DoAfterComponent>(player, out var doAfters) && doAfters.DoAfters.Count > 0;
    }

    private void Hide()
    {
        if (_overlay == null)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlay = null;
    }

    // The square goes to the middle of the tile under the cursor.
    private void UpdateSquare(EntityUid player, PitDigPreviewOverlay overlay)
    {
        overlay.Visible = false;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace || !_map.TryFindGridAt(mouse, out var gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, mouse);
        var center = _map.GridTileToWorldPos(gridUid, grid, tile);
        var from = _transform.GetWorldPosition(player);

        overlay.Box = Box2.CenteredAround(center, Vector2.One);
        overlay.Color = (center - from).Length() <= Reach ? Color.Blue : Color.Red;
        overlay.Visible = true;
    }
}

// The square under the cursor: a faint fill and an outline.
public sealed class PitDigPreviewOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public Box2 Box;
    public Color Color = Color.Blue;
    public bool Visible;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!Visible)
            return;

        args.WorldHandle.DrawRect(Box, Color.WithAlpha(0.3f));
        args.WorldHandle.DrawRect(Box, Color.WithAlpha(0.9f), filled: false);
    }
}
