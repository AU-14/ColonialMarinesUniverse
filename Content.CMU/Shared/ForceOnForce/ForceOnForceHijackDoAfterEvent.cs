using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ForceOnForce;

[Serializable, NetSerializable]
public sealed partial class ForceOnForceHijackDoAfterEvent : SimpleDoAfterEvent;
