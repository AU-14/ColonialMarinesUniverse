using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.K9.Events;

[Serializable, NetSerializable]
public sealed partial class K9EscapeGrabDoAfterEvent : SimpleDoAfterEvent;
