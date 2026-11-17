using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Radio;

[Serializable, NetSerializable]
public sealed partial class CMURadioRipWiresDoAfterEvent : SimpleDoAfterEvent;
