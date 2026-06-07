using Content.Shared._CMU14.Medical.Surgery.Traits;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgicalTraitConditionComponent : Component
{
    [DataField(required: true)]
    public CMUSurgicalTrait Trait;
}
