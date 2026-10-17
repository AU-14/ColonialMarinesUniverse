using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DroneOperator;

[Serializable, NetSerializable]
public sealed partial class CMUCombatDroneInstallTurretDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUCombatDroneAssembleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUCombatDroneWeldDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUCombatDroneWireDoAfterEvent : SimpleDoAfterEvent;
