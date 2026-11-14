using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fireman;

/// <summary>
/// The short windup of a telegraphed one-click aggressive grab (the Early Warning Joe's Machine Grip, FLOW-02). The
/// user is the grabber, the target the pulled victim; on completion the grab goes through TryAggressiveGrab.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUTelegraphGrabDoAfterEvent : SimpleDoAfterEvent;
