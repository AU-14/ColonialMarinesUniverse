using System.Linq;
using Content.Shared._RMC14.Construction.Conditions;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Map;

namespace Content.Client.Construction;

public sealed partial class ConstructionSystem
{
    private bool ConflictsWithGhost(EntityUid ghost, EntityCoordinates location, ConstructionPrototype recipe, Direction direction)
    {
        var transform = Transform(ghost);
        // Spawning can reparent map-relative coordinates to the underlying grid.
        if (!TransformSystem.InRange(transform.Coordinates, location, 0.001f))
            return false;

        if (!IsBarricadeEdge(recipe) || !IsBarricadeEdge(Comp<ConstructionGhostComponent>(ghost).Prototype))
            return true;

        var rotation = TransformSystem.GetWorldRotation(ghost) - TransformSystem.GetWorldRotation(location.EntityId);
        return rotation.GetDir() == direction;
    }

    private static bool IsBarricadeEdge(ConstructionPrototype? recipe) =>
        recipe?.Conditions.Any(condition => condition is TileBarricadeClear) == true;
}
