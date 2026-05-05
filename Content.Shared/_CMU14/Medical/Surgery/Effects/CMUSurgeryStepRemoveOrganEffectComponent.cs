using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

/// <summary>
///     On step success the system extracts the named organ from the part,
///     applies <see cref="OrganStasisComponent"/> to the detached organ
///     entity, and applies the matching <c>StatusEffectCMU&lt;Failure&gt;</c>
///     to the body for the missing organ. The organ entity itself is not
///     deleted — it falls into the surgeon's hand for transplant or disposal.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepRemoveOrganEffectComponent : Component
{
    [DataField(required: true)]
    public string OrganSlot = string.Empty;
}
