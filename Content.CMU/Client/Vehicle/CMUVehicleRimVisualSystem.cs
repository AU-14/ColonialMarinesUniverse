using System.Linq;
using Content.Client._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws a vehicle's rim design. Each rim layer follows the wheel or spare layer it sits over: the
/// rim map beside that layer's state (rims_&lt;state&gt;, or the off-road state's for road tyres), on
/// the same frame, offset and tint, shown while the layer shows and has a map, which only intact
/// wheels do. The rim shader reads the map and
/// paints the design, baked here into a small texture of its colours whenever it or they change.
/// </summary>
public sealed class CMUVehicleRimVisualSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> RimShader = "CMUJeepRim";
    private const string MapPrefix = "rims_";

    private readonly Dictionary<EntityUid, Rims> _rims = new();

    private sealed class Rims(ShaderInstance shader) : IDisposable
    {
        public readonly ShaderInstance Shader = shader;
        public OwnedTexture? Texture;
        public string? Design;
        public readonly List<Color> Colors = new();
        public float Dirt = -1f;

        public void Dispose()
        {
            Shader.Dispose();
            Texture?.Dispose();
        }
    }

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleWheelVisualizerSystem));
        UpdatesAfter.Add(typeof(CMUJeepVisualSystem));
        UpdatesAfter.Add(typeof(CMUVehicleOverlayVisualSystem));
    }

    public override void Shutdown()
    {
        foreach (var rims in _rims.Values)
        {
            rims.Dispose();
        }

        _rims.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        // The paint visuals already take the paint's shutdown; a vehicle gone or without a design
        // frees its shader and texture here.
        foreach (var (uid, stale) in _rims)
        {
            if (TryComp(uid, out CMUVehiclePaintComponent? kept) && kept.RimDesign != null)
                continue;

            stale.Dispose();
            _rims.Remove(uid);
        }

        var query = EntityQueryEnumerator<CMUVehiclePaintComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var paint, out var sprite))
        {
            if (paint.RimLayers.Count == 0 && paint.RimOverlayLayers.Count == 0)
                continue;

            CMURimDesignPrototype? design = null;
            if (paint.RimDesign is { } id)
                _prototypes.TryIndex(id, out design);

            var rims = design == null ? null : Update(uid, paint, design);
            Mirror((uid, sprite), paint.RimLayers, rims);
            if (TryComp(uid, out CMUVehicleOverlayComponent? overlay) &&
                TryComp(overlay.Overlay, out SpriteComponent? overlaySprite))
            {
                Mirror((overlay.Overlay.Value, overlaySprite), paint.RimOverlayLayers, rims);
            }
        }
    }

    /// <summary>
    /// The vehicle's rim shader, its design texture baked again if the design or its colours changed.
    /// </summary>
    private Rims Update(EntityUid uid, CMUVehiclePaintComponent paint, CMURimDesignPrototype design)
    {
        if (!_rims.TryGetValue(uid, out var rims))
        {
            rims = new Rims(_prototypes.Index(RimShader).InstanceUnique());
            _rims[uid] = rims;
        }

        var colors = Colors(paint, design);
        if (rims.Design != design.ID || !rims.Colors.SequenceEqual(colors))
        {
            using var image = DesignImage(design, colors);
            rims.Texture?.Dispose();
            rims.Texture = _clyde.LoadTextureFromImage(image, $"rims-{uid}");
            rims.Shader.SetParameter("design", rims.Texture);
            rims.Design = design.ID;
            rims.Colors.Clear();
            rims.Colors.AddRange(colors);
        }

        var dirt = CompOrNull<CMUVehicleGrimeComponent>(uid)?.Dirt ?? 0f;
        if (!MathHelper.CloseTo(rims.Dirt, dirt))
        {
            rims.Shader.SetParameter("dirt", dirt);
            rims.Dirt = dirt;
        }

        return rims;
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

    private void Mirror(Entity<SpriteComponent> ent, Dictionary<string, string> layers, Rims? rims)
    {
        foreach (var (key, sourceKey) in layers)
        {
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
                continue;

            if (rims == null ||
                !_sprite.LayerMapTryGet(ent.AsNullable(), sourceKey, out var sourceIndex, false) ||
                ent.Comp[sourceIndex] is not SpriteComponent.Layer source ||
                source.State.Name is not { } sourceState ||
                source.ActualRsi is not { } rsi ||
                MapState(rsi, sourceState) is not { } state)
            {
                _sprite.LayerSetVisible(ent.AsNullable(), index, false);
                continue;
            }

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
    /// A vehicle's design colours: the wheels' paint as the base, then the accent, lip and cap.
    /// </summary>
    public static List<Color> Colors(CMUVehiclePaintComponent paint, CMURimDesignPrototype design)
    {
        var colors = new List<Color> { paint.WheelColor };
        if (paint.RimColors.Count == 3)
            colors.AddRange(paint.RimColors);
        else
            colors.AddRange(new[] { design.Accent, design.LipColor, design.CapColor });

        return colors;
    }

    /// <summary>
    /// A design's colours for the rim shader: one rim per frame, side by side, v up.
    /// </summary>
    public static Image<Rgba32> DesignImage(CMURimDesignPrototype design, IReadOnlyList<Color> colors)
    {
        var size = CMURimDesigns.Size;
        var radius = CMURimDesigns.Radius;
        var image = new Image<Rgba32>(size * CMURimDesigns.Frames, size);
        for (var frame = 0; frame < CMURimDesigns.Frames; frame++)
        {
            for (var u = -radius; u <= radius; u++)
            {
                for (var v = -radius; v <= radius; v++)
                {
                    if (!CMURimDesigns.OnRim(u, v))
                        continue;

                    var (region, tone) = CMURimDesigns.Sample(design, u, v, frame);
                    var color = CMURimDesigns.Shade(region, tone, colors[0], colors[1], colors[2], colors[3]);
                    image[frame * size + u + radius, radius - v] = new Rgba32(color.R, color.G, color.B, 1f);
                }
            }
        }

        return image;
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
