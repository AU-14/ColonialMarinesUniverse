using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Marks an entity as an ordnance assembly part (igniter or timer).
///     Two parts of compatible types are combined via interaction to produce a detonator assembly.
/// </summary>

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCOrdnancePartComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public RMCOrdnancePartType PartType;
}
public enum RMCOrdnancePartType
{
    RMCOrdnanceIgniter,
    RMCOrdnanceTimer,
    RMCOrdnanceSignaler,
    RMCOrdnanceProximitySensor
}
