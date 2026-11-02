using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    /// <summary>
    /// Checks a prospective climb landing without moving the climber or anyone they are pulling.
    /// Uses the same floors, openings and elevated supports as falling physics.
    /// </summary>
    public bool HasClimbLanding(
        EntityUid climber,
        Entity<CMUZLevelMapComponent> map,
        Vector2 worldPosition,
        float localPosition)
    {
        var distance = QueryGroundDistance(climber, map, worldPosition, localPosition, velocity: 0f, out _, maxFloors: 1);
        return distance >= -MaxStepHeight && distance <= GroundSnapDistance;
    }
}
