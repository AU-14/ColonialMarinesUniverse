using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe;

[Serializable, NetSerializable]
public sealed partial class WorkingJoeRebootDoAfterEvent : SimpleDoAfterEvent;
