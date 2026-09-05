using Content.Client.Atmos.EntitySystems;
using Content.Client.Graphics;
using Content.Client.Resources;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Numerics;
using Color = Robust.Shared.Maths.Color;
using Texture = Robust.Client.Graphics.Texture;

namespace Content.Client.Atmos.Overlays;

/// <summary>
///     Overlay responsible for rendering heat distortion shader.
/// </summary>
public sealed partial class GasTileHeatBlurOverlay : Overlay
{
    public override bool RequestScreenTexture { get; set; } = true;
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> HeatOverlayShader = "HeatBlur";

    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _xformSys;
    private readonly ShaderInstance _shader;

    private readonly Texture _noiseTexture;
    private readonly Texture _heatGradientTexture;
    private List<Entity<MapGridComponent>> _intersectingGrids = new();
    private readonly OverlayResourceCache<CachedResources> _resources = new();

    // Overlay settings
    private const float
        ShaderSpilling = 2.5f; // for example 4f - spills shader one tile from hotspot, 2.5f - spills it half tile

    private const float ShaderStrength = 0.04f; // Makes waves stronger
    private const float ShaderScale = 1f; // Makes more waves
    private const float ShaderSpeed = 0.4f; // Makes waves run faster

    // Overlay settings for reduced motion setting
    private const float ShaderStrengthForReducedMotion = 0.01f;
    private const float ShaderScaleReducedMotion = 0.5f;
    private const float ShaderSpeedReducedMotion = 0.25f;

    private const int MinDistortionTemp = 300; // Distortion starts to show up at this temperature in Kelvins
    private const int MaxDistortionTemp = 2000; // Maximum distortion strength at this temperature in Kelvins

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public GasTileHeatBlurOverlay()
    {
        IoCManager.InjectDependencies(this);
        _maps = _entManager.System<SharedMapSystem>();
        _xformSys = _entManager.System<SharedTransformSystem>();

        _noiseTexture = _resourceCache.GetTexture("/Textures/Effects/HeatBlur/perlin_noise.png");
        _heatGradientTexture = _resourceCache.GetTexture("/Textures/Effects/HeatBlur/soft_circle.png");

        _shader = _proto.Index(HeatOverlayShader).InstanceUnique();
        _configManager.OnValueChanged(CCVars.DisableHeatDistortion, SetReducedMotion, invokeImmediately: true);
    }

    private void SetReducedMotion(bool reducedMotion)
    {
        _shader.SetParameter("strength_scale", reducedMotion ? ShaderStrengthForReducedMotion : ShaderStrength);
        _shader.SetParameter("spatial_scale", reducedMotion ? ShaderScaleReducedMotion : ShaderScale);
        _shader.SetParameter("speed_scale", reducedMotion ? ShaderSpeedReducedMotion : ShaderSpeed);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return CMUBeforeDrawHeat(args.Viewport, args.MapId, args.WorldAABB, args.WorldBounds, args.WorldHandle);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (ScreenTexture is null || res.HeatTarget is null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("NOISE_TEXTURE", _noiseTexture);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawTextureRect(res.HeatTarget.Texture, args.WorldBounds);

        args.WorldHandle.UseShader(null);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();

        _configManager.UnsubValueChanged(CCVars.DisableHeatDistortion, SetReducedMotion);
        base.DisposeBehavior();
    }

    /// <summary>
    /// Gets the strength of the heat distortion effect based on the temperature of the tile.
    /// The strength is a value between 0 and 1, where 0 means no distortion and 1 means maximum distortion.
    /// </summary>
    /// <param name="temp">The temperature of the tile.</param>
    /// <returns>The strength of the heat distortion effect.</returns>
    /// <seealso cref="ThermalByte"/>
    private static float GetHeatDistortionStrength(ThermalByte temp)
    {
        if (!temp.TryGetTemperature(out var kelvinTemp))
        {
            return 0f;
        }

        var strength = (kelvinTemp - MinDistortionTemp) / (MaxDistortionTemp - MinDistortionTemp);

        return MathHelper.Clamp01(strength);
    }

    internal sealed class CachedResources : IDisposable
    {
        public IRenderTexture? HeatTarget;

        public void Dispose()
        {
            HeatTarget?.Dispose();
        }
    }
}
