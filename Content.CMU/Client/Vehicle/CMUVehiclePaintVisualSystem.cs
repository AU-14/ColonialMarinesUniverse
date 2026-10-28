using System.Linq;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Shows a vehicle's paint and dirt: the paint shader on its painted layers, its overlay's and its
/// mounted turret's, with the vehicle's colour and dirt, and one more on each wheel set (the road
/// wheels, the spare) with that set's own colour. Only the art's paint pixels change colour or get dirty. The shader stays on even while the
/// vehicle is clean in its factory paint, since it is what draws those pixels opaque: their alpha
/// carries how soon they get dirty.
/// </summary>
public sealed class CMUVehiclePaintVisualSystem : EntitySystem
{
    [Dependency] private CMUVehiclePaintSystem _paint = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private static readonly ProtoId<ShaderPrototype> PaintShader = "CMUVehiclePaint";
    // The olive top tone the art was drawn in, and the mud splashed on the low bodywork.
    private static readonly Color Base = new(118, 126, 80);
    private static readonly Color Mud = new(98, 84, 58);
    private static readonly Color FactoryWheels = new CMUVehicleWheelPaintComponent().Color;

    private readonly Dictionary<EntityUid, Paint> _paints = new();

    private sealed class Paint(ShaderInstance body, ShaderInstance plain)
    {
        public readonly ShaderInstance Body = body;
        public readonly ShaderInstance Plain = plain;

        // By the item slot of the wheel set they draw.
        public readonly Dictionary<string, (ShaderInstance Shader, Color Color)> Wheels = new();
        public Color? Color;
        public Color? InteriorColor;
        public Color? SeatColor;
        public float Dirt = -1f;

        public void Dispose()
        {
            Body.Dispose();
            Plain.Dispose();
            foreach (var (shader, _) in Wheels.Values)
            {
                shader.Dispose();
            }
        }
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePaintComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var (_, paint) in _paints)
        {
            paint.Dispose();
        }

