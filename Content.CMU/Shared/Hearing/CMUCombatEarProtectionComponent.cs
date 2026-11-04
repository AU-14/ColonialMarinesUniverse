using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Hearing;

/// <summary>
///     Worn gear with this protects the wearer from combat hearing loss without blocking other deafness.
///     Headsets and anything with RMCEarProtection already count.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCombatEarProtectionComponent : Component;
