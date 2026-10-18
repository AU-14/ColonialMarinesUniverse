using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.WallLean;

/// <summary>
///     Climbing up on someone leaning against a wall to get onto the level above.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUWallBoostDoAfterEvent : SimpleDoAfterEvent;
