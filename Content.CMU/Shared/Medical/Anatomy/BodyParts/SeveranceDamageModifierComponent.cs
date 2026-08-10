using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts;

/// <summary>
///     Scales the structural severance contribution of damage caused by this
///     entity without changing its ordinary damage or armor penetration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeveranceDamageModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
