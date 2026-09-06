using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Inventory;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUItemSlotSkillSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class CMUItemSlotSkillRequiredComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();

    [DataField(required: true), AutoNetworkedField]
    public LocId FailPopup;
}
