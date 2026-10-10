using System.Linq;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Shows a vehicle's paint and dirt: the paint shader on its painted layers, its overlay's and its
/// mounted turret's, with the vehicle's colour and dirt. Only the art's paint pixels change colour or
/// get dirty. The shader stays on even while the vehicle is clean in its factory paint, since it is
/// what draws those pixels opaque: their alpha carries how soon they get dirty.
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

    private readonly Dictionary<EntityUid, (ShaderInstance Shader, Color? Color, float Dirt)> _paints = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePaintComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var (_, paint) in _paints)
        {
            paint.Shader.Dispose();
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
                entry = (_prototypes.Index(PaintShader).InstanceUnique(), null, -1f);
                entry.Shader.SetParameter("base", Base);
                entry.Shader.SetParameter("mud", Mud);
                entry.Shader.SetParameter("paint", Base);
                entry.Shader.SetParameter("painted", 0f);
            }

            if (entry.Color != paint.Color)
            {
                entry.Shader.SetParameter("paint", paint.Color ?? Base);
                entry.Shader.SetParameter("painted", paint.Color == null ? 0f : 1f);
                entry.Color = paint.Color;
            }

            if (!MathHelper.CloseTo(entry.Dirt, dirt))
            {
                entry.Shader.SetParameter("dirt", dirt);
                entry.Dirt = dirt;
            }

            _paints[uid] = entry;
            SetVehicle(uid, sprite, paint, entry.Shader, entry.Shader);
        }

        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turrets.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (TryGetEntity(visual.Turret, out var turret) &&
                _turret.TryGetVehicle(turret.Value, out var vehicle) &&
                _paints.TryGetValue(vehicle, out var paint))
            {
                SetLayer((uid, sprite), 0, paint.Shader, paint.Shader);
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
            SetVehicle(ent, sprite, ent.Comp, old.Shader, null);

        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turrets.MoveNext(out var uid, out _, out var turretSprite))
        {
            SetLayer((uid, turretSprite), 0, old.Shader, null);
        }

        old.Shader.Dispose();
    }

    private void SetVehicle(EntityUid uid, SpriteComponent sprite, CMUVehiclePaintComponent paint, ShaderInstance ours, ShaderInstance? shader)
    {
        SetLayers((uid, sprite), paint.Layers, ours, shader);
        if (TryComp(uid, out CMUVehicleOverlayComponent? overlay) &&
            TryComp(overlay.Overlay, out SpriteComponent? overlaySprite))
        {
            SetLayers((overlay.Overlay.Value, overlaySprite), paint.OverlayLayers, ours, shader);
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
