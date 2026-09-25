using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Xenos;

/// <summary>
/// Vegetation and furniture that resin construction removes after placement succeeds.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ResinClearableComponent : Component;
