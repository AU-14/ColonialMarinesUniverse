using Robust.Shared.Map;

namespace Content.Server.AU14.Scenario;

/// <summary>
/// A map, grid, or all-maps boundary for round-world discovery.
/// </summary>
public readonly record struct RoundWorldScope
{
    private RoundWorldScope(bool allMaps, MapId mapId, EntityUid? gridUid)
    {
        IncludesEveryMap = allMaps;
        MapId = mapId;
        GridUid = gridUid;
    }

    public bool IncludesEveryMap { get; }
    public MapId MapId { get; }
    public EntityUid? GridUid { get; }

    public static RoundWorldScope EveryMap()
    {
        return new RoundWorldScope(true, MapId.Nullspace, null);
    }

    public static RoundWorldScope Map(MapId mapId)
    {
        return new RoundWorldScope(false, mapId, null);
    }

    public static RoundWorldScope Grid(MapId mapId, EntityUid gridUid)
    {
        return new RoundWorldScope(false, mapId, gridUid);
    }
}
