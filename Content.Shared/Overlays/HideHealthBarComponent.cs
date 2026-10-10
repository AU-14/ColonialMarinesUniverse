using Robust.Shared.GameStates;

namespace Content.Shared.Overlays;

/// <summary>
/// Prevents the world-space health bar overlay from being drawn for this entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HideHealthBarComponent : Component;
