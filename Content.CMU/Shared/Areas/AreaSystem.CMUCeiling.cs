using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    // Ceiling tiers per Docs/ceiling-tiers.md: cumulative, each the tier an action is blocked from.
    public const int CMUTierBlocksLightSupport = 1;
    public const int CMUTierBlocksDrops = 2;
    public const int CMUTierBlocksCAS = 3;
    public const int CMUTierBlocksOrbitalBombardment = 4;

    private EntityQuery<CMUZLevelMapComponent> _cmuZMapQuery;

    private void CMUInitializeCeiling()
    {
        _cmuZMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
    }

    /// <summary>
    /// The ceiling tier over a position. A deliberately graded area owns it; otherwise the world does:
    /// below the surface is 4, under a sealed sky is 3, open sky is 0.
    /// </summary>
    public int CMUTryGetCeilingTier(EntityCoordinates coordinates, out bool authored)
    {
        authored = false;
        if (TryGetArea(coordinates, out var area, out _) && area.Value.Comp.CeilingGraded)
        {
            authored = true;
            return CeilingTierOf(area.Value.Comp);
        }

        return CMUGeometricCeilingTier(_transform.ToMapCoordinates(coordinates));
    }

    public int CMUGeometricCeilingTier(MapCoordinates coordinates)
    {
        if (!_map.TryGetMap(coordinates.MapId, out var mapUid) || mapUid is not { } map)
            return 0;

        if (_cmuZMapQuery.TryComp(map, out var zMap) && zMap.Depth < 0)
            return 4;

        // Resolved lazily: the ordnance system depends on this one, so a field would be a cycle.
        return EntityManager.System<CMUTopDownOrdnanceSystem>().IsOpenToSky(coordinates) ? 0 : 3;
    }

    /// <summary>
    /// Aerial permission: runtime roof bubbles veto, the authored flag must permit, and the ceiling
    /// tier must be below the one that blocks the action.
    /// </summary>
    private bool CMUAreaPermits(
        EntityCoordinates coordinates,
        Func<AreaComponent, bool> authored,
        Predicate<Entity<RoofingEntityComponent>> roofDenied,
        int blockedFromTier,
        out bool roofed)
    {
        roofed = IsRoofed(coordinates, roofDenied);
        if (roofed)
            return false;

        if (TryGetArea(coordinates, out var area, out _) && !authored(area.Value.Comp))
            return false;

        return CMUTryGetCeilingTier(coordinates, out _) < blockedFromTier;
    }

    public bool CanCAS(EntityCoordinates coordinates, out bool roofed)
        => CMUAreaPermits(coordinates, a => a.CAS, r => !r.Comp.CanCAS, CMUTierBlocksCAS, out roofed);

    /// An authored CAS denial holds even where the tier is derived: whoever wrote it meant it.
    public bool CMUAuthoredDeniesCAS(EntityCoordinates coordinates)
        => TryGetArea(coordinates, out var area, out _) && !area.Value.Comp.CAS;
}
