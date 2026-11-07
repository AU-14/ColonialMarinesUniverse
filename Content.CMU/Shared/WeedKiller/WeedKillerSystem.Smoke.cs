using Content.Shared.CMU14.WeedKiller;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.WeedKiller;

public sealed partial class WeedKillerSystem
{
    private void PublishSmoke(List<(Entity<MapGridComponent> Grid, Vector2i Indices)> positions)
    {
        // Bound individual messages; all batches share a start time, including clients without this grid in PVS yet.
        const int batchSize = 1024;
        var grids = new Dictionary<EntityUid, List<Vector2i>>();
        foreach (var (grid, indices) in positions)
        {
            if (!grids.TryGetValue(grid, out var tiles))
                grids.Add(grid, tiles = new List<Vector2i>());
            tiles.Add(indices);
        }

        foreach (var (grid, tiles) in grids)
        {
            if (!TryComp(grid, out TransformComponent? transform))
                continue;

            var filter = Filter.BroadcastMap(transform.MapID);
            var netGrid = GetNetEntity(grid);
            for (var offset = 0; offset < tiles.Count; offset += batchSize)
            {
                var batch = new Vector2i[Math.Min(batchSize, tiles.Count - offset)];
                tiles.CopyTo(offset, batch, 0, batch.Length);
                RaiseNetworkEvent(new CMUWeedKillerSmokeEvent(netGrid, batch, _timing.CurTime), filter);
            }
        }
    }
}
