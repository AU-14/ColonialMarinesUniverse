using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.LawEnforcement;

/// <summary>A dash camera being fitted to a vehicle.</summary>
[Serializable, NetSerializable]
public sealed partial class CMUDashCameraMountDoAfterEvent : SimpleDoAfterEvent;

/// <summary>A dash camera being unscrewed from a vehicle.</summary>
[Serializable, NetSerializable]
public sealed partial class CMUDashCameraRemoveDoAfterEvent : SimpleDoAfterEvent;

/// <summary>A dash camera's electronics being shorted out with a multitool.</summary>
[Serializable, NetSerializable]
public sealed partial class CMUDashCameraShortDoAfterEvent : SimpleDoAfterEvent;
