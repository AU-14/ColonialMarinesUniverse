using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery.Conditions;

/// <summary>The surgery is only offered on a part with an open stump.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUOpenStumpSurgeryConditionComponent : Component
{
}
