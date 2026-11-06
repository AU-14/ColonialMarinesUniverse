using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Emplacements;

/// <summary>Prevents normal portable weapon mounts from being deployed aboard this grid.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBlockEmplacementDeploymentComponent : Component;
