using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUEscharSurgeryConditionComponent : Component
{
}
