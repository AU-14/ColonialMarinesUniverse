using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Spreader;

/// <summary>
/// Raised when trying to spread to neighboring tiles.
/// If the spread is no longer able to happen you MUST cancel this event!
/// </summary>
[ByRefEvent]
public record struct SpreadNeighborsEvent
{
    public ValueList<(MapGridComponent Grid, TileRef Tile)> NeighborFreeTiles;
    public ValueList<EntityUid> Neighbors;
    public ValueList<EntityCoordinates> NeighborFreeCoordinates; // CMU14: air without a terrain grid.

    /// <summary>
    /// How many updates allowed are remaining.
    /// Subscribers can handle as they wish.
    /// </summary>
    public int Updates;
}
