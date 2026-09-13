using System.Numerics;
using Content.Shared._RMC14.Weather;
using Content.Shared.Light.Components;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private void DrawWeather(
        in OverlayDrawArgs args,
        HashSet<Entity<WeatherStatusEffectComponent, StatusEffectComponent>> weathers)
    {
        var worldHandle = args.WorldHandle;
        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var tileOffset = args.Viewport.Eye is { } eye
            ? (-eye.Rotation).ToWorldVec() * -0.5f
            : Vector2.Zero;

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        var stencil = _gridStencil.GetTileStencil(args,
            "weather-blocked",
            "weather-blocked-grid-stencil",
            (grid, tile) =>
            {
                _entManager.TryGetComponent(grid.Owner, out RoofComponent? roofComp);
                // Ignored tiles for stencil.
                return !_weather.CanWeatherAffect((grid.Owner, grid.Comp, roofComp), tile);
            },
            ignoreEmpty: false,
            queryExpansion: 1f,
            tileOffset: tileOffset,
            drawAdditionalMask: DrawWeatherBlockers);

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_protoManager.Index(StencilMask).Instance());
        worldHandle.DrawTextureRect(stencil.Texture, worldBounds);
        var curTime = _timing.RealTime;

        foreach (var (uid, weather, status) in weathers)
        {
            var alpha = _weather.GetWeatherPercent((uid, status));
            var sprite = _sprite.GetFrame(weather.Sprite, curTime);

            // Draw the rain
            worldHandle.UseShader(_protoManager.Index(StencilDraw).Instance());
            _parallax.DrawParallax(worldHandle,
                worldAABB,
                sprite,
                curTime,
                position,
                weather.Scrolling ?? Vector2.Zero,
                modulate: (weather.Color ?? Color.White).WithAlpha(alpha));
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);
    }

    private void DrawWeatherBlockers(DrawingHandleWorld worldHandle)
    {
        if (_playerManager.LocalEntity is not { } player ||
            !_entManager.TryGetComponent(player, out TransformComponent? playerXform))
        {
            return;
        }

        var playerCoordinates = _transform.GetMapCoordinates(player, playerXform);
        var query = _entManager.EntityQueryEnumerator<RMCBlockWeatherComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out _, out var xform))
        {
            if (xform.MapID != playerCoordinates.MapId)
                continue;

            var roofBounds = _entLookup.GetAABBNoContainer(entity,
                _transform.GetWorldPosition(xform),
                _transform.GetWorldRotation(xform));

            if (roofBounds.Contains(playerCoordinates.Position))
                worldHandle.DrawRect(roofBounds, Color.White);
        }
    }
}
