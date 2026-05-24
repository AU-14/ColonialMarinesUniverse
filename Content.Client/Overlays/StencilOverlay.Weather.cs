using System.Numerics;
using Content.Shared._RMC14.Weather;
using Content.Shared.Light.Components;
using Content.Shared.Weather;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private List<Entity<MapGridComponent>> _grids = new();

    private void DrawWeather(in OverlayDrawArgs args, WeatherPrototype weatherProto, float alpha, Matrix3x2 invMatrix)
    {
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB.Enlarged(1f); //CrystallEdge: Enlarged(1), because ignoreEmpty disabled, and that cause borderscreen weather flickering
        var worldBounds = args.WorldBounds;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var eye = args.Viewport.Eye; //CrystallEdge: we need Eye for calculation of isometric wall offset direction

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        worldHandle.RenderInRenderTarget(_blep!, () =>
        {
            var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
            _grids.Clear();

            // idk if this is safe to cache in a field and clear sloth help
            _mapManager.FindGridsIntersecting(mapId, worldAABB, ref _grids);

            foreach (var grid in _grids)
            {
                var matrix = _transform.GetWorldMatrix(grid, xformQuery);
                var matty =  Matrix3x2.Multiply(matrix, invMatrix);
                worldHandle.SetTransform(matty);
                _entManager.TryGetComponent(grid.Owner, out RoofComponent? roofComp);

                foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid, worldAABB, ignoreEmpty: false)) //CrystallEdge: ignoreEmpty: false, because we can have empty tiles under zLevel roof
                {
                    // Ignored tiles for stencil
                    if (_weather.CanWeatherAffect(grid.Owner, grid, tile, roofComp))
                    {
                        continue;
                    }

                    //CrystallEdge offset - required for isometric walls
                    if (eye is not null)
                    {
                        Angle rotation = eye.Rotation * -1f;
                        var offset = rotation.ToWorldVec() * -0.5f;
                        var gridTile = new Box2(
                            tile.GridIndices * grid.Comp.TileSize + offset,
                            (tile.GridIndices + Vector2i.One) * grid.Comp.TileSize + offset);
                        worldHandle.DrawRect(gridTile, Color.White);
                    }
                    //CrystallEdge offset end
                }
            }

            // RMC14
            if (_entManager.TryGetComponent(_playerManager.LocalEntity, out TransformComponent? playerXform))
            {
                var playerPos = _transform.GetMapCoordinates(_playerManager.LocalEntity!.Value, playerXform).Position;

                var query = _entManager.EntityQueryEnumerator<RMCBlockWeatherComponent>();
                while (query.MoveNext(out var entity, out _))
                {
                    var roofBounds = _entLookup.GetAABBNoContainer(entity,
                        _transform.GetWorldPosition(entity),
                        _transform.GetWorldRotation(entity));

                    if (roofBounds.Contains(playerPos))
                        worldHandle.DrawRect(roofBounds, Color.White);
                }
            }

        }, Color.Transparent);

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_protoManager.Index(StencilMask).Instance());
        worldHandle.DrawTextureRect(_blep!.Texture, worldBounds);
        var curTime = _timing.RealTime;
        var sprite = _sprite.GetFrame(weatherProto.Sprite, curTime);

        // Draw the rain
        worldHandle.UseShader(_protoManager.Index(StencilDraw).Instance());
        _parallax.DrawParallax(worldHandle, worldAABB, sprite, curTime, position, Vector2.Zero, modulate: (weatherProto.Color ?? Color.White).WithAlpha(alpha));

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);
    }
}
