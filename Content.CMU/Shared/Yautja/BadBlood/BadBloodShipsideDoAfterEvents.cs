using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-514: a Bad Blood finished (or broke off) sabotaging the console a running Vessel Strike was fired from.
/// Raised on the Bad Blood doing it, so it still arrives when the console stops being the strike console.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodStrikeSabotageDoAfterEvent : SimpleDoAfterEvent;
