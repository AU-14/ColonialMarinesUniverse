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

    /// <summary>
    /// Whether this piece fills its whole tile, like a wall or door, rather than fencing the side it faces.
    /// </summary>
    [DataField]
    public bool FullTile;
}

[RegisterComponent]
public sealed partial class CMUElevatorWeightLimitComponent : Component
{
    [DataField]
    public int MaxEntities = 20;
}

/// <summary>
/// Calls the elevator with the same ID to the floor this button is on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUElevatorCallButtonComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ElevatorId = string.Empty;
}
