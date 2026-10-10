using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    // Linked floors share the battlefield's authorized feeds. Their own grid still supplies
    // the tile coordinates, and the reconstruction resolves each contact's actual depth.
    private bool TryGetTrackingMap(EntityUid grid, out Entity<TacticalMapComponent> map)
    {
        var owner = grid;
        if (TryComp<CMUZLevelMapComponent>(grid, out var level) &&
            TryComp<CMUZLevelsNetworkComponent>(level.NetworkUid, out var network))
        {
            IReadOnlyDictionary<int, EntityUid?> floors = network.ZLevels;
            if (floors.TryGetValue(0, out var ground) && ground is { } root && _tacticalMapQuery.HasComp(root))
                owner = root;
            else
            {
                var firstDepth = int.MaxValue;
                foreach (var (depth, floor) in floors)
                {
                    if (depth >= firstDepth || floor is not { } candidate || !_tacticalMapQuery.HasComp(candidate)) continue;
                    firstDepth = depth;
                    owner = candidate;
                }
            }
        }

        if (_tacticalMapQuery.TryComp(owner, out var component))
        {
            map = (owner, component);
            return true;
        }
        map = default;
        return false;
    }
}
