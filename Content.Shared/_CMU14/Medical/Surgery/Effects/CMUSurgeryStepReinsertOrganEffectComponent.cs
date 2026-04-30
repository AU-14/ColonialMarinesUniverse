using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

/// <summary>
///     On step success the system pulls the organ from the surgeon's active
///     hand, inserts it into <see cref="OrganSlot"/> on the targeted part,
///     clears <see cref="OrganStasisComponent"/> on the organ, and applies
///     <c>StatusEffectCMUTransplantRejection</c> to the body for the
///     CCVar-tunable rejection window.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepReinsertOrganEffectComponent : Component
{
    [DataField(required: true)]
    public string OrganSlot = string.Empty;
}
