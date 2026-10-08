using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Elevators;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUElevatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ElevatorId = string.Empty;

    [DataField, AutoNetworkedField]
    public int TravelDirection = 1;

    [DataField, AutoNetworkedField]
    public bool Disabled;

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> DisableSkill = "RMCSkillEngineer";

    [DataField, AutoNetworkedField]
    public int DisableSkillLevel = 3;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUElevatorRailComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ElevatorId = string.Empty;
}
