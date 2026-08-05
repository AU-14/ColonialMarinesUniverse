using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMSurgeryPartConditionComponent : Component
{
    [DataField]
    public ProtoId<OrganCategoryPrototype> Part;
}
