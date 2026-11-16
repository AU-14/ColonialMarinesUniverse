using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-509: a Bad Blood finished (or broke off) sabotaging a marine objective site: an uplink site or a colony
/// reactor. Raised on the site.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodSiteSabotageDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// ARCH-505: a marine finished (or broke off) moving the Vessel Strike's lock to another working uplink site.
/// Raised on the new site.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodRerouteLockDoAfterEvent : SimpleDoAfterEvent;
