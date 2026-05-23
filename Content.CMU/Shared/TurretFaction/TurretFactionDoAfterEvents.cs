using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.TurretFaction;

[Serializable, NetSerializable]
public sealed partial class TurretAssignFactionDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TurretClearFactionDoAfterEvent : SimpleDoAfterEvent;
