using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Flare;

[Serializable, NetSerializable]
public sealed partial class FlareStompDoAfterEvent : SimpleDoAfterEvent;
