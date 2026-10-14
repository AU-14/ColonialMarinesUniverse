using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws the rim designs of a vehicle's wheel sets. Each rim layer follows the wheel or spare layer it
/// sits over: the rim map beside that layer's state (rims_&lt;state&gt;, or the off-road state's for
/// road tyres), on the same frame, offset and tint, shown while the layer shows and has a map, which
/// only intact wheels do. Each rim layer has its own shader, set to the design of the wheel set in
/// that layer's slot and its colours; the shader works the design out from those numbers, so no
/// texture is handed to it.
/// </summary>
public sealed class CMUVehicleRimVisualSystem : EntitySystem
{
    [Dependency] private CMUVehiclePaintSystem _paint = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> RimShader = "CMUVehicleRim";
    private const string MapPrefix = "rims_";

    private readonly Dictionary<(EntityUid Vehicle, EntityUid Sprite, string Layer), Rims> _rims = new();
    private readonly List<(EntityUid Vehicle, EntityUid Sprite, string Layer)> _stale = new();

    private sealed class Rims(ShaderInstance shader)
    {
        public readonly ShaderInstance Shader = shader;
        public CMURimDesignPrototype? Design;
        public readonly List<Color> Colors = new();
        public float Dirt = -1f;
    }

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleWheelVisualizerSystem));
        UpdatesAfter.Add(typeof(CMUOpenVehicleVisualSystem));
        UpdatesAfter.Add(typeof(CMUVehicleOverlayVisualSystem));
    }

    public override void Shutdown()
    {
        foreach (var rims in _rims.Values)
        {
            rims.Shader.Dispose();
        }

        _rims.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        // A vehicle gone or no longer painted frees its shaders here.
        _stale.Clear();
        foreach (var key in _rims.Keys)
        {
            if (!HasComp<CMUVehiclePaintComponent>(key.Vehicle) || TerminatingOrDeleted(key.Sprite))
                _stale.Add(key);
        }

        foreach (var key in _stale)
        {
            if (!TerminatingOrDeleted(key.Sprite) &&
                TryComp(key.Sprite, out SpriteComponent? stale) &&
                _sprite.LayerMapTryGet((key.Sprite, stale), key.Layer, out var layer, false))
            {
                stale.LayerSetShader(layer, (ShaderInstance?) null);
                _sprite.LayerSetVisible((key.Sprite, stale), layer, false);
            }

            _rims[key].Shader.Dispose();
            _rims.Remove(key);
        }

        var query = EntityQueryEnumerator<CMUVehiclePaintComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var paint, out var sprite))
        {
            if (paint.RimLayers.Count == 0 && paint.RimOverlayLayers.Count == 0)
                continue;

            var dirt = CompOrNull<CMUVehicleGrimeComponent>(uid)?.Dirt ?? 0f;
            Mirror(uid, (uid, sprite), paint.RimLayers, paint.WheelLayers, dirt);
            if (TryComp(uid, out CMUVehicleOverlayComponent? overlay) &&
                TryComp(overlay.Overlay, out SpriteComponent? overlaySprite))
            {
                Mirror(uid, (overlay.Overlay.Value, overlaySprite), paint.RimOverlayLayers, paint.WheelOverlayLayers, dirt);
            }
        }
    }

    /// <summary>
    /// The rim map for a wheel or spare state. Tyres with their own art keep their rims where the
    /// plain state has them, so they use its map.
    /// </summary>
    private string? MapState(RSI rsi, string state)
    {
        if (rsi.TryGetState(MapPrefix + state, out _))
            return MapPrefix + state;

        foreach (var tyre in _prototypes.EnumeratePrototypes<CMUVehicleTyrePrototype>())
        {
            if (tyre.StatePrefix.Length > 0 &&
                state.StartsWith(tyre.StatePrefix) &&
                rsi.TryGetState(MapPrefix + state[tyre.StatePrefix.Length..], out _))
            {
                return MapPrefix + state[tyre.StatePrefix.Length..];
            }
        }

        return null;
    }

    private void Mirror(
        EntityUid vehicle,
        Entity<SpriteComponent> ent,
        Dictionary<string, string> layers,
        Dictionary<string, string> wheelLayers,
        float dirt)
    {
        foreach (var (key, sourceKey) in layers)
        {
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
                continue;

            CMURimDesignPrototype? design = null;
            Entity<CMUVehicleWheelPaintComponent>? wheels = null;
            if (wheelLayers.TryGetValue(sourceKey, out var slot) &&
                _paint.TryGetWheelPaint(vehicle, slot, out wheels) &&
                wheels.Value.Comp.RimDesign is { } id)
            {
                _prototypes.TryIndex(id, out design);
            }

            if (design == null ||
                wheels == null ||
                !_sprite.LayerMapTryGet(ent.AsNullable(), sourceKey, out var sourceIndex, false) ||
                ent.Comp[sourceIndex] is not SpriteComponent.Layer source ||
                source.State.Name is not { } sourceState ||
                source.ActualRsi is not { } rsi ||
                MapState(rsi, sourceState) is not { } state)
            {
                _sprite.LayerSetVisible(ent.AsNullable(), index, false);
                continue;
            }

            var rims = Update((vehicle, ent.Owner, key), design, Colors(wheels.Value.Comp, design), dirt);
            if (_sprite.LayerGetRsiState(ent.AsNullable(), index) != state)
                _sprite.LayerSetRsiState(ent.AsNullable(), index, state);

            _sprite.LayerSetVisible(ent.AsNullable(), index, source.Visible);
            _sprite.LayerSetColor(ent.AsNullable(), index, source.Color);
            _sprite.LayerSetOffset(ent.AsNullable(), index, source.Offset);
            _sprite.LayerSetAutoAnimated(ent.AsNullable(), index, source.AutoAnimated);
            _sprite.LayerSetAnimationTime(ent.AsNullable(), index, source.AnimationTime);
            if (ent.Comp[index] is SpriteComponent.Layer layer && layer.Shader != rims.Shader)
                ent.Comp.LayerSetShader(index, rims.Shader, RimShader.Id);
        }
    }

    /// <summary>
    /// A rim layer's shader, set to the design and colours it should draw.
    /// </summary>
    private Rims Update(
        (EntityUid Vehicle, EntityUid Sprite, string Layer) key,
        CMURimDesignPrototype design,
        List<Color> colors,
        float dirt)
    {
        if (!_rims.TryGetValue(key, out var rims))
        {
            rims = new Rims(_prototypes.Index(RimShader).InstanceUnique());
            _rims[key] = rims;
        }

        var shader = rims.Shader;
        if (rims.Design != design)
        {
            shader.SetParameter("pattern", (float) design.Pattern);
            shader.SetParameter("count", (float) design.Count);
            shader.SetParameter("width", design.Width);
            shader.SetParameter("twist", design.Twist);
            shader.SetParameter("holeRadius", design.HoleRadius);
            shader.SetParameter("holeSize", design.HoleSize);
            shader.SetParameter("cap", design.Cap);
            shader.SetParameter("lip", design.Lip);
            shader.SetParameter("nuts", (float) design.Nuts);
            shader.SetParameter("nutRadius", design.NutRadius);
            shader.SetParameter("lipBolts", (float) design.LipBolts);
            shader.SetParameter("regions", new Vector3(
                design.Regions.Contains(CMURimRegion.Accent) ? 1f : 0f,
                design.Regions.Contains(CMURimRegion.Lip) ? 1f : 0f,
                design.Regions.Contains(CMURimRegion.Cap) ? 1f : 0f));
            rims.Design = design;
        }

        if (!rims.Colors.SequenceEqual(colors))
        {
            shader.SetParameter("baseColor", colors[0]);
            shader.SetParameter("accentColor", colors[1]);
            shader.SetParameter("lipColor", colors[2]);
            shader.SetParameter("capColor", colors[3]);
            rims.Colors.Clear();
            rims.Colors.AddRange(colors);
        }

        if (!MathHelper.CloseTo(rims.Dirt, dirt))
        {
            shader.SetParameter("dirt", dirt);
            rims.Dirt = dirt;
        }

        return rims;
    }

    /// <summary>
    /// A wheel set's design colours: its paint as the base, then the accent, lip and cap.
    /// </summary>
    public static List<Color> Colors(CMUVehicleWheelPaintComponent wheels, CMURimDesignPrototype design)
    {
        var colors = new List<Color> { wheels.Color };
        if (wheels.RimColors.Count == 3)
            colors.AddRange(wheels.RimColors);
        else
            colors.AddRange(new[] { design.Accent, design.LipColor, design.CapColor });

        return colors;
    }

    /// <summary>
    /// A design on a wheel, for the painter's previews: its rim on frame 0 inside a knobby tyre, one
    /// pixel each, as big as the wheels are drawn.
    /// </summary>
    public static Image<Rgba32> PreviewImage(CMURimDesignPrototype design, IReadOnlyList<Color> colors)
    {
        const int tyre = 9;
        var size = 2 * tyre + 1;
        var image = new Image<Rgba32>(size, size);
        var rubber = new Rgba32(44, 44, 43);
        var groove = new Rgba32(30, 30, 29);
        for (var u = -tyre; u <= tyre; u++)
        {
            for (var v = -tyre; v <= tyre; v++)
            {
                var dist = MathF.Sqrt(u * u + v * v);
                if (dist > 8.4f)
                    continue;

                Rgba32 pixel;
                if (CMURimDesigns.OnRim(u, v))
                {
                    var (region, tone) = CMURimDesigns.Sample(design, u, v, 0);
                    var color = CMURimDesigns.Shade(region, tone, colors[0], colors[1], colors[2], colors[3]);
                    pixel = new Rgba32(color.R, color.G, color.B, 1f);
                }
                else
                {
                    // The tread's lugs, 13 round the tyre like the art's.
                    var lug = (MathF.Atan2(v, u) / (MathF.Tau / 13f) % 1f + 1f) % 1f < 0.4f;
                    pixel = dist > 7.6f && lug ? groove : rubber;
                }

                image[u + tyre, tyre - v] = pixel;
            }
        }

        return image;
    }
}
