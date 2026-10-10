using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VehicleMaintenanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public VehicleMaintenanceMode Mode;

    [DataField]
    public TimeSpan PanelDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float ExposedDamageMultiplier = 1.5f;

    [DataField]
    public EntProtoId Action = "ActionVehicleMaintenance";

    public EntityUid? DriverAction;
    public DoAfterId? PanelDoAfter;

    public bool ControlsLocked => Mode != VehicleMaintenanceMode.Operational;
}

[Serializable, NetSerializable]
public enum VehicleMaintenanceMode : byte
{
    Operational,
    Opening,
    Maintenance,
    Closing,
}

public sealed partial class VehicleMaintenanceActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class VehicleMaintenanceDoAfterEvent : SimpleDoAfterEvent;
