using Content.Shared.Atmos;
using Content.Shared._RMC14.Atmos;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Atmos.Reactions;

/// <summary>
/// Bridges burning gas tiles to RMC tile fires: while a fire reaction keeps a tile
/// hot, a real tile fire entity stands on it, giving mobs the RMC panic flow
/// (ignite, alert, pat or roll) instead of only wizden fire stacks.
/// </summary>
internal static class CMUGasFireBridge
{
    // One fire per tile; skipped while any tile fire (ours or a flamers') is present,
    // and respawned each tick once the tile clears, so it tracks the burning gas.
    internal static void SpawnTileFire(TileAtmosphere tile, EntProtoId fire)
    {
        var entManager = IoCManager.Resolve<IEntityManager>();
        var mapSystem = entManager.System<SharedMapSystem>();

        if (!entManager.TryGetComponent(tile.GridIndex, out MapGridComponent? grid))
            return;

        // Two dict lookups per burning tile per tick, no alloc. If refinery burns
        // ever profile hot, the upgrade is a per-grid tile fire in SharedRMCFlammableSystem
        var anchored = mapSystem.GetAnchoredEntities(tile.GridIndex, grid, tile.GridIndices);
        while (anchored.MoveNext(out var ent))
        {
            if (entManager.HasComponent<TileFireComponent>(ent))
                return;
        }

        entManager.SpawnEntity(fire, new EntityCoordinates(tile.GridIndex, tile.GridIndices));
    }
}
