using Content.Shared.CMU14.Ship;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Shuttles;

public sealed partial class RMCShuttleSystem
{
    [Dependency] private CMUZeroGSpaceSystem _cmuZeroG = default!;

    private bool CMUSkipDepartureDeathTile(EntProtoId spawnedEntity, MapCoordinates coordinate)
    {
        // The legacy kill floor made an abandoned berth out of bounds. Other FTL spawn effects
        // and departures from maps that still use those bounds retain their existing behavior.
        return spawnedEntity.Id == "FloorDeathEntity"
               && _mapSystem.TryGetMap(coordinate.MapId, out var map)
               && _cmuZeroG.IsZeroGMap(map.Value);
    }
}
