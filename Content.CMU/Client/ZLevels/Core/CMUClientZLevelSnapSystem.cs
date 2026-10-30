using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.CMU14.ZLevels.Core;

/// <summary>
/// CMU14: crossing a z level - a leap, a ladder, a teleporter - changes the local player's map. The client keeps
/// interpolating the player's transform from its previous position for the first frames after the transition, so the
/// sprite is briefly drawn where the player used to be. On the level just arrived at, that old spot is underneath the
/// deck, so the upper floor's roof covers the hunter for a frame or two.
/// </summary>
/// <remarks>
/// Clearing <see cref="TransformComponent.ActivelyLerping"/> is the same state the client transform system checks
/// before it interpolates, so this only stops the interpolation for the local player on the tick its map changes;
/// nothing else in the scene is touched and no other entity's rendering changes.
/// </remarks>
public sealed class CMUClientZLevelSnapSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    private EntityUid _lastPlayer;
    private MapId _lastMap = MapId.Nullspace;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } player)
        {
            _lastPlayer = EntityUid.Invalid;
            return;
        }

        var xform = Transform(player);
        if (player != _lastPlayer)
        {
            _lastPlayer = player;
            _lastMap = xform.MapID;
            return;
        }

        if (xform.MapID == _lastMap)
            return;

        _lastMap = xform.MapID;
        xform.ActivelyLerping = false;
    }
}
