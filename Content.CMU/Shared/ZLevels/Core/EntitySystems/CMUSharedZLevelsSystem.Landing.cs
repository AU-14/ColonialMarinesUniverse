using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    private readonly HashSet<FixtureProxy> _climbLandingBlockers = new();

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
        if (distance < -MaxStepHeight || distance > GroundSnapDistance)
            return false;

        if (!TryComp<FixturesComponent>(climber, out var fixtures))
            return true;

        // Test the destination before moving the climber; resolving an overlap after teleporting can trap them.
        var transform = new Robust.Shared.Physics.Transform(worldPosition, _transform.GetWorldRotation(climber));
        var query = new FixtureQueryArgs(new QueryFilter
        {
            LayerBits = -1,
            MaskBits = -1,
            Flags = QueryFlags.Dynamic | QueryFlags.Static,
        });
        _climbLandingBlockers.Clear();
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;
            for (var child = 0; child < fixture.Shape.ChildCount; child++)
                _lookup.GetFixturesIntersecting(Transform(map).MapID, fixture.Shape, child, transform, _climbLandingBlockers, query);

            foreach (var blocker in _climbLandingBlockers)
            {
                if (blocker.Entity == climber || !blocker.Body.CanCollide || !blocker.Fixture.Hard)
                    continue;
                if ((fixture.CollisionMask & blocker.Fixture.CollisionLayer) != 0 ||
                    (fixture.CollisionLayer & blocker.Fixture.CollisionMask) != 0)
                    return false;
            }
            _climbLandingBlockers.Clear();
        }

        return true;
    }
}
