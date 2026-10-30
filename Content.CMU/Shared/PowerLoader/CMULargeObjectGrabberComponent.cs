using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PowerLoader;

/// <summary>
/// Lets a mob lift <c>PowerLoaderGrabbable</c> objects (crates etc.) into a free hand, like a power loader does.
/// The holder is authorized to hold them and is exempt from the power loader's held-cargo slowdown.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMULargeObjectGrabberComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMULargeObjectGrabDoAfterEvent : SimpleDoAfterEvent;
