using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.DirectSporeInfect;

/// <summary>
/// An interruptible direct infection against an adjacent living target.
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
    public EntProtoId EmbryoSpawn = "CMU14XenoBloodburster";

    [DataField, AutoNetworkedField]
    public TimeSpan InfectDelay = TimeSpan.FromSeconds(1.5);
}
