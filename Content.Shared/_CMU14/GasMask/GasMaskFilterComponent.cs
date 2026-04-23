

using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.GasMask;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GasMaskFilterComponent : Component
{
    /// <summary>
    /// Can the filter resist neurotoxin Gas?
    /// </summary>
    [DataField]
    public bool NeurotoxinResist = false;

    /// <summary>
    /// Multiplier for how much damage neurotoxin does to the filter compared to regular gas
    /// </summary>
    [DataField]
    public float NeurotoxinDamageMultiplier = 1f;

    /// <summary>
    /// The "Starting HP" of the filter
    /// </summary>
    [DataField]
    public float BaseIntegrity = 100f;

    /// <summary>
    /// The filter's "Health"
    /// Set to the BaseIntegrity when initialized
    /// </summary>
    [AutoNetworkedField]
    public float Integrity = 0f;
}
