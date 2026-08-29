using Content.Shared.Nutrition.Prototypes;

namespace Content.Shared._AU14.Nutrition;

/// <summary>
/// Marker component. Attach to a job (directly via <c>roundComponents</c>/<c>roundSideComponents</c>/
/// <c>roundForceComponents</c>, or on a shared abstract job so every descendant inherits it - see
/// <c>AU14JobMilitaryBase</c>) to make the mob spawn already at the given Thirst/Hunger threshold
/// instead of the usual well-rested "Okay" starting point - e.g. troops reporting for duty already
/// needing breakfast after waking up.
/// </summary>
/// <remarks>
/// The actual thirst/hunger values are resolved from the entity's satiation prototypes at spawn time,
/// so this always matches whatever pacing those prototypes are currently tuned to.
/// </remarks>
[RegisterComponent]
public sealed partial class SpawnHungryThirstyComponent : Component
{
    [DataField]
    public SatiationValue StartingThirstThreshold = "Thirsty";

    [DataField]
    public SatiationValue StartingHungerThreshold = "Peckish";
}
