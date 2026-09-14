using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
/// Slows hunger and thirst loss for players in Distress Signal.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DistressSignalNutritionSystem))]
public sealed partial class DistressSignalNutritionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float RateMultiplier = 0.5f;
}
