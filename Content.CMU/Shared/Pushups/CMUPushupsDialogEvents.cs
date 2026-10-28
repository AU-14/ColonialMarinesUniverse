using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Pushups;

/// <summary>
/// Picked in the pushups popup: start pushups, on the knees or not. Also how a set carries on after the arms give out.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUPushupsStyleChosenEvent(bool Knees, bool CarryOn = false);

/// <summary>
/// Picked, closed or timed out in the popup after the arms give out: get back up.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUPushupsFinishEvent;
