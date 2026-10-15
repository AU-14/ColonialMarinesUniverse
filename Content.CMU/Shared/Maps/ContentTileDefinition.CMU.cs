using Content.Shared.Storage;

namespace Content.Shared.Maps;

/// <summary>
/// CMU tile settings, mainly for destroyable multi-Z grates and floors.
/// </summary>
public sealed partial class ContentTileDefinition
{
    /// <summary>
    /// Whether explosions may break this tile down to its base turf even when the base turf is space and vacuums are
    /// disabled. For tiles that are already open to the map's air, like multi-Z grates, so breaking them doesn't
    /// actually create a vacuum.
    /// </summary>
    [DataField]
    public bool BreakIgnoresVacuum;

    /// <summary>
    /// Items rolled and dropped in the tile's place when an explosion or a planted charge destroys it.
    /// Uses the same format as a StorageFill: id, prob, amount, maxAmount and orGroup.
    /// </summary>
    [DataField]
    public List<EntitySpawnEntry> DestroyDrops = new();

    /// <summary>
    /// Whether C4 and breaching charges can be planted directly on this tile to blow it open.
    /// </summary>
    [DataField]
    public bool AllowCharges;

    /// <summary>
    /// Whether this tile collapses once none of the four tiles around it are left, so destroyable tiles can't float
    /// on their own.
    /// </summary>
    [DataField]
    public bool CollapseWhenUnsupported;
}
