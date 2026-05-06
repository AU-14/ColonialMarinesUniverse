using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

/// <summary>
///     On step success the system detaches the targeted arm or leg from the
///     patient body and leaves the detached limb as a normal pickup entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepRemoveLimbEffectComponent : Component
{
}
