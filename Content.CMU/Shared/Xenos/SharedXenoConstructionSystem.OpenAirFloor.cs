using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Xenonids.Construction;

public sealed partial class SharedXenoConstructionSystem
{
    // multi-z roofs and grates are flagged isSpace so they share the map's outdoor air, not because they're void.
    // breakIgnoresVacuum is the flag that marks exactly those tiles, so treat them as real floor for resin (BUG-778)
    private bool IsSpaceForConstruction(TileRef tile)
    {
        if (!_turf.IsSpace(tile))
            return false;

        return tile.Tile.IsEmpty || !_turf.GetContentTileDefinition(tile).BreakIgnoresVacuum;
    }
}
