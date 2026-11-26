using Content.Shared._RMC14.Areas;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Dropship.Weapon;

public abstract partial class SharedDropshipWeaponSystem
{
    /// <summary>
    /// Column gate. An authored tier is absolute: declared roofs are not carved. A geometric tier 3 is
    /// opened one fire sequence at a time by a breaching munition, and tier 4 rock is reached only by
    /// munitions that say so, as deep as their rating.
    /// </summary>
    private bool CMUCanFireAtColumn(EntityCoordinates coordinates, int zLevelPenetration, int breachRadius, bool penetratesRock)
    {
        if (CasDebug || _area.CanCAS(coordinates, out var roofed))
            return true;

        if (roofed)
            return false;

        var tier = _area.CMUTryGetCeilingTier(coordinates, out var authored);
        var target = _transform.ToMapCoordinates(coordinates);

        if (tier >= AreaSystem.CMUTierBlocksOrbitalBombardment)
            return penetratesRock && CMUCountSealedLevelsAbove(target) <= zLevelPenetration;

        if (authored || _area.CMUAuthoredDeniesCAS(coordinates))
            return false;

        if (!CMUTryFindFirstSealedLevel(target, out var level, out var sealedLevel))
            return true;

        // An indestructible ceiling can be neither carved nor passed. Refusing here saves the munition.
        if (_topDownOrdnance.IsIndestructible(sealedLevel))
            return false;

        return level <= zLevelPenetration && breachRadius > 0;
    }

    /// <summary>
    /// The first ceiling above the target that is not an opening, counted from 1 at the level directly above.
    /// </summary>
    private bool CMUTryFindFirstSealedLevel(MapCoordinates target, out int level, out MapCoordinates sealedLevel)
    {
        level = 0;
        sealedLevel = default;
        if (!_map.TryGetMap(target.MapId, out var mapUid) || mapUid is not { } map)
            return false;

        for (var offset = 1; _zLevels.TryMapOffset((map, null), offset, out _, out var above); offset++)
        {
            var candidate = new MapCoordinates(target.Position, above.MapId);
            if (_topDownOrdnance.IsOpening(candidate))
                continue;

            level = offset;
            sealedLevel = candidate;
            return true;
        }

        return false;
    }

    private int CMUCountSealedLevelsAbove(MapCoordinates target)
    {
        if (!_map.TryGetMap(target.MapId, out var mapUid) || mapUid is not { } map)
            return 0;

        var count = 0;
        for (var offset = 1; _zLevels.TryMapOffset((map, null), offset, out _, out var above); offset++)
        {
            if (!_topDownOrdnance.IsOpening(new MapCoordinates(target.Position, above.MapId)))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Breach-first: a breaching munition over a derived tier 3 roof takes the payload on the first
    /// sealed level and opens it. Nothing falls through on that sequence. Rock never opens.
    /// </summary>
    private bool CMUTryResolveBreach(AmmoInFlightComponent flight, EntityCoordinates landing, MapCoordinates target, out MapCoordinates breachAt)
    {
        breachAt = default;
        if (flight.BreachRadius <= 0)
            return false;

        var tier = _area.CMUTryGetCeilingTier(landing, out var authored);
        if (authored || tier >= AreaSystem.CMUTierBlocksOrbitalBombardment)
            return false;

        return CMUTryFindFirstSealedLevel(target, out _, out breachAt);
    }

    /// <summary>
    /// The strongest reach among the munitions a fire mission will actually fire, so the start gate
    /// asks the same question the mission's shots will. Any munition that can strike lets the mission
    /// start; impacts that cannot are skipped per shot.
    /// </summary>
    private void CMUMissionReach(Entity<DropshipComponent> dropship, FireMissionData missionData, out int penetration, out int breachRadius, out bool penetratesRock)
    {
        penetration = 0;
        breachRadius = 0;
        penetratesRock = false;

        foreach (var weaponOffset in missionData.WeaponOffsets)
        {
            if (weaponOffset.Offset == null)
                continue;

            var weapon = GetEntity(weaponOffset.WeaponId);
            if (!TryGetWeaponLocation(weapon, out _) ||
                !_dropship.TryGetGridDropship(weapon, out var owner) ||
                owner != dropship ||
                !TryGetWeaponAmmo(weapon, out var ammo))
            {
                continue;
            }

            penetration = Math.Max(penetration, ammo.Comp.ZLevelPenetration);
            breachRadius = Math.Max(breachRadius, ammo.Comp.BreachRadius);
            penetratesRock |= ammo.Comp.PenetratesRock;
        }
    }
}
