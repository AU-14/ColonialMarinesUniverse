using System.Numerics;
using Content.Server.CMU14.Atmos;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

// CMU14: grids without air of their own share the air of the grid beneath them.
public sealed partial class AtmosphereSystem
{
    private List<Entity<MapGridComponent>> _cmuBorrowGrids = new();

    private bool CMUTryGetBorrowedMixture(EntityUid grid, Vector2i tile, bool excite, out GasMixture? mixture)
    {
        mixture = null;
        if (!HasComp<CMUBorrowedAtmosphereComponent>(grid) || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var xform = Transform(grid);
        var local = (tile + new Vector2(0.5f)) * gridComp.TileSize;
        var world = Vector2.Transform(local, XformSystem.GetWorldMatrix(xform));

        _cmuBorrowGrids.Clear();
        _mapSystem.FindGridsIntersecting(xform.MapID, Box2.CenteredAround(world, new Vector2(0.01f)), ref _cmuBorrowGrids);
        foreach (var other in _cmuBorrowGrids)
        {
            if (other.Owner == grid || !_gridAtmosQuery.TryComp(other, out var atmosphere))
                continue;

            var otherTile = _mapSystem.WorldToTile(other.Owner, other.Comp, world);
            if (!atmosphere.Tiles.ContainsKey(otherTile))
                continue;

            mixture = GetTileMixture((other.Owner, atmosphere, null), xform.MapUid, otherTile, excite);
            return mixture != null;
        }

        return false;
    }
}
