using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// DoAfter event for using an underground entrance to teleport between maps.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class UndergroundEntranceDoAfterEvent : SimpleDoAfterEvent;
