using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery.Effects;

/// <summary>Closes every open stump on the part on step success, stopping their bleeding.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepCloseStumpEffectComponent : Component
{
}
