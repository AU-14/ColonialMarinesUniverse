using Robust.Shared.GameStates;

namespace Content.Shared.Eye.Blinding.Components;

/// <summary>
/// RMC compatibility component for blindness applied directly by unconsciousness.
/// Timed blindness uses the current status-effect entity instead.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class TemporaryBlindnessComponent : Component;
