using System.Linq;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Shows a vehicle's paint and dirt: the paint shader on its painted layers, its overlay's and its
/// mounted turret's, with the vehicle's colour and dirt, and a second one on its wheels with their own
/// colour. Only the art's paint pixels change colour or get dirty. The shader stays on even while the
/// vehicle is clean in its factory paint, since it is what draws those pixels opaque: their alpha
/// carries how soon they get dirty.
/// </summary>
public sealed class CMUVehiclePaintVisualSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private static readonly ProtoId<ShaderPrototype> PaintShader = "CMUJeepPaint";
    // The olive top tone the art was drawn in, and the mud splashed on the low bodywork.
    private static readonly Color Base = new(118, 126, 80);
    private static readonly Color Mud = new(98, 84, 58);

    private readonly Dictionary<EntityUid, Paint> _paints = new();

    private sealed class Paint(ShaderInstance body, ShaderInstance plain, ShaderInstance wheels)
    {
        public readonly ShaderInstance Body = body;
        public readonly ShaderInstance Plain = plain;
        public readonly ShaderInstance Wheels = wheels;
        public Color? Color;
        public Color? InteriorColor;
        public Color? SeatColor;
        public Color? WheelColor;
        public float Dirt = -1f;
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePaintComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var (_, paint) in _paints)
        {
            paint.Body.Dispose();
            paint.Plain.Dispose();
            paint.Wheels.Dispose();
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
            var dirt = CompOrNull<CMUVehicleGrimeComponent>(uid)?.Dirt ?? 0f;
            if (!_paints.TryGetValue(uid, out var entry))
            {
                entry = new Paint(NewShader(), NewShader(), NewShader());
                entry.Body.SetParameter("zoned", 1f);
                entry.Wheels.SetParameter("painted", 1f);
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

            if (entry.WheelColor != paint.WheelColor)
            {
                entry.Wheels.SetParameter("paint", paint.WheelColor);
                entry.WheelColor = paint.WheelColor;
            }

            if (!MathHelper.CloseTo(entry.Dirt, dirt))
            {
                entry.Body.SetParameter("dirt", dirt);
                entry.Plain.SetParameter("dirt", dirt);
                entry.Wheels.SetParameter("dirt", dirt);
                entry.Dirt = dirt;
            }

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

        old.Body.Dispose();
        old.Plain.Dispose();
        old.Wheels.Dispose();
    }

    private ShaderInstance NewShader()
    {
        var shader = _prototypes.Index(PaintShader).InstanceUnique();
        shader.SetParameter("base", Base);
        shader.SetParameter("mud", Mud);
        shader.SetParameter("paint", Base);
        shader.SetParameter("painted", 0f);
        return shader;
    }

    private void SetVehicle(EntityUid uid, SpriteComponent sprite, CMUVehiclePaintComponent paint, Paint shaders, bool on)
    {
        SetLayers((uid, sprite), paint.Layers, shaders.Body, on ? shaders.Body : null);
        SetLayers((uid, sprite), paint.PlainLayers, shaders.Plain, on ? shaders.Plain : null);
        SetLayers((uid, sprite), paint.WheelLayers, shaders.Wheels, on ? shaders.Wheels : null);
        if (TryComp(uid, out CMUVehicleOverlayComponent? overlay) &&
            TryComp(overlay.Overlay, out SpriteComponent? overlaySprite))
        {
            SetLayers((overlay.Overlay.Value, overlaySprite), paint.OverlayLayers, shaders.Body, on ? shaders.Body : null);
            SetLayers((overlay.Overlay.Value, overlaySprite), paint.PlainOverlayLayers, shaders.Plain, on ? shaders.Plain : null);
            SetLayers((overlay.Overlay.Value, overlaySprite), paint.WheelOverlayLayers, shaders.Wheels, on ? shaders.Wheels : null);
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
