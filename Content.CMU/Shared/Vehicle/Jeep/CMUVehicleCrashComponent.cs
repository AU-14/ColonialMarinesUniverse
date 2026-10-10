using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Scales the damage a vehicle takes from crashing into things with how fast it hit: the slowest
/// impact that hurts at all costs <see cref="MinDamageFraction"/> of the full damage, a crash at top
/// speed all of it. Each crash is raised on the vehicle as a <see cref="CMUVehicleCrashedEvent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleCrashComponent : Component
{
    [DataField]
    public float MinDamageFraction = 1f / 3f;

    /// <summary>
    /// Crashes, into walls or people, wear only the wheels, never the guns or other hardpoints.
    /// </summary>
    [DataField]
    public bool WheelsOnly = true;

    /// <summary>
    /// The share of full crash damage taken at an impact speed: <paramref name="minFraction"/> at
    /// <paramref name="minSpeed"/>, rising evenly to all of it at <paramref name="maxSpeed"/>.
    /// </summary>
    public static float Scale(float speed, float minSpeed, float maxSpeed, float minFraction)
    {
        if (maxSpeed <= minSpeed)
            return 1f;

        var t = Math.Clamp((speed - minSpeed) / (maxSpeed - minSpeed), 0f, 1f);
        return minFraction + (1f - minFraction) * t;
    }
}

/// <summary>
/// A vehicle crashed into something and its hull took <see cref="HullDamage"/>.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVehicleCrashedEvent(float HullDamage);
