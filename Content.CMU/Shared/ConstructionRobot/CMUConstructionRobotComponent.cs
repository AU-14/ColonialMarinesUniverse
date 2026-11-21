using Content.Shared.DoAfter;
using Content.Shared.Actions;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ConstructionRobot;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUConstructionRobotComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Operator;

    [DataField, AutoNetworkedField]
    public float WeldingFuel = 1000f;

    [DataField]
    public float FuelCapacity = 1000f;

    [DataField]
    public float MaterialPickupRadius = 5f;

    [DataField]
    public int MaxNameLength = 24;

    [DataField]
    public TimeSpan RefuelDelay = TimeSpan.FromSeconds(15);

    public Container? Materials;

    public bool IsBuilding;
    public DoAfterId? BuildDoAfter;

    public EntityCoordinates? CurrentFollowTarget;
    public float FollowRange;
    public MapCoordinates? LastProgressPosition;
    public TimeSpan LastProgressTime;

    public EntityCoordinates? IdlePosition;
    public bool AvoidHazards = true;
    public bool AvoidFire = true;
    public bool AvoidHostiles = true;
    public bool AvoidExplosives = true;
    public bool AvoidingHazard;
}

[RegisterComponent]
public sealed partial class CMUConstructionRobotPackageComponent : Component
{
    [DataField]
    public EntProtoId DeployerPrototype = "CMUConstructionRobotDeployer";

    [DataField]
    public EntProtoId VisorPrototype = "CMUSmartWeldingVisor";
}

[RegisterComponent]
public sealed partial class CMUConstructionRobotKitComponent : Component
{
    [DataField]
    public EntProtoId RobotPrototype = "CMUConstructionRobot";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSmartWeldingVisorComponent : Component
{
    public EntityUid? SelectedRobot;
}

public sealed partial class CMUConstructionRobotStatusActionEvent : InstantActionEvent;
public sealed partial class CMUConstructionRobotHoldActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class CMUConstructionRobotRefuelDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUConstructionRobotBuildDoAfterEvent : SimpleDoAfterEvent;
