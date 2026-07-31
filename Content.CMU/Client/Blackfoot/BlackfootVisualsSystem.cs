using System;
using System.Numerics;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._RMC14.Vehicle;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Blackfoot;

public sealed partial class BlackfootVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var aircraft = EntityQueryEnumerator<BlackfootVisualsComponent, BlackfootFlightComponent, SpriteComponent>();
        while (aircraft.MoveNext(out var uid, out var visuals, out var flight, out var sprite))
        {
            ApplyAircraftVisuals(uid, visuals, flight, sprite);
        }

        var shadows = EntityQueryEnumerator<BlackfootShadowComponent, SpriteComponent>();
        while (shadows.MoveNext(out var uid, out var shadow, out var sprite))
        {
            ApplyShadowVisuals(uid, shadow, sprite);
        }

        var padLights = EntityQueryEnumerator<BlackfootLandingPadLightComponent, SpriteComponent>();
        while (padLights.MoveNext(out var uid, out var light, out var sprite))
        {
            ApplyPadLightVisuals(uid, light, sprite);
        }

        var rearDoors = EntityQueryEnumerator<BlackfootRearDoorVisualsComponent, SpriteComponent>();
        while (rearDoors.MoveNext(out var uid, out var door, out var sprite))
        {
            ApplyRearDoorVisuals(uid, door, sprite);
        }
    }

    private void ApplyAircraftVisuals(
        EntityUid uid,
        BlackfootVisualsComponent visuals,
        BlackfootFlightComponent flight,
        SpriteComponent sprite)
    {
        var mode = GetVisualMode(flight);
        var airborneOrTransitioning = IsAirborneOrTransitioning(flight.State);
        var runningFans = flight.State is
            BlackfootFlightState.Idling or
            BlackfootFlightState.TakingOff or
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;

        SetLayerState(uid, sprite, visuals.BaseLayer, mode, true);
        SetLayerState(uid, sprite, visuals.LightsLayer, $"{mode}_lights", flight.State != BlackfootFlightState.Crashed);
        SetLayerState(uid, sprite, visuals.ThrustLayer, flight.State == BlackfootFlightState.Flight ? "flight_thrust" : "vtol_thrust", airborneOrTransitioning);
        SetLayerState(uid, sprite, visuals.FansLayer, "fan-overlay", runningFans);
        SetLayerState(uid, sprite, visuals.DownwashLayer, "downwash", false);
        SetLayerState(uid, sprite, visuals.DamageLayer, "damage", IsDamageVisible(uid, visuals, flight));

        SetHardpointState(uid, sprite, visuals.ThrustersLayer, $"engines_{mode}");
        SetHardpointState(uid, sprite, visuals.LaunchersLayer, $"launchers_{mode}");
        SetHardpointState(uid, sprite, visuals.DoorGunLayer, $"doorgun_{mode}");
        SetHardpointState(uid, sprite, visuals.SupportLayer, ResolveSupportState(uid, sprite, visuals.SupportLayer, mode));
        SetHardpointState(uid, sprite, visuals.SensorsLayer, $"radar_{mode}");

        _sprite.SetOffset((uid, sprite), GetAircraftVisualOffset(flight, visuals));
    }

    private void ApplyShadowVisuals(EntityUid uid, BlackfootShadowComponent shadow, SpriteComponent sprite)
    {
        var state = "vtol_shadow";
        if (shadow.Aircraft is { } aircraft &&
            TryComp(aircraft, out BlackfootFlightComponent? flight))
        {
            state = GetVisualMode(flight) switch
            {
                "flight" => "flight_shadow",
                "stowed" => "stowed_shadow",
                _ => "vtol_shadow",
            };
        }

        SetLayerState(uid, sprite, 0, state, true);
    }

    private void ApplyPadLightVisuals(EntityUid uid, BlackfootLandingPadLightComponent light, SpriteComponent sprite)
    {
        var on = light.State != BlackfootLandingPadLightState.Off;
        SetLayerState(uid, sprite, 0, on ? "landing-pad-light-on" : "landing-pad-light", true);

        if (!TryComp(uid, out PointLightComponent? pointLight))
            return;

        _pointLight.SetEnabled(uid, on, pointLight);
        _pointLight.SetEnergy(uid, light.State == BlackfootLandingPadLightState.Servicing ? 1f : 0.65f, pointLight);
    }

    private void ApplyRearDoorVisuals(EntityUid uid, BlackfootRearDoorVisualsComponent door, SpriteComponent sprite)
    {
        SetLayerState(uid, sprite, door.DoorLayer, door.Open ? door.OpenState : door.ClosedState, true);

        if (!string.IsNullOrWhiteSpace(door.OverlayLayer))
            SetLayerState(uid, sprite, door.OverlayLayer, string.Empty, door.ShowOverlay);
    }

    private string ResolveSupportState(EntityUid uid, SpriteComponent sprite, string layerMap, string mode)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerMap, out var layer, false))
            return $"recon_{mode}";

        var current = _sprite.LayerGetRsiState((uid, sprite), layer).Name ?? string.Empty;
        if (current.StartsWith("para_", StringComparison.Ordinal))
            return $"para_{mode}";

        if (current.StartsWith("radar_", StringComparison.Ordinal))
            return $"radar_{mode}";

        return $"recon_{mode}";
    }

    private bool IsDamageVisible(EntityUid uid, BlackfootVisualsComponent visuals, BlackfootFlightComponent flight)
    {
        if (flight.State == BlackfootFlightState.Crashed)
            return true;

        if (!TryComp(uid, out HardpointIntegrityComponent? integrity) ||
            integrity.MaxIntegrity <= 0f)
        {
            return false;
        }

        var fraction = Math.Clamp(integrity.Integrity / integrity.MaxIntegrity, 0f, 1f);
        return fraction <= visuals.DamageVisibleIntegrityFraction;
    }

    private void SetLayerState(EntityUid uid, SpriteComponent sprite, string layerMap, string state, bool visible)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerMap, out var layer, false))
            return;

        SetLayerState(uid, sprite, layer, state, visible);
    }

    private void SetLayerState(EntityUid uid, SpriteComponent sprite, int layer, string state, bool visible)
    {
        if (!string.IsNullOrWhiteSpace(state))
            _sprite.LayerSetRsiState((uid, sprite), layer, state);

        _sprite.LayerSetVisible((uid, sprite), layer, visible);
    }

    private void SetHardpointState(EntityUid uid, SpriteComponent sprite, string layerMap, string state)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerMap, out var layer, false))
            return;

        _sprite.LayerSetRsiState((uid, sprite), layer, state);
    }

    private static string GetVisualMode(BlackfootFlightComponent flight)
    {
        return flight.State switch
        {
            BlackfootFlightState.Stowed => "stowed",
            BlackfootFlightState.Flight => "flight",
            _ => "vtol",
        };
    }

    private static bool IsAirborneOrTransitioning(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.TakingOff or
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;
    }

    private Vector2 GetAircraftVisualOffset(BlackfootFlightComponent flight, BlackfootVisualsComponent visuals)
    {
        if (flight.State != BlackfootFlightState.TakingOff)
            return Vector2.Zero;

        var duration = flight.TransitionEndTime - flight.StateStartedAt;
        if (duration <= TimeSpan.Zero)
            duration = flight.TakeoffDuration;

        if (duration <= TimeSpan.Zero)
            return Vector2.Zero;

        var progress = Math.Clamp((float) ((_timing.CurTime - flight.StateStartedAt).TotalSeconds / duration.TotalSeconds), 0f, 1f);
        var liftStart = Math.Clamp(visuals.TakeoffLiftStartFraction, 0f, 0.95f);
        if (progress <= liftStart)
            return Vector2.Zero;

        var liftProgress = SmoothStep((progress - liftStart) / (1f - liftStart));
        return new Vector2(0f, visuals.TakeoffLiftOffset * liftProgress);
    }

    private static float SmoothStep(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }
}
