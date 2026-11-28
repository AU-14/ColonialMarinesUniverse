using System;
using System.Collections.Generic;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementModeModifiersComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Disabled;

    [DataField, AutoNetworkedField]
    public float SlowestStandard = 0.5f;

    [DataField, AutoNetworkedField]
    public float SlowStandard = 0.75f;

    [DataField, AutoNetworkedField]
    public float Standard = 1f;

    [DataField, AutoNetworkedField]
    public float FastStandard = 1.05f;

    [DataField, AutoNetworkedField]
    public float FastestStandard = 1.1f;

    [DataField, AutoNetworkedField]
    public float Overdrive = 1.25f;

    [DataField, AutoNetworkedField]
    public float Overdrive2 = 1.3f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementSpeedGearComponent : Component
{
    [DataField, AutoNetworkedField]
    public float GearSpeedMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public VehicleMovementStateList MoveMode = VehicleMovementStateList.Standard;

    [DataField, AutoNetworkedField]
    public bool Enabled;
}

[Serializable, NetSerializable]
public enum VehicleMovementStateList : byte
{
    SlowestStandard,
    SlowStandard,
    Standard,
    FastStandard,
    FastestStandard,
    Overdrive,
    Overdrive2,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementActionHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId1 = "ActionVehicleGearUp";
    [DataField, AutoNetworkedField]
    public EntityUid? Action1;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId2 = "ActionVehicleGearDown";

    [DataField, AutoNetworkedField]
    public EntityUid? Action2;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId3 = "ActionVehicleToggleEngine";

    [DataField, AutoNetworkedField]
    public EntityUid? Action3;
}

public sealed partial class VehicleStartUpShutDownEvent : InstantActionEvent
{
    public NetEntity Vehicle;

    public VehicleStartUpShutDownEvent()
    {
    }

    public VehicleStartUpShutDownEvent(NetEntity vehicle)
    {
        Vehicle = vehicle;
    }
}

public sealed partial class VehicleGearShiftUpEvent : InstantActionEvent
{
    public NetEntity Vehicle;

    public VehicleGearShiftUpEvent()
    {
    }

    public VehicleGearShiftUpEvent(NetEntity vehicle)
    {
        Vehicle = vehicle;
    }
}

public sealed partial class VehicleGearShiftDownEvent : InstantActionEvent
{
    public NetEntity Vehicle;

    public VehicleGearShiftDownEvent()
    {
    }

    public VehicleGearShiftDownEvent(NetEntity vehicle)
    {
        Vehicle = vehicle;
    }
}
