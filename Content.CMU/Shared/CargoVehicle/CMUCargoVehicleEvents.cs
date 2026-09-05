using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CargoVehicle;

public sealed partial class CMUCargoVehicleReturnActionEvent : InstantActionEvent;

public sealed partial class CMUCargoVehicleToggleBayActionEvent : InstantActionEvent;

public sealed partial class CMUCargoVehicleSelfDestructActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class CMUCargoVehicleLoadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUCargoVehicleUnloadDoAfterEvent : SimpleDoAfterEvent;
