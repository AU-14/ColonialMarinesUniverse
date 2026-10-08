using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weather;

/// <summary>
/// Gameplay hazards applied by a weather status effect to mobs standing on tiles the weather reaches.
/// Rides the weather entity next to <c>WeatherStatusEffect</c>. All values are per <see cref="Interval"/> tick
/// and scale with the weather fade in and out.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class WeatherHazardComponent : Component
{
    /// <summary>
    /// Damage applied each tick. Armor resistances apply as normal.
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Time between hazard applications.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Radiation dose per second. Rad protection on the target applies.
    /// </summary>
    [DataField]
    public float RadsPerSecond;

    /// <summary>
    /// Chance per tick that an exposed flammable target catches fire.
    /// </summary>
    [DataField]
    public float IgniteChance;

    /// <summary>
    /// Fire stacks added when <see cref="IgniteChance"/> succeeds.
    /// </summary>
    [DataField]
    public float FireStacks = 1;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTickAt;
}
