// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): a construction condition that blocks placement of the overhaul's vertical-building
/// recipes (stairs, support beams, in-air tiles) on a map a mapper has opted out of z-building.
///
/// Add it to those recipes' <c>conditions:</c> list. A map opts out with <c>ZBuildableMap { enabled: false }</c>
/// (see <see cref="ZBuildableMapComponent"/>), so mappers can stop players from building stairs / digging under
/// maps like the UNS Almayer. Maps with no component (the default) allow it. The component's <c>Enabled</c> field
/// is networked so this client-side ghost check matches the server.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class ZBuildAllowed : IConstructionCondition
{
    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();

        // location.EntityId is the grid (or map) the ghost is over; walk to its map to read the opt-out.
        if (!entMan.TryGetComponent(location.EntityId, out TransformComponent? xform) || xform.MapUid is not { } mapUid)
            return true; // can't resolve the map - don't block.

        return !entMan.TryGetComponent(mapUid, out ZBuildableMapComponent? zb) || zb.Enabled;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-au14-zbuild-allowed",
        };
    }
}
