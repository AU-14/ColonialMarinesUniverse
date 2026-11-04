using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Pulling;

/// <summary>
/// Cancels the hardcoded 0.95x slowdown that <c>PullerComponent</c> applies to anyone pulling anything.
/// Pulls of held-speed entities (crates etc.) keep their own math, and <c>SlowOnPull</c> still applies.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUNoPullSlowdownComponent : Component;
