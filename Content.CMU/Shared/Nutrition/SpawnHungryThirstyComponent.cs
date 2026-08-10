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
/// The configured keys are resolved against the entity's satiation prototypes at spawn time, so this
/// always follows the current hunger and thirst pacing without hard-coded numeric values.
/// </remarks>
[RegisterComponent]
public sealed partial class SpawnHungryThirstyComponent : Component
{
    [DataField]
    public SatiationValue StartingThirstThreshold = "Thirsty";

    [DataField]
    public SatiationValue StartingHungerThreshold = "Peckish";
}
