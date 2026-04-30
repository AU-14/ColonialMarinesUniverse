using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Diagnostics;

[Serializable, NetSerializable]
public sealed partial class CMUStethoscopeDoAfterEvent : SimpleDoAfterEvent
{
}
