using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Marks an item as a detonator assembly that can be inserted into a chembomb casing.
///     The actual trigger behaviour is provided by the PayloadTrigger component on the same entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCDetonatorAssemblyComponent : Component
{
    [DataField, AutoNetworkedField]
    public RMCDetonatorType DetonatorType = RMCDetonatorType.Timer;

    // Indicates whether the detonator assembly is armed and ready to trigger.
    [DataField, AutoNetworkedField]
    public bool Ready = false;
}

public enum RMCDetonatorType : byte
{
    Timer,
    DoubleIgniter,
    Proximity,
    Signaler,
}
