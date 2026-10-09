using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Paints a vehicle's crayon drawings into per-direction textures: the parts under the riders on the
/// vehicle, the parts over them on its overlay, blended so the panel shows through.
/// </summary>
public sealed class CMUVehicleCrayonSystem : SharedCMUVehicleCrayonSystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string Layer = "crayon";
    private const float Opacity = 0.75f;
    private const float BloodOpacity = 0.9f;

    private readonly Dictionary<ResPath, Image<Rgba32>?> _maps = new();
    private readonly Dictionary<string, Image<Rgba32>?> _decals = new();
    private readonly Dictionary<EntityUid, Paint> _paints = new();

    private sealed class Paint
    {
        // The drawings list is refilled in place by each state, so a new state marks the paint stale.
        public bool Stale = true;

        // What the paint was baked with: the overlay's slide, and the panels it left out while open.
        public CMUVehicleOverlayVisualsComponent? Visuals;
        public int OpenPanels;

        public readonly OwnedTexture?[] Under = new OwnedTexture?[4];
        public readonly OwnedTexture?[] Over = new OwnedTexture?[4];

        public void Dispose()
        {
            for (var i = 0; i < 4; i++)
            {
                Under[i]?.Dispose();
                Over[i]?.Dispose();
                Under[i] = Over[i] = null;
            }
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(VehicleExactCardinalDirectionSystem));
        UpdatesAfter.Add(typeof(CMUVehicleOverlayVisualSystem));
        SubscribeLocalEvent<CMUVehicleCrayonComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUVehicleCrayonComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<CMUVehicleCrayonComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_paints.TryGetValue(ent, out var paint))
            paint.Stale = true;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var paint in _paints.Values)
        {
            paint.Dispose();
        }

        foreach (var image in _maps.Values.Concat(_decals.Values))
        {
            image?.Dispose();
        }

        _paints.Clear();
        _maps.Clear();
        _decals.Clear();
    }

    private void OnShutdown(Entity<CMUVehicleCrayonComponent> ent, ref ComponentShutdown args)
    {
        if (_paints.Remove(ent, out var paint))
            paint.Dispose();
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehicleCrayonComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var crayon, out var sprite))
        {
            if (crayon.Drawings.Count == 0 && crayon.Blood.Count == 0 && !_paints.ContainsKey(uid))
                continue;

            Entity<SpriteComponent>? overlay = null;
            CMUVehicleOverlayVisualsComponent? overlayVisuals = null;
            if (TryComp(uid, out CMUVehicleOverlayComponent? over) &&
                TryComp(over.Overlay, out SpriteComponent? overSprite))
            {
                overlay = (over.Overlay.Value, overSprite);
                TryComp(over.Overlay, out overlayVisuals);
            }

            if (!_paints.TryGetValue(uid, out var paint))
                _paints[uid] = paint = new Paint();

            // The overlay can arrive after the drawings, and a panel swinging changes what shows.
            var jeep = CompOrNull<CMUJeepComponent>(uid);
            var openPanels = OpenPanels(jeep);
            if (paint.Stale ||
                !ReferenceEquals(paint.Visuals, overlayVisuals) ||
                paint.OpenPanels != openPanels)
            {
                paint.Stale = false;
                paint.Visuals = overlayVisuals;
                paint.OpenPanels = openPanels;
                Repaint(crayon, paint, overlayVisuals, jeep);
            }

            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(
                _transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation);
            var index = Array.IndexOf(MapDirections, direction);
            var counter = -direction.ToAngle();

            ShowLayer((uid, sprite), "fuel_door", paint.Under[index], counter);
            if (overlay != null)
                ShowLayer(overlay.Value, "hood", paint.Over[index], counter);
        }
    }

    private void ShowLayer(Entity<SpriteComponent> ent, string after, Texture? texture, Angle counter)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), Layer, out var index, false))
        {
            if (texture == null)
                return;

            var at = _sprite.LayerMapTryGet(ent.AsNullable(), after, out var anchor, false) ? anchor + 1 : (int?) null;
            index = _sprite.AddTextureLayer(ent.AsNullable(), texture, at);
            _sprite.LayerMapSet(ent.AsNullable(), Layer, index);
        }

        _sprite.LayerSetTexture(ent.AsNullable(), index, texture);
        _sprite.LayerSetVisible(ent.AsNullable(), index, texture != null);
        _sprite.LayerSetRotation(ent.AsNullable(), index, counter);
    }

    private void Repaint(CMUVehicleCrayonComponent crayon, Paint paint, CMUVehicleOverlayVisualsComponent? overlayVisuals, CMUJeepComponent? jeep)
    {
        paint.Dispose();
        if (crayon.Drawings.Count == 0 && crayon.Blood.Count == 0 || GetImage(_maps, crayon.Map) is not { } map)
            return;

        for (var i = 0; i < MapDirections.Length; i++)
        {
            var direction = MapDirections[i];
            var slide = 0;
            if (overlayVisuals != null && overlayVisuals.DirectionOffsets.TryGetValue(direction, out var offset))
                slide = (int) MathF.Round(offset.Y);

            using var under = new Image<Rgba32>(FrameSize, FrameSize);
            using var overImage = new Image<Rgba32>(FrameSize, FrameSize);
            var anyUnder = false;
            var anyOver = false;

            for (var y = 0; y < FrameSize; y++)
            {
                for (var x = 0; x < FrameSize; x++)
                {
                    var (px, py) = MapPixel(direction, x, y);
                    var surface = Pixel(map, px, py);
                    // The map has the panels shut, so an open one would leave its paint hanging in the air.
                    if (!DecodeSurface(surface.R, surface.G, surface.B, surface.A, out var voxel, out var front, out var overRiders, out var panel) ||
                        IsPanelOpen(jeep, panel))
                    {
                        continue;
                    }

                    // Crayon goes on over blood.
                    if ((PaintPixel(crayon.Drawings, voxel, front, direction, Opacity) ??
                         PaintPixel(crayon.Blood, voxel, front, direction, BloodOpacity)) is not { } color)
                        continue;

                    if (overRiders)
                    {
                        if (y + slide < FrameSize)
                            overImage[x, y + slide] = color;

                        anyOver = true;
                    }
                    else
                    {
                        under[x, y] = color;
                        anyUnder = true;
                    }
                }
            }

            paint.Under[i] = anyUnder ? _clyde.LoadTextureFromImage(under) : null;
            paint.Over[i] = anyOver ? _clyde.LoadTextureFromImage(overImage) : null;
        }
    }

    /// <summary>
    /// The colour the newest drawing covering this surface point paints, or null.
    /// </summary>
    private Rgba32? PaintPixel(List<CMUCrayonDrawing> drawings, Vector3 voxel, bool front, Direction direction, float opacity)
    {
        for (var i = drawings.Count - 1; i >= 0; i--)
        {
            var drawing = drawings[i];
            if (DecalPixel(drawing, voxel, front, direction) is not { } uv || GetDecal(drawing.Decal) is not { } decal)
                continue;

            var texel = Pixel(decal, Math.Min(uv.X, decal.Width - 1), Math.Min(uv.Y, decal.Height - 1));
            if (texel.A == 0)
                continue;

            var c = drawing.Color;
            return new Rgba32(
                c.R * texel.R / 255f,
                c.G * texel.G / 255f,
                c.B * texel.B / 255f,
                c.A * texel.A / 255f * opacity);
        }

        return null;
    }

    private Image<Rgba32>? GetDecal(string id)
    {
        if (_decals.TryGetValue(id, out var image))
            return image;

        if (_prototypes.TryIndex<DecalPrototype>(id, out var decal) &&
            decal.Sprite is SpriteSpecifier.Rsi rsi)
        {
            var path = new ResPath("/Textures") / rsi.RsiPath / $"{rsi.RsiState}.png";
            image = GetImage(path);
        }

        _decals[id] = image;
        return image;
    }

    public override bool TryGetSurface(ResPath map, Direction direction, int x, int y, out Vector3 voxel, out bool front, out bool overRiders, out CMUCrayonPanel panel)
    {
        voxel = default;
        front = false;
        overRiders = false;
        panel = CMUCrayonPanel.Body;
        if (x < 0 || y < 0 || x >= FrameSize || y >= FrameSize || GetImage(_maps, map) is not { } image)
            return false;

        var (px, py) = MapPixel(direction, x, y);
        var pixel = Pixel(image, px, py);
        return DecodeSurface(pixel.R, pixel.G, pixel.B, pixel.A, out voxel, out front, out overRiders, out panel);
    }

    private static Rgba32 Pixel(Image<Rgba32> image, int x, int y)
    {
        return image.GetPixelSpan()[y * image.Width + x];
    }

    private Image<Rgba32>? GetImage(Dictionary<ResPath, Image<Rgba32>?> cache, ResPath path)
    {
        if (cache.TryGetValue(path, out var image))
            return image;

        image = GetImage(path);
        cache[path] = image;
        return image;
    }

    private Image<Rgba32>? GetImage(ResPath path)
    {
        if (!_resources.TryContentFileRead(path, out var stream))
            return null;

        using (stream)
        {
            return Image.Load<Rgba32>(stream);
        }
    }
}
