using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.StatusEffects;

/// <summary>
///     Sits on a <c>StatusEffectCMUPainSuppression</c> entity. The pain
///     accumulator multiplies its rate by <c>1 - Percent</c>. Multiple
///     painkillers stack by taking the strongest concurrent
///     <see cref="Percent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PainSuppressionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Percent = 0.5f;
}
