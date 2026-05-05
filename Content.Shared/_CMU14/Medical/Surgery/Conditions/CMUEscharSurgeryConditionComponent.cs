using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Wounds;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUEscharSurgeryConditionComponent : Component
{
}
