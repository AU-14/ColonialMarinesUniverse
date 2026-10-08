using System.Numerics;
using System.Linq;
using Content.Client._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Keeps a vehicle's over-rider overlay on the same cardinal frame as the vehicle, and drives its
/// near wheels, lamps, brake lights and turn signals from the vehicle's state.
/// </summary>
public sealed class CMUVehicleOverlayVisualSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float PixelsPerMeter = 32f;
    private const float StoppedSpeed = 0.05f;
    private const float TurningDegrees = 5f;
    // Both signal states blink on the same two-frame cycle; this keeps every lamp in step.
    private const double SignalPeriod = 0.8;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleExactCardinalDirectionSystem));
        UpdatesAfter.Add(typeof(VehicleWheelVisualizerSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehicleOverlayVisualsComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var visuals, out var sprite, out var xform))
        {
            var ent = (uid, sprite);
            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(
                _transform.GetWorldRotation(xform) + _eye.CurrentEye.Rotation);

            var vehicle = xform.ParentUid;
            var bob = CMUVehicleBob.Offset(vehicle, CompOrNull<GridVehicleMoverComponent>(vehicle), _timing.CurTime);
            ApplyDirection(ent, visuals, direction, bob);
            BobBody(vehicle, direction, bob);
            MirrorWheels(ent, vehicle);
            UpdateLights(ent, visuals, vehicle);
        }
    }

    /// <summary>
    /// Lifts the vehicle's own layers by the suspension bob; the wheels stay on the ground.
    /// </summary>
    private void BobBody(EntityUid vehicle, Direction direction, float bob)
    {
        if (!TryComp(vehicle, out SpriteComponent? sprite))
            return;

        var offset = (-direction.ToAngle()).RotateVec(new Vector2(0f, bob / PixelsPerMeter));
        _sprite.LayerMapTryGet((vehicle, sprite), VehicleWheelLayers.Wheels, out var wheels, false);
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetOffset((vehicle, sprite), i, i == wheels ? Vector2.Zero : offset);
        }
    }

    private void ApplyDirection(Entity<SpriteComponent> ent, CMUVehicleOverlayVisualsComponent visuals, Direction direction, float bob)
    {
        var sprite = ent.Comp;
        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = direction;
        sprite.NoRotation = true;
        _sprite.SetGranularLayersRendering(ent.AsNullable(), true);

        var counter = -direction.ToAngle();
        visuals.DirectionOffsets.TryGetValue(direction, out var slide);
        var lift = new Vector2(0f, bob);
        var slideOffset = counter.RotateVec((slide + lift) / PixelsPerMeter);
        var groundOffset = counter.RotateVec(slide / PixelsPerMeter);
        _sprite.LayerMapTryGet(ent.AsNullable(), "wheels", out var wheels, false);
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetRenderingStrategy(ent.AsNullable(), i, LayerRenderingStrategy.Default);
            _sprite.LayerSetOffset(ent.AsNullable(), i, i == wheels ? groundOffset : slideOffset);

            var layer = sprite[i];
            if (layer.ActualRsi is { } rsi &&
                rsi.TryGetState(layer.RsiState, out var state) &&
                state.RsiDirections is RsiDirectionType.Dir4 or RsiDirectionType.Dir8)
            {
                _sprite.LayerSetRotation(ent.AsNullable(), i, counter);
            }
        }

        foreach (var (key, offsets) in visuals.LayerOffsets)
        {
            if (offsets.TryGetValue(direction, out var pixels) &&
                _sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            {
                _sprite.LayerSetOffset(ent.AsNullable(), index, counter.RotateVec((pixels + lift) / PixelsPerMeter));
            }
        }
    }

    private void MirrorWheels(Entity<SpriteComponent> ent, EntityUid vehicle)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), "wheels", out var index, false))
            return;

        if (!TryComp(vehicle, out SpriteComponent? vehicleSprite) ||
            !_sprite.LayerMapTryGet((vehicle, vehicleSprite), VehicleWheelLayers.Wheels, out var source, false) ||
            vehicleSprite[source] is not SpriteComponent.Layer wheels)
        {
            _sprite.LayerSetVisible(ent.AsNullable(), index, false);
            return;
        }

        var state = wheels.State.Name?.Replace("wheels_", "wheels_overlay_");
        if (state != null && _sprite.LayerGetRsiState(ent.AsNullable(), index) != state)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state);

        _sprite.LayerSetVisible(ent.AsNullable(), index, wheels.Visible);
        _sprite.LayerSetColor(ent.AsNullable(), index, wheels.Color);
        _sprite.LayerSetAutoAnimated(ent.AsNullable(), index, wheels.AutoAnimated);
        _sprite.LayerSetAnimationTime(ent.AsNullable(), index, wheels.AnimationTime);
    }

    private void UpdateLights(Entity<SpriteComponent> ent, CMUVehicleOverlayVisualsComponent visuals, EntityUid vehicle)
    {
        var lightsOn = TryComp(vehicle, out VehicleSpotlightComponent? spotlight) && spotlight.Enabled;
        var braking = false;
        var left = false;
        var right = false;

        if (TryComp(vehicle, out GridVehicleMoverComponent? mover))
        {
            var speed = MathF.Abs(mover.CurrentSpeed);
            if (speed > StoppedSpeed && speed < visuals.LastSpeed - 0.001f)
                visuals.BrakeUntil = _timing.CurTime + visuals.BrakeHold;

            visuals.LastSpeed = speed;
            braking = _timing.CurTime < visuals.BrakeUntil;

            var steer = mover.AngularVelocityDegrees * MathF.Sign(mover.CurrentSpeed);
            if (speed <= StoppedSpeed)
            {
                left = right = lightsOn;
            }
            else
            {
                left = steer > TurningDegrees;
                right = steer < -TurningDegrees;
            }
        }

        SetVisible(ent, "lights", lightsOn);
        SetVisible(ent, "headlights_on", lightsOn);
        SetVisible(ent, "brake", braking);
        SetVisible(ent, "signal_left", left);
        SetVisible(ent, "signal_right", right);

        var blink = (float) (_timing.CurTime.TotalSeconds % SignalPeriod);
        foreach (var key in new[] { "signal_left", "signal_right" })
        {
            if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
                _sprite.LayerSetAnimationTime(ent.AsNullable(), index, blink);
        }
    }

    private void SetVisible(Entity<SpriteComponent> ent, string key, bool visible)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            _sprite.LayerSetVisible(ent.AsNullable(), index, visible);
    }
}
