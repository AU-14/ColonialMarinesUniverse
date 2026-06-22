using Content.Shared._AU14.Formations;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Formations;

/// <summary>
/// Client-side system managing formation dot placement mode.
/// Holds a reference to the active BUI so the hijack can send placement messages,
/// and updates the ghost facing direction to follow the local player each frame.
/// </summary>
public sealed partial class FormationClientSystem : EntitySystem
{
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    private FormationMenuBui? _activeBui;
    private bool _isInFormationPlacement;

    public bool IsInPlacementMode => _isInFormationPlacement;

    public void EnterPlacementMode(FormationMenuBui bui, bool isLeaderDot)
    {
        _activeBui = bui;
        _isInFormationPlacement = true;

        var ghostProto = isLeaderDot ? "AU14FormationDotGhostLeader" : "AU14FormationDotGhostFollower";
        _placement.BeginPlacing(new PlacementInformation
        {
            IsTile = false,
            PlacementOption = "SnapgridCenter",
            EntityType = ghostProto,
        }, new FormationPlacementHijack(this, isLeaderDot));
    }

    public void ExitPlacementMode()
    {
        _isInFormationPlacement = false;
        _activeBui = null;
        if (_placement.IsActive)
            _placement.Clear();
    }

    /// <summary>Called by <see cref="FormationPlacementHijack"/> on each click.</summary>
    public void OnPlacementClick(EntityCoordinates coordinates, bool isLeaderDot, Direction facing)
    {
        if (_activeBui == null) return;
        if (_player.LocalSession?.AttachedEntity is not { } player) return;

        var playerXform = Transform(player);
        if (playerXform.GridUid is not { } gridUid) return;
        if (!TryComp<Robust.Shared.Map.Components.MapGridComponent>(gridUid, out var gridComp)) return;

        var tile = _mapSystem.TileIndicesFor(gridUid, gridComp, coordinates);

        _activeBui.SendPlaceDotMessage(tile, facing, isLeaderDot);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!_isInFormationPlacement) return;

        // If placement was cancelled externally (ESC), clean up our state.
        if (!_placement.IsActive)
        {
            _isInFormationPlacement = false;
            _activeBui = null;
            return;
        }

        // Sync ghost direction with the local player's current facing direction.
        // This gives a live preview of which way the dot arrow will point.
        if (_player.LocalSession?.AttachedEntity is not { } player) return;
        var xform = Transform(player);
        _placement.Direction = xform.LocalRotation.GetDir();
    }
}
