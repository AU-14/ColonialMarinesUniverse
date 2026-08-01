using Content.Shared.Nutrition.Components;

namespace Content.Shared._AU14.Nutrition;

/// <summary>
/// Marker component. Attach to a job (directly via <c>roundComponents</c>/<c>roundSideComponents</c>/
/// <c>roundForceComponents</c>, or on a shared abstract job so every descendant inherits it - see
/// <c>AU14JobMilitaryBase</c>) to make the mob spawn already at the given Thirst/Hunger threshold
/// instead of the usual well-rested "Okay" starting point - e.g. troops reporting for duty already
/// needing breakfast after waking up.
/// </summary>
/// <remarks>
/// The actual thirst/hunger values used are read from the entity's own <see cref="ThirstComponent"/>/
/// <see cref="HungerComponent"/> thresholds at spawn time, so this always matches whatever pacing
/// those components are currently tuned to - no numbers to keep in sync by hand.
/// </remarks>
[RegisterComponent]
public sealed partial class SpawnHungryThirstyComponent : Component
{
    [DataField]
    public ThirstThreshold StartingThirstThreshold = ThirstThreshold.Thirsty;

    [DataField]
    public HungerThreshold StartingHungerThreshold = HungerThreshold.Peckish;
}
