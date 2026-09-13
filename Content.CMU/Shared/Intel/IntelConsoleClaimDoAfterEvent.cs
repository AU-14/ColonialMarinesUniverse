using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Intel;

[Serializable, NetSerializable]
public sealed partial class IntelConsoleClaimDoAfterEvent : SimpleDoAfterEvent;
