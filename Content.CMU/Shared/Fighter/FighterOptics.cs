using System.Numerics;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Aircraft-owned optics state. Neither changing crew nor changing passes resets thermal recovery.</summary>
public static class FighterOptics
{
    public static readonly TimeSpan ThermalDuration = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ThermalCooldown = TimeSpan.FromMinutes(6);

    public static FighterSensorMode Mode(FighterAircraftComponent aircraft, TimeSpan now) =>
        aircraft.SensorMode == FighterSensorMode.Thermal && now >= aircraft.ThermalUntil
            ? FighterSensorMode.Normal
            : aircraft.SensorMode;

    public static bool ThermalReady(FighterAircraftComponent aircraft, TimeSpan now) => now >= aircraft.ThermalReadyAt;

    public static void Update(FighterAircraftComponent aircraft, TimeSpan now)
    {
        aircraft.SensorMode = Mode(aircraft, now);
    }

    public static bool TrySetMode(FighterAircraftComponent aircraft, FighterSensorMode mode, TimeSpan now)
    {
        if (mode is not (FighterSensorMode.Normal or FighterSensorMode.NightVision or FighterSensorMode.Thermal))
            return false;

        Update(aircraft, now);
        if (aircraft.SensorMode == mode)
            return true;

        if (mode == FighterSensorMode.Thermal)
        {
            if (!ThermalReady(aircraft, now))
                return false;

            aircraft.ThermalUntil = now + ThermalDuration;
            aircraft.ThermalReadyAt = aircraft.ThermalUntil + ThermalCooldown;
        }
        else if (aircraft.SensorMode == FighterSensorMode.Thermal)
        {
            // Switching off early still requires the full cooling cycle.
            aircraft.ThermalUntil = now;
            aircraft.ThermalReadyAt = now + ThermalCooldown;
        }

        aircraft.SensorMode = mode;
        return true;
    }

    public static bool CloudsBlock(FighterAircraftComponent aircraft, Vector2 point, TimeSpan now) =>
        Mode(aircraft, now) != FighterSensorMode.Thermal && FighterFlight.CloudBlocks(point, aircraft.Altitude);
}
