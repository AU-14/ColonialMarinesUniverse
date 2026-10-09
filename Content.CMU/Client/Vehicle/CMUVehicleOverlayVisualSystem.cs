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
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Keeps a vehicle's over-rider overlay on the same cardinal frame as the vehicle, and drives its
/// near wheels, frame damage, lamps, brake lights and turn signals from the vehicle's state.
/// </summary>
public sealed class CMUVehicleOverlayVisualSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private const float PixelsPerMeter = 32f;
    private const float StoppedSpeed = 0.05f;
    private const float TurningDegrees = 5f;
    // Both signal states blink on the same two-frame cycle; this keeps every lamp in step.
    private const double SignalPeriod = 0.8;

    // This frame's body lift per vehicle, in the vehicle's frame, for its mounted turrets to follow.
    private readonly Dictionary<EntityUid, (Vector2 Lift, Direction Direction)> _lifts = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleExactCardinalDirectionSystem));
        UpdatesAfter.Add(typeof(VehicleTurretVisualSystem));
        UpdatesAfter.Add(typeof(VehicleWheelVisualizerSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        _lifts.Clear();
        var query = EntityQueryEnumerator<CMUVehicleOverlayVisualsComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var visuals, out var sprite, out var xform))
        {
            var ent = (uid, sprite);
            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(
                _transform.GetWorldRotation(xform) + _eye.CurrentEye.Rotation);

            var vehicle = xform.ParentUid;
            var bob = CMUVehicleBob.Offset(vehicle, CompOrNull<GridVehicleMoverComponent>(vehicle), _timing.CurTime);
            var lift = (-direction.ToAngle()).RotateVec(new Vector2(0f, bob / PixelsPerMeter));
            _lifts[vehicle] = (lift, direction);
            ApplyDirection(ent, visuals, direction, bob);
            BobBody(vehicle, lift);
            MirrorWheels(ent, vehicle);
            MirrorDamage(ent, vehicle);
            UpdateLights(ent, visuals, vehicle);
        }

        BobTurrets();
    }

    /// <summary>
    /// Lifts the vehicle's own layers by the suspension bob; the wheels stay on the ground.
    /// </summary>
    private void BobBody(EntityUid vehicle, Vector2 lift)
    {
        if (!TryComp(vehicle, out SpriteComponent? sprite))
            return;

        _sprite.LayerMapTryGet((vehicle, sprite), VehicleWheelLayers.Wheels, out var wheels, false);
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetOffset((vehicle, sprite), i, i == wheels ? Vector2.Zero : lift);
        }
    }

    /// <summary>
    /// Lifts a mounted turret with its vehicle's body. The turret's visual turns on the vehicle and
    /// draws its layers in its own frame, so the body's lift is turned back by the turret's aim.
    /// </summary>
    private void BobTurrets()
    {
        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turrets.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (!TryGetEntity(visual.Turret, out var turret) ||
                !_turret.TryGetVehicle(turret.Value, out var vehicle) ||
                !_lifts.TryGetValue(vehicle, out var frame))
            {
                continue;
            }

            // Facing north the crew stands between the camera and the gun, so the gun drops to the
            // riders' depth and y-sorts behind them; otherwise it stays over them, at the depth the
            // turret visual system gives it.
            var depth = frame.Direction == Direction.North
                ? (int) DrawDepth.Mobs
                : (int) DrawDepth.OverMobs + (HasComp<VehicleTurretAttachmentComponent>(turret) ? 1 : 0);
            if (sprite.DrawDepth != depth)
                _sprite.SetDrawDepth((uid, sprite), depth);

            var lift = frame.Lift;

            var aim = _transform.GetWorldRotation(uid) - _transform.GetWorldRotation(vehicle);
            var offset = (-aim).RotateVec(lift);

            // The exact cardinal system shows a turret's cardinal frame and only turns four-way
            // states back, so an eight-way gun would be drawn as that frame turned by the whole
            // heading. Pick the gun's frame from its aim on the vehicle's drawn frame instead, and
            // turn it back the same way, so it sits in the body's perspective at any heading.
            var gun = (frame.Direction.ToAngle() + aim).GetDir();
            for (var i = 0; i < sprite.AllLayers.Count(); i++)
            {
                _sprite.LayerSetOffset((uid, sprite), i, offset);

                var layer = sprite[i];
                if (layer.ActualRsi is { } rsi &&
                    rsi.TryGetState(layer.RsiState, out var state) &&
                    state.RsiDirections == RsiDirectionType.Dir8)
                {
                    sprite.DirectionOverride = gun;
                    _sprite.LayerSetRotation((uid, sprite), i, -gun.ToAngle());
                }
            }
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

    /// <summary>
    /// Shows the overlay's frame damage as the frame damage visualizer shows the vehicle's, fading in
    /// as the frame's integrity drops.
    /// </summary>
    private void MirrorDamage(Entity<SpriteComponent> ent, EntityUid vehicle)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), VehicleFrameDamageLayers.DamagedFrame, out var index, false))
            return;

        if (!TryComp(vehicle, out SpriteComponent? vehicleSprite) ||
            !_sprite.LayerMapTryGet((vehicle, vehicleSprite), VehicleFrameDamageLayers.DamagedFrame, out var source, false))
        {
            _sprite.LayerSetVisible(ent.AsNullable(), index, false);
            return;
        }

        var damage = vehicleSprite[source];
        _sprite.LayerSetVisible(ent.AsNullable(), index, damage.Visible);
        _sprite.LayerSetColor(ent.AsNullable(), index, damage.Color);
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

            // Steering blinks the turn signals unless the driver has switched that off.
            var steer = mover.AngularVelocityDegrees * MathF.Sign(mover.CurrentSpeed);
            var auto = !TryComp(vehicle, out CMUVehicleDriverActionsComponent? actions) || actions.AutoSignals;
            if (auto && speed > StoppedSpeed)
            {
                left = steer > TurningDegrees;
                right = steer < -TurningDegrees;
            }
        }

        // The driver's hazard switch blinks both sides, moving or not.
        if (TryComp(vehicle, out CMUVehicleDriverActionsComponent? driver) && driver.Hazards)
            left = right = true;

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
