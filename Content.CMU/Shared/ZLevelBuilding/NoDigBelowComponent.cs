// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared.Construction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Construction.Conditions;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.ZLevelBuilding;

// A support that holds the level it stands on against the digging from above: while it stands, nothing is dug down on the level over it, as far as
// Radius tiles around it, into the level where it stands (no pit, no staircase down). The concrete pillars and the slab have it.
[RegisterComponent, NetworkedComponent]
public sealed partial class NoDigBelowComponent : Component
{
    // How many tiles around it (a square, not a circle) are held.
    [DataField]
    public int Radius = 1;
}

// Helper: tells whether a place is held by a support that forbids digging below it.
public static class NoDigBelow
{
    // The farthest a support can hold from itself, to look no farther than that.
    private const float Reach = 8f;

    // Whether a hole dug at the place (on its level) would open into a held place of the level under it: a support stands there, within its radius.
    public static bool IsHeld(IEntityManager entMan, EntityCoordinates location)
    {
        var transform = entMan.System<SharedTransformSystem>();
        var lookup = entMan.System<EntityLookupSystem>();

        if (transform.GetMap(location) is not { } map ||
            !entMan.TryGetComponent(map, out CMUZLevelMapComponent? level) ||
            level.MapBelow is not { } below ||
            !entMan.TryGetComponent(below, out MapComponent? belowMap))
        {
            return false;
        }

        var here = transform.ToMapCoordinates(location).Position;
        var near = new HashSet<Entity<NoDigBelowComponent>>();
        lookup.GetEntitiesInRange(new MapCoordinates(here, belowMap.MapId), Reach, near);

        // The tiles of the world are the same on every level, so the tiles are told by the floor of the place.
        foreach (var support in near)
        {
            var there = transform.GetMapCoordinates(support.Owner).Position;
            var dx = Math.Abs(Math.Floor(here.X) - Math.Floor(there.X));
            var dy = Math.Abs(Math.Floor(here.Y) - Math.Floor(there.Y));
            if (Math.Max(dx, dy) <= support.Comp.Radius)
                return true;
        }

        return false;
    }
}

// The condition of a construction: the staircases down are not built where the ground is held.
[UsedImplicitly]
[DataDefinition]
public sealed partial class NoDigNearby : IConstructionCondition
{
    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        return !NoDigBelow.IsHeld(IoCManager.Resolve<IEntityManager>(), location);
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-au14-no-dig-nearby",
        };
    }
}
