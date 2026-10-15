using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    internal static EntityCoordinates GetAimCoordinates(
        SharedTransformSystem transform,
        EntityUid origin,
        MapCoordinates target)
    {
        // A facing update can arrive before the shot request or during prediction replay.
        // Grid/map coordinates preserve the cursor target without rotating it with the shooter.
        var reference = transform.GetMoverCoordinates(origin).EntityId;
        return transform.ToCoordinates(reference, target);
    }
}
