using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Fireman;

/// <summary>
/// On a grabber: scales how long its victim's break-free struggle takes (FLOW-02). Read by FiremanCarrySystem when the
/// victim of a carry or aggressive grab starts to struggle. Networked because the struggle do-after is predicted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUGrabEscapeMultiplierComponent : Component
{
    /// <summary>Multiplies FiremanCarriableComponent.Delay. 1 leaves the struggle unchanged.</summary>
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
