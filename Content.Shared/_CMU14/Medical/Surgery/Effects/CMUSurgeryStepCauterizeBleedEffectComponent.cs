using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

/// <summary>
///     Removes the part's <see cref="InternalBleedingComponent"/> on step
///     success. Recompute may re-add it next tick if the cause persists.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepCauterizeBleedEffectComponent : Component
{
}
