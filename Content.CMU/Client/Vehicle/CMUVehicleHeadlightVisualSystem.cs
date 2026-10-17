using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
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
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ShaderPrototype> BeamShader = "CMUVehicleBeam";

    private readonly Dictionary<EntityUid, BeamShaderState> _shaders = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleHeadlightBeamComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var state in _shaders.Values)
        {
            state.Shader.Dispose();
        }

        _shaders.Clear();
    }

    private void OnShutdown(Entity<CMUVehicleHeadlightBeamComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent, out var state))
            return;

        if (TryComp(ent, out SpriteComponent? sprite) && _sprite.LayerMapTryGet((ent, sprite), "beam", out var layer, false))
            sprite.LayerSetShader(layer, (ShaderInstance?) null);

        state.Shader.Dispose();
    }

    public override void FrameUpdate(float frameTime)
    {
        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<CMUVehicleHeadlightBeamComponent, SpriteComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beam, out var sprite, out var light, out var xform))
        {
            // Out of view: nothing to draw until it comes back.
            if ((MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
                continue;

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
            var anchors = TryComp(beam.Vehicle, out CMUVehicleLampsComponent? lamps) &&
                          lamps.Lamps.Find(l => l.Id == beam.Lamp) is { BeamAnchors.Count: > 0 } lampData
                ? lampData.BeamAnchors
                : beam.Driver ? lights.DriverLampAnchors : lights.PassengerLampAnchors;
            var lamp = (-direction.ToAngle()).RotateVec(anchors.GetValueOrDefault(direction) / EyeManager.PixelsPerMeter);
            _sprite.LayerSetOffset((uid, sprite), layer, lamp + new Vector2(0f, -lights.BeamHalfLength));
            // Set on this client only: the offset follows this client's camera, and dirtying the
            // server's light from a frame update breaks prediction (debug clients assert on it).
            if (!light.Offset.EqualsApprox(lamp))
                light.Offset = lamp;

            if (!_shaders.TryGetValue(uid, out var beamShader))
            {
                beamShader = new BeamShaderState(_prototypes.Index(BeamShader).InstanceUnique());
                _shaders[uid] = beamShader;
                sprite.LayerSetShader(layer, beamShader.Shader, BeamShader.Id);
            }

            // Each parameter set re-uploads the shader's uniforms, so only on a change.
            var ambient = TryComp(xform.MapUid, out MapLightComponent? mapLight)
                ? mapLight.AmbientLightColor
                : Color.Black;
            var ambientRgb = new Vector3(ambient.R, ambient.G, ambient.B);
            if (beamShader.Ambient != ambientRgb)
            {
                beamShader.Shader.SetParameter("ambient", ambientRgb);
                beamShader.Ambient = ambientRgb;
            }

            if (!MathHelper.CloseTo(beamShader.Strength, beam.Strength))
            {
                beamShader.Shader.SetParameter("strength", beam.Strength);
                beamShader.Strength = beam.Strength;
            }
        }
    }

    private sealed class BeamShaderState(ShaderInstance shader)
    {
        public readonly ShaderInstance Shader = shader;
        public Vector3? Ambient;
        public float Strength = float.NaN;
    }
}
