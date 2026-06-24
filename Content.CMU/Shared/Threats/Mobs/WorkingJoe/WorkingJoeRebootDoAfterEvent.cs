using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.WorkingJoe;

[Serializable, NetSerializable]
public sealed partial class WorkingJoeRebootDoAfterEvent : SimpleDoAfterEvent;
