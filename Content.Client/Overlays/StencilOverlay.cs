using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Parallax;
using Content.Client.Viewport;
using Content.Client.Weather;
using Content.Shared._CMU14.ZLevels;
using Content.Shared.Salvage;
using Content.Shared.Weather;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Overlays;

/// <summary>
/// Simple re-useable overlay with stencilled texture.
/// </summary>
public sealed partial class StencilOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleShader = "WorldGradientCircle";
    private static readonly ProtoId<ShaderPrototype> StencilMask = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilDraw = "StencilDraw";

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IConfigurationManager _config = default!;

    //RMC14
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly ParallaxSystem _parallax;
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly WeatherSystem _weather;

    //RMC14
    private readonly EntityLookupSystem _entLookup;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly OverlayResourceCache<CachedResources> _resources = new();

    private readonly ShaderInstance _shader;

    public StencilOverlay(ParallaxSystem parallax, SharedTransformSystem transform, SharedMapSystem map, SpriteSystem sprite, WeatherSystem weather, EntityLookupSystem entLookup)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _parallax = parallax;
        _transform = transform;
        _map = map;
        _sprite = sprite;
        _weather = weather;
        IoCManager.InjectDependencies(this);
        _shader = _protoManager.Index(CircleShader).InstanceUnique();

        //RMC14
        _entLookup = entLookup;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var mapUid = _map.GetMapOrInvalid(args.MapId);
        var invMatrix = args.Viewport.GetWorldToLocalMatrix();

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (res.Blep?.Texture.Size != args.Viewport.Size)
        {
            res.Blep?.Dispose();
            res.Blep = _clyde.CreateRenderTarget(args.Viewport.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "weather-stencil");
        }

        var drawWeather = args.Viewport.Eye is not ScalingViewport.ZEye { Depth: < 0 } ||
                          _config.GetCVar(CMUZLevelsCVars.WeatherLowerLayers);

        if (drawWeather && TryGetWeatherComponentForPass(args, mapUid, out var weatherMapUid, out var comp))
        {
            foreach (var (proto, weather) in comp.Weather)
            {
                if (!_protoManager.TryIndex<WeatherPrototype>(proto, out var weatherProto))
                    continue;

                var alpha = _weather.GetPercent(weather, weatherMapUid);
                DrawWeather(args, res, weatherProto, alpha, invMatrix);
            }
        }

        if (_entManager.TryGetComponent<RestrictedRangeComponent>(mapUid, out var restrictedRangeComponent))
        {
            DrawRestrictedRange(args, res, restrictedRangeComponent, invMatrix);
        }

        args.WorldHandle.UseShader(null);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Blep;

        public void Dispose()
        {
            Blep?.Dispose();
        }
    }

    private bool TryGetWeatherComponentForPass(
        in OverlayDrawArgs args,
        EntityUid mapUid,
        out EntityUid weatherMapUid,
        out WeatherComponent comp)
    {
        if (_entManager.TryGetComponent<WeatherComponent>(mapUid, out var mapWeather) &&
            mapWeather.Weather.Count > 0)
        {
            weatherMapUid = mapUid;
            comp = mapWeather;
            return true;
        }

        if (args.Viewport.Eye is not ScalingViewport.ZEye zEye ||
            zEye.WeatherSourceMapId == MapId.Nullspace ||
            zEye.WeatherSourceMapId == args.MapId)
        {
            weatherMapUid = default;
            comp = default!;
            return false;
        }

        weatherMapUid = _map.GetMapOrInvalid(zEye.WeatherSourceMapId);
        if (weatherMapUid == EntityUid.Invalid ||
            !_entManager.TryGetComponent<WeatherComponent>(weatherMapUid, out var sourceWeather) ||
            sourceWeather.Weather.Count == 0)
        {
            weatherMapUid = default;
            comp = default!;
            return false;
        }

        comp = sourceWeather;
        return true;
    }
}
