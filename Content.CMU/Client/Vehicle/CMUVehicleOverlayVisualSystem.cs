using System.Numerics;
using System.Linq;
using Content.Client._RMC14.Vehicle;
using Content.Client._RMC14.Sprite;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Keeps a vehicle's over-rider overlay on the same cardinal frame as the vehicle, and drives its
/// near wheels, frame damage, lamps, brake lights and turn signals from the vehicle's state.
/// </summary>
public sealed class CMUVehicleOverlayVisualSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private CMUOpenVehicleSystem _jeep = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private const float PixelsPerMeter = 32f;
    // How far a wrecked hull sags on its wheels, in pixels.
    private const float HullSag = 2f;
    // Half the height of a vehicle frame, in pixels.
    private const float FrameHalf = 48f;
    private const float StoppedSpeed = 0.05f;
    private const float TurningDegrees = 5f;
    // Both signal states blink on the same two-frame cycle; this keeps every lamp in step.
    private const double SignalPeriod = 0.8;


    // This frame's body lift per vehicle, in the vehicle's frame, for its mounted turrets to follow.
    private readonly Dictionary<EntityUid, (Vector2 Lift, Direction Direction)> _lifts = new();

    // Client-side glows each overlay's vehicle throws from its tail lights and turn signals. The tail
    // lights glow dim red, brighter braking; a blinking signal amber.
    private static readonly EntProtoId GlowPrototype = "CMUVehicleLampGlow";
    private static readonly Color TailGlow = Color.FromHex("#FF2A1A");
    private static readonly Color SignalGlow = Color.FromHex("#FFA62A");
    private const float TailEnergy = 1f;
    private const float BrakeEnergy = 1.8f;
    private const float SignalEnergy = 1.5f;
    // How far out from its lamp a glow sits, in tiles, so it lights the ground past the bumper rather
    // than under the jeep.
    private const float GlowReach = 0.35f;
    private readonly Dictionary<EntityUid, Dictionary<string, EntityUid>> _glows = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleExactCardinalDirectionSystem));
        UpdatesAfter.Add(typeof(VehicleTurretVisualSystem));
        UpdatesAfter.Add(typeof(VehicleWheelVisualizerSystem));
        UpdatesAfter.Add(typeof(RMCSpriteSystem));
        SubscribeLocalEvent<CMUVehicleOverlayVisualsComponent, ComponentShutdown>((uid, _, _) => RemoveGlows(uid));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var overlay in _glows.Keys.ToList())
        {
            RemoveGlows(overlay);
        }
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
            if (_jeep.IsHullWrecked(vehicle))
                bob -= HullSag;
            var lift = (-direction.ToAngle()).RotateVec(new Vector2(0f, bob / PixelsPerMeter));
            _lifts[vehicle] = (lift, direction);
            ApplyDirection(ent, visuals, direction, bob);
            BobBody(vehicle, lift);
            MirrorWheels(ent, vehicle);
            MirrorDamage(ent, vehicle);
            UpdateLights(ent, visuals, vehicle);
        }

        BobTurrets();
        UpdateMobDepths();
    }

    // How near a vehicle overlay mobs are sorted against it, and how far below a mob's centre its
    // feet are, in tiles.
    private const float MobDepthRange = 3.5f;
    private const float MobFeet = 0.4f;

    // Mobs near a vehicle, with the depth they had before it sorted them.
    private readonly Dictionary<EntityUid, int> _mobDepths = new();
    private readonly HashSet<EntityUid> _nearMobs = new();

    /// <summary>
    /// The vehicle is drawn in two depths around its crew (body under them, overlay over them), so
    /// anyone else nearby goes wholly in front or wholly behind it: below its near edge on screen they
    /// draw over the whole vehicle, otherwise under it. A mob's feet decide: standing against the
    /// vehicle's near side, its centre is still above the edge. They get their own depth back once clear.
    /// </summary>
    private void UpdateMobDepths()
    {
        _nearMobs.Clear();
        var eye = _eye.CurrentEye.Rotation;
        var overlays = EntityQueryEnumerator<CMUVehicleOverlayVisualsComponent, TransformComponent>();
        while (overlays.MoveNext(out _, out var visuals, out var xform))
        {
            var centre = _transform.GetMapCoordinates(xform);
            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(_transform.GetWorldRotation(xform) + eye);
            visuals.DirectionOffsets.TryGetValue(direction, out var slide);
            // The overlay's states are slid so their frame ends at the vehicle's near edge.
            var nearEdge = (FrameHalf - slide.Y) / PixelsPerMeter;
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(centre, MobDepthRange))
            {
                if (!TryComp(mob, out SpriteComponent? sprite))
                    continue;

                // Riders are the crew's depth, which RMC's buckle keeps.
                if (TryComp(mob, out BuckleComponent? buckle) && buckle.Buckled)
                {
                    _mobDepths.Remove(mob);
                    continue;
                }

                var below = -eye.RotateVec(_transform.GetWorldPosition(mob.Owner) - centre.Position).Y;
                var depth = below + MobFeet > nearEdge ? (int) DrawDepth.OverMobs : (int) DrawDepth.BelowMobs;
                _mobDepths.TryAdd(mob, sprite.DrawDepth);
                _nearMobs.Add(mob);
                if (sprite.DrawDepth != depth)
                    _sprite.SetDrawDepth((mob, sprite), depth);
            }
        }

        foreach (var (mob, depth) in _mobDepths)
        {
            if (_nearMobs.Contains(mob))
                continue;

            _mobDepths.Remove(mob);
            if (TryComp(mob, out SpriteComponent? sprite))
                _sprite.SetDrawDepth((mob, sprite), depth);
        }
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
        if (state == null ||
            ent.Comp[index] is not SpriteComponent.Layer { ActualRsi: { } rsi } ||
            !rsi.TryGetState(state, out _))
        {
            _sprite.LayerSetVisible(ent.AsNullable(), index, false);
            return;
        }

        if (_sprite.LayerGetRsiState(ent.AsNullable(), index) != state)
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

        // Each lamp's lit layers show while it is fitted and whole.
        var blink = (float) (_timing.CurTime.TotalSeconds % SignalPeriod);
        var lamps = CompOrNull<CMUVehicleLampsComponent>(vehicle)?.Lamps ?? NoLamps;
        foreach (var lamp in lamps)
        {
            var works = _jeep.LampWorks(vehicle, lamp.Id);
            var signal = lamp.Side == CMUVehicleLampSide.Left ? left : right;
            if (lamp.LitLayer is { } lit)
                SetVisible(ent, lit, lightsOn && works);
            if (lamp.BrakeLayer is { } brake)
                SetVisible(ent, brake, braking && works);
            if (lamp.SignalLayer is { } blinker)
            {
                SetVisible(ent, blinker, signal && works);
                if (_sprite.LayerMapTryGet(ent.AsNullable(), blinker, out var index, false))
                    _sprite.LayerSetAnimationTime(ent.AsNullable(), index, blink);
            }
        }

        // The signal states show their lit frame for the first half of each blink; the relay ticks as
        // they go on and off.
        var blinkOn = blink < SignalPeriod / 2;
        if ((left || right) && blinkOn != visuals.BlinkOn && visuals.SignalSound != null)
            _audio.PlayEntity(visuals.SignalSound, Filter.Local(), vehicle, false);

        visuals.BlinkOn = blinkOn;
        UpdateGlows(ent.Owner, vehicle, lamps, lightsOn, braking, left && blinkOn, right && blinkOn);
    }

    private static readonly List<CMUVehicleLampData> NoLamps = new();

    /// <summary>
    /// Lights the ground around the lamps that are lit: a dim red round each working tail light (with
    /// the lights on, brighter while braking), amber round a blinking turn signal, front and rear.
    /// </summary>
    private void UpdateGlows(EntityUid overlay, EntityUid vehicle, List<CMUVehicleLampData> lamps, bool lightsOn, bool braking, bool leftBlink, bool rightBlink)
    {
        if (!_glows.TryGetValue(overlay, out var glows))
            _glows[overlay] = glows = new Dictionary<string, EntityUid>();

        foreach (var lamp in lamps)
        {
            var tail = lamp.Role == CMUVehicleLampRole.TailLight;
            if (!tail && lamp.Role != CMUVehicleLampRole.TurnSignal)
                continue;

            var blinking = lamp.SignalLayer != null && (lamp.Side == CMUVehicleLampSide.Left ? leftBlink : rightBlink);
            var works = _jeep.LampWorks(vehicle, lamp.Id);
            Color? colour = null;
            var energy = 0f;
            if (works && blinking)
                (colour, energy) = (SignalGlow, SignalEnergy);
            else if (works && tail && braking)
                (colour, energy) = (TailGlow, BrakeEnergy);
            else if (works && tail && lightsOn)
                (colour, energy) = (TailGlow, TailEnergy);

            if (!glows.TryGetValue(lamp.Id, out var glow) || TerminatingOrDeleted(glow))
            {
                if (colour == null)
                    continue;

                // The vehicle faces -y in its own space: a lamp's glow reaches out past its own end.
                var reach = new Vector2(0f, MathF.Sign(lamp.Offset.Y) * GlowReach);
                glow = Spawn(GlowPrototype, new EntityCoordinates(vehicle, lamp.Offset + reach));
                glows[lamp.Id] = glow;
            }

            _lights.SetEnabled(glow, colour != null);
            if (colour is { } c)
            {
                _lights.SetColor(glow, c);
                _lights.SetEnergy(glow, energy);
            }
        }
    }

    private void RemoveGlows(EntityUid overlay)
    {
        if (!_glows.Remove(overlay, out var glows))
            return;

        foreach (var glow in glows.Values)
        {
            if (!TerminatingOrDeleted(glow))
                QueueDel(glow);
        }
    }

    private void SetVisible(Entity<SpriteComponent> ent, string key, bool visible)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            _sprite.LayerSetVisible(ent.AsNullable(), index, visible);
    }
}
