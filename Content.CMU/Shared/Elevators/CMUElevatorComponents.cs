using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Maps;
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

    /// <summary>
    /// How long the doors stay shut and the car is in transit before it arrives. Zero moves instantly.
    /// </summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Tile left behind on the elevator's home floor when the car leaves it.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition> HomeFloorTile = "Plating";

    /// <summary>
    /// The floor the elevator was first used from. Recorded on the first trip.
    /// </summary>
    [ViewVariables]
    public EntityUid? HomeMap;

    [ViewVariables]
    public EntityUid? PendingTarget;

    [ViewVariables]
    public EntityUid? PendingUser;

    [ViewVariables]
    public TimeSpan ArriveAt;
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

    /// <summary>
    /// A landing door on a floor rather than part of the car. It is not part of the outline and stays
    /// closed and bolted unless the elevator is waiting on its floor.
    /// </summary>
    [DataField]
    public bool Landing;
}

[RegisterComponent]
public sealed partial class CMUElevatorWeightLimitComponent : Component
{
    [DataField]
    public int MaxEntities = 250;
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
