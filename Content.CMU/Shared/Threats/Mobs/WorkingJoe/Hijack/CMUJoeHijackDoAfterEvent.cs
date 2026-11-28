using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;

/// <summary>The Offer on a dead synthetic: when it completes, the synthetic becomes a hijacked Working Joe.</summary>
[Serializable, NetSerializable]
public sealed partial class CMUJoeHijackDoAfterEvent : SimpleDoAfterEvent;
