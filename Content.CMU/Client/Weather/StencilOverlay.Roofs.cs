using Content.Client.Light.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private readonly Dictionary<Vector2i, Color> _weatherRoofColors = new();
    private readonly HashSet<Vector2i> _weatherExactRoofTiles = new();

    private GridStencilSystem.TileStencilPredicate CreateWeatherStencilPredicate(Box2Rotated worldBounds)
    {
        var roofs = _entManager.System<SharedRoofSystem>();
        EntityUid? batchGrid = null;
        RoofComponent? roof = null;
        return (grid, tile) =>
        {
            // The stencil enumerates one grid at a time. Build only when its
            // cached texture needs refreshing, including the stencil's margin.
            if (batchGrid != grid.Owner)
            {
                batchGrid = grid.Owner;
                _entManager.TryGetComponent(grid.Owner, out roof);
                if (roof != null)
                {
                    var bounds = _transform.GetInvWorldMatrix(grid.Owner)
                        .TransformBox(worldBounds.Enlarged(1f).CalcBoundingBox());
                    roofs.GetEntityRoofColors((grid.Owner, grid.Comp, roof), bounds,
                        _weatherRoofColors, _weatherExactRoofTiles);
                }
            }

            var roofed = roof != null && roofs.IsRooved((grid.Owner, grid.Comp, roof), tile.GridIndices,
                _weatherExactRoofTiles.Contains(tile.GridIndices), _weatherRoofColors.ContainsKey(tile.GridIndices));
            return !_weather.CanWeatherAffect((grid.Owner, grid.Comp, roof), tile, roofed);
        };
    }
}
