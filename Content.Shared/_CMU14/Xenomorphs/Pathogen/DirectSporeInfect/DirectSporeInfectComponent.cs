using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.DirectSporeInfect;

/// <summary>
/// Gives the Popper a melee "infect" ability that directly infects
/// an adjacent living target without a doafter.
/// Costs plasma; can only be used on InfectableComponent targets.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoDirectSporeInfectSystem))]
public sealed partial class CMUXenoDirectSporeInfectComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlasmaCost = 50f;

    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    /// <summary>Embryo prototype to inject into the victim.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId EmbryoSpawn = "CMUXenoBloodburster";

    [DataField, AutoNetworkedField]
    public TimeSpan InfectDelay = TimeSpan.FromSeconds(1.5);
}