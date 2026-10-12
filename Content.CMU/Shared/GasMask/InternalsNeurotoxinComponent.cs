using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.GasMask;

/// <summary>
/// How much more neurotoxin a gas tank can keep out when breathed from through internals.
/// Added the first time the tank meets neurotoxin; swapping to a fresh tank restores full protection.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGasMaskSystem))]
public sealed partial class InternalsNeurotoxinComponent : Component
{
    /// <summary>Remaining neurotoxin gas damage, in the same units gas deals to filters before their multiplier.</summary>
    [DataField, AutoNetworkedField]
    public float Remaining;
}
