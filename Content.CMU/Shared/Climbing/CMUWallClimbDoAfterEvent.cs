using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Climbing;

[Serializable, NetSerializable]
public sealed partial class CMUWallClimbDoAfterEvent : SimpleDoAfterEvent;
