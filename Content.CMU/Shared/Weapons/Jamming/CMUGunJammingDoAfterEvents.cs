using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.Jamming;

[Serializable, NetSerializable]
public sealed partial class CMUGunCycleJamDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUGunSlamStockDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUGunMaintenanceDoAfterEvent : SimpleDoAfterEvent;
