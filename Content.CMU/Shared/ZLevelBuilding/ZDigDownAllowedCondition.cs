using System.Numerics;
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Asks whether a new downward excavation may start on <see cref="Map"/>. The server answers it from the dig-down
/// policy (CVar and game preset). Nothing answers it on the client, so construction ghosts stay permissive and
/// the server decides when the build starts and again when it completes.
/// </summary>
[ByRefEvent]
public struct ZDigDownQueryEvent
{
    public readonly EntityUid Map;

    /// <summary>The map position of the stair.</summary>
    public readonly Vector2 WorldPos;

    public bool Allowed = true;

    public ZDigDownQueryEvent(EntityUid map, Vector2 worldPos)
    {
        Map = map;
        WorldPos = worldPos;
    }
}

/// <summary>
/// A construction condition for the recipes that dig a level down (the cavern down-stair). It refuses where the
/// dig-down policy forbids a new excavation and the map has no stone level below yet, so no materials are spent
/// on a stair that would lead nowhere.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class ZDigDownAllowed : IConstructionCondition
{
    public bool RecheckOnCompletion => true;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();

        // location.EntityId is the grid (or map) the ghost is over; walk to its map.
        if (!entMan.TryGetComponent(location.EntityId, out TransformComponent? xform) || xform.MapUid is not { } mapUid)
            return true; // can't resolve the map - don't block.

        var worldPos = entMan.System<SharedTransformSystem>().ToMapCoordinates(location).Position;
        var ev = new ZDigDownQueryEvent(mapUid, worldPos);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return ev.Allowed;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "cmu-construction-step-condition-zdig-down-allowed",
        };
    }
}