        _paints.Clear();
    }

    private void OnShutdown(Entity<CMUVehiclePaintComponent> ent, ref ComponentShutdown args)
    {
        Unpaint(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehiclePaintComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var paint, out var sprite))
        {
            // Out of view: nothing to draw until it comes back.
            if ((MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
                continue;

            var dirt = CompOrNull<CMUVehicleGrimeComponent>(uid)?.Dirt ?? 0f;
            if (!_paints.TryGetValue(uid, out var entry))
            {
                entry = new Paint(NewShader(), NewShader());
                entry.Body.SetParameter("zoned", 1f);
                _paints[uid] = entry;
            }

            if (entry.Color != paint.Color)
            {
                foreach (var shader in new[] { entry.Body, entry.Plain })
                {
                    shader.SetParameter("paint", paint.Color ?? Base);
                    shader.SetParameter("painted", paint.Color == null ? 0f : 1f);
                }

                entry.Color = paint.Color;
            }

            if (entry.InteriorColor != paint.InteriorColor)
            {
                entry.Body.SetParameter("interior", paint.InteriorColor ?? Base);
                entry.Body.SetParameter("paintedInterior", paint.InteriorColor == null ? 0f : 1f);
                entry.InteriorColor = paint.InteriorColor;
            }

            if (entry.SeatColor != paint.SeatColor)
            {
                entry.Body.SetParameter("seats", paint.SeatColor ?? Base);
                entry.Body.SetParameter("paintedSeats", paint.SeatColor == null ? 0f : 1f);
                entry.SeatColor = paint.SeatColor;
            }

            if (!MathHelper.CloseTo(entry.Dirt, dirt))
            {
                entry.Body.SetParameter("dirt", dirt);
                entry.Plain.SetParameter("dirt", dirt);
                foreach (var (shader, _) in entry.Wheels.Values)
                {
                    shader.SetParameter("dirt", dirt);
                }

                entry.Dirt = dirt;
            }

            UpdateWheels(uid, paint.WheelLayers, entry, dirt);
            UpdateWheels(uid, paint.WheelOverlayLayers, entry, dirt);

            SetVehicle(uid, sprite, paint, entry, true);
        }

        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turrets.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (TryGetEntity(visual.Turret, out var turret) &&
                _turret.TryGetVehicle(turret.Value, out var vehicle) &&
                _paints.TryGetValue(vehicle, out var paint))
            {
                SetLayer((uid, sprite), 0, paint.Body, paint.Body);
            }
        }
    }

    /// <summary>
    /// Takes the vehicle's paint shader off every layer still drawing with it, then frees it.
    /// </summary>
    private void Unpaint(Entity<CMUVehiclePaintComponent> ent)
    {
        if (!_paints.Remove(ent, out var old))
            return;

        if (TryComp(ent, out SpriteComponent? sprite))
            SetVehicle(ent, sprite, ent.Comp, old, false);

        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turrets.MoveNext(out var uid, out _, out var turretSprite))
        {
            SetLayer((uid, turretSprite), 0, old.Body, null);
        }

        old.Dispose();
    }

    /// <summary>
    /// Gives each wheel set the layers draw its own shader, in the set's colour.
    /// </summary>
    private void UpdateWheels(EntityUid uid, Dictionary<string, string> layers, Paint entry, float dirt)
    {
        foreach (var slot in layers.Values)
        {
            var color = _paint.TryGetWheelPaint(uid, slot, out var wheels) ? wheels.Value.Comp.Color : FactoryWheels;
            if (!entry.Wheels.TryGetValue(slot, out var current))
            {
                var shader = NewShader();
                shader.SetParameter("painted", 1f);
                shader.SetParameter("dirt", dirt);
                shader.SetParameter("paint", color);
                entry.Wheels[slot] = (shader, color);
                continue;
            }

            if (current.Color == color)
                continue;

            current.Shader.SetParameter("paint", color);
            entry.Wheels[slot] = (current.Shader, color);
        }
    }

    /// <summary>
    /// A paint shader with every uniform set: one left unset keeps whatever the last vehicle drawn
    /// with the shader had, painting its seats or tub on every other vehicle.
    /// </summary>
    private ShaderInstance NewShader()
    {
        var shader = _prototypes.Index(PaintShader).InstanceUnique();
        shader.SetParameter("base", Base);
        shader.SetParameter("mud", Mud);
        shader.SetParameter("paint", Base);
        shader.SetParameter("painted", 0f);
        shader.SetParameter("interior", Base);
        shader.SetParameter("paintedInterior", 0f);
        shader.SetParameter("seats", Base);
        shader.SetParameter("paintedSeats", 0f);
        shader.SetParameter("zoned", 0f);
        shader.SetParameter("dirt", 0f);
        return shader;
    }

    private void SetVehicle(EntityUid uid, SpriteComponent sprite, CMUVehiclePaintComponent paint, Paint shaders, bool on)
    {
        SetLayers((uid, sprite), paint.Layers, shaders.Body, on ? shaders.Body : null);
        SetLayers((uid, sprite), paint.PlainLayers, shaders.Plain, on ? shaders.Plain : null);
        SetWheelLayers((uid, sprite), paint.WheelLayers, shaders, on);
        if (TryComp(uid, out CMUVehicleOverlayComponent? overlay) &&
            TryComp(overlay.Overlay, out SpriteComponent? overlaySprite))
        {
            SetLayers((overlay.Overlay.Value, overlaySprite), paint.OverlayLayers, shaders.Body, on ? shaders.Body : null);
            SetLayers((overlay.Overlay.Value, overlaySprite), paint.PlainOverlayLayers, shaders.Plain, on ? shaders.Plain : null);
            SetWheelLayers((overlay.Overlay.Value, overlaySprite), paint.WheelOverlayLayers, shaders, on);
        }
    }

    private void SetWheelLayers(Entity<SpriteComponent> ent, Dictionary<string, string> layers, Paint shaders, bool on)
    {
        foreach (var (key, slot) in layers)
        {
            if (shaders.Wheels.TryGetValue(slot, out var wheels) && _sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
                SetLayer(ent, index, wheels.Shader, on ? wheels.Shader : null);
        }
    }

    private void SetLayers(Entity<SpriteComponent> ent, List<string> keys, ShaderInstance ours, ShaderInstance? shader)
    {
        foreach (var key in keys)
        {
            if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
                SetLayer(ent, index, ours, shader);
        }
    }

    /// <summary>
    /// Sets a layer's shader to <paramref name="shader"/> if it has none or ours, so another
    /// system's shader is never replaced or removed.
    /// </summary>
    private static void SetLayer(Entity<SpriteComponent> ent, int index, ShaderInstance ours, ShaderInstance? shader)
    {
        if (index >= ent.Comp.AllLayers.Count() || ent.Comp[index] is not SpriteComponent.Layer layer)
            return;

        var current = layer.Shader;
        if (current == shader || current != null && current != ours)
            return;

        ent.Comp.LayerSetShader(index, shader, shader == null ? null : PaintShader.Id);
    }
}
