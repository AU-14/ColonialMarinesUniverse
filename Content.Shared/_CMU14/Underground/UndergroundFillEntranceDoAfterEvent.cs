using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// DoAfter event for filling in an underground entrance with a shovel.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class UndergroundFillEntranceDoAfterEvent : SimpleDoAfterEvent;
