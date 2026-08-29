using Robust.Shared.GameStates;

namespace Content.Shared.Drunk;

/// <summary>
/// Marks a status effect entity as providing the client-side drunk presentation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DrunkStatusEffectComponent : Component
{
}
