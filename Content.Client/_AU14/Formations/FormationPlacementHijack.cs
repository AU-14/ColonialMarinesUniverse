using Content.Shared._AU14.Formations;
using Robust.Client.Placement;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Formations;

/// <summary>
/// Intercepts left-clicks during formation dot placement.
/// The ghost preview entity (AU14FormationDotGhostFollower/Leader) is rendered
/// by the placement manager using its sprite definition directly — we do not
/// override CurrentTextures so the tinted arrow ghost shows naturally.
/// Press R to rotate the ghost arrow (sets intended facing direction),
/// then left-click to confirm a dot at that tile.
/// </summary>
public sealed class FormationPlacementHijack : PlacementHijack
{
    private readonly FormationClientSystem _system;
    private readonly bool _isLeaderDot;

    public override bool CanRotate => true;

    public FormationPlacementHijack(FormationClientSystem system, bool isLeaderDot)
    {
        _system = system;
        _isLeaderDot = isLeaderDot;
    }

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        var facing = Manager.Direction;
        _system.OnPlacementClick(coordinates, _isLeaderDot, facing);
        return true;
    }

    public override bool HijackDeletion(EntityUid entity)
    {
        // Consume right-clicks so they don't cancel placement mode unexpectedly.
        return true;
    }
}
