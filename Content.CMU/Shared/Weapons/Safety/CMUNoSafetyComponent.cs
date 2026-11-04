using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Safety;

/// <summary>
/// A gun with no manual safety to flip, like a Glock: no safety option, and it's never put on safety.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUNoSafetyComponent : Component;
