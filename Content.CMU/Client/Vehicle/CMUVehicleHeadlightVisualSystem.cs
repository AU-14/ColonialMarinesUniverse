using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Puts a lit headlight beam's light and haze at its own lamp, as the jeep's art draws it in its
/// current direction, turning with the jeep's true heading. The haze shader only lets it show where
/// the beam's own light lands, above the map's ambient light: walls cut it off and daylight hides it.
/// </summary>
public sealed class CMUVehicleHeadlightVisualSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ShaderPrototype> BeamShader = "CMUJeepBeam";

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleHeadlightBeamComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _shaders.Clear();
    }

    private void OnShutdown(Entity<CMUVehicleHeadlightBeamComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent, out var shader))
            return;

        if (TryComp(ent, out SpriteComponent? sprite) && _sprite.LayerMapTryGet((ent, sprite), "beam", out var layer, false))
            sprite.LayerSetShader(layer, (ShaderInstance?) null);

        shader.Dispose();
    }

    public override void FrameUpdate(float frameTime)
    {
        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<CMUVehicleHeadlightBeamComponent, SpriteComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beam, out var sprite, out var light, out var xform))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), "beam", out var layer, false))
                continue;

            var lit = light.Enabled && TryComp(beam.Vehicle, out CMUVehicleHeadlightsComponent? lights);
            _sprite.LayerSetVisible((uid, sprite), layer, lit);
            if (!lit || !TryComp(beam.Vehicle, out lights))
                continue;

            // The jeep is drawn as its nearest cardinal frame turned by what is left of its heading;
            // the beam turns with the full heading, so the frame's lamp position is turned back into
            // the beam's own space. The light sits on the lens; the haze's cone starts there.
            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(
                _transform.GetWorldRotation(xform) + eyeRotation);
            var anchors = beam.Driver ? lights.DriverLampAnchors : lights.PassengerLampAnchors;
            var lamp = (-direction.ToAngle()).RotateVec(anchors.GetValueOrDefault(direction) / EyeManager.PixelsPerMeter);
            _sprite.LayerSetOffset((uid, sprite), layer, lamp + new Vector2(0f, -lights.BeamHalfLength));
            if (!light.Offset.EqualsApprox(lamp))
                _lights.SetOffset(uid, lamp, light);

            if (!_shaders.TryGetValue(uid, out var shader))
            {
                shader = _prototypes.Index(BeamShader).InstanceUnique();
                _shaders[uid] = shader;
                sprite.LayerSetShader(layer, shader, BeamShader.Id);
            }

            var ambient = TryComp(xform.MapUid, out MapLightComponent? mapLight)
                ? mapLight.AmbientLightColor
                : Color.Black;
            shader.SetParameter("ambient", new Vector3(ambient.R, ambient.G, ambient.B));
            shader.SetParameter("strength", beam.Strength);
        }
    }
}
