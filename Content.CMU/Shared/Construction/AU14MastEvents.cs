using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Construction;

/// <summary>Loading the faction on a COMSEC fill card into an open mast feed.</summary>
[Serializable, NetSerializable]
public sealed partial class AU14MastKeyDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Wiping every key out of an open mast feed with a multitool.</summary>
[Serializable, NetSerializable]
public sealed partial class AU14MastZeroizeDoAfterEvent : SimpleDoAfterEvent;
