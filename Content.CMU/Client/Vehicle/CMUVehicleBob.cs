using Content.Shared.Vehicle.Components;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// The body's bounce on its suspension while driving: a few uneven waves summed, rounded to whole
/// pixels, so it never settles into a regular rhythm. Riders and cargo share it with the body.
/// </summary>
public static class CMUVehicleBob
{
    private const float MinSpeed = 0.3f;
    private const float FullSpeed = 6f;

    public static float Offset(EntityUid vehicle, GridVehicleMoverComponent? mover, TimeSpan time)
    {
        if (mover == null)
            return 0f;

        var speed = MathF.Abs(mover.CurrentSpeed);
        if (speed < MinSpeed)
            return 0f;

        var t = (float) time.TotalSeconds + vehicle.Id % 97 * 0.37f;
        var wave = 0.55f * MathF.Sin(t * 10.7f) +
                   0.3f * MathF.Sin(t * 17.3f + 1.3f) +
                   0.25f * MathF.Sin(t * 26.9f + 0.7f);
        var strength = Math.Clamp(speed / FullSpeed, 0.4f, 1f);
        return MathF.Round(wave * strength * 1.4f);
    }
}
