using Content.Shared._RMC14.Entrenching;
using Content.Shared.Physics;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Map;

public sealed partial class RMCMapSystem
{
    /// <summary>
    /// Checks weapon placement while allowing barricades along the deployment tile's edges.
    /// Other solid objects and, for sentries, structures still block deployment.
    /// </summary>
    public bool CanDeployWeaponOn(EntityCoordinates coordinates, CollisionGroup group = CollisionGroup.Impassable,
        bool checkStructures = true)
    {
        if (_turf.TryGetTileRef(coordinates, out var tile) &&
            _turf.IsTileBlocked(tile.Value.GridUid, tile.Value.GridIndices, group,
                ignore: uid => HasComp<BarricadeComponent>(uid)))
            return false;

        if (!checkStructures)
            return true;

        var anchored = GetAnchoredEntitiesEnumerator(coordinates);
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<BarricadeComponent>(uid) && _tag.HasTag(uid, StructureTag))
                return false;
        }

        return true;
    }
}
