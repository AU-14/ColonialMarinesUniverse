using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ConstructionRobot;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUConstructionRobotComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Owner;

    [DataField, AutoNetworkedField]
    public float WeldingFuel = 1000f;

    [DataField]
    public float FuelCapacity = 1000f;

    [DataField]
    public int MaxMaterialStacks = 20;

    [DataField]
    public float MaterialPickupRadius = 5f;

    [DataField]
    public string MaterialsContainerId = "cmu-construction-robot-materials";

    [DataField]
    public TimeSpan RefuelDelay = TimeSpan.FromSeconds(15);

    public Container? Materials;

    public bool IsBuilding;

    public int? ActiveGhostId;

    public EntityCoordinates? CurrentFollowTarget;
}

[RegisterComponent]
public sealed partial class CMUConstructionRobotKitComponent : Component
{
    [DataField]
    public EntProtoId RobotPrototype = "CMUConstructionRobot";
}

[Serializable, NetSerializable]
public sealed partial class CMUConstructionRobotRefuelDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUConstructionRobotBuildDoAfterEvent : SimpleDoAfterEvent;
