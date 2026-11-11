using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Climbing;

/// <summary>
///     A railing built like a barricade (handrails, wire rails). Barricades aren't vaulted by walking into them, but
///     railings are.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVaultOnBumpRailingComponent : Component;
