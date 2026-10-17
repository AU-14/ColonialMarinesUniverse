using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Stimmaster;

/// <summary>
/// Adds autoinjector fabrication to an RMC ChemMaster-compatible machine.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUStimmasterComponent : Component
{
    /// <summary>
    /// Raw MaterialStorage units per injector. A CM sheet contains 3750 units, so each is 0.2 sheets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MetalCost = 750;

    [DataField, AutoNetworkedField]
    public int GlassCost = 750;

    [DataField(required: true), AutoNetworkedField]
    public List<EntProtoId> InjectorPrototypes = [];

    [DataField, AutoNetworkedField]
    public string InjectorContainer = "cmu_stimmaster_injectors";

    [DataField, AutoNetworkedField]
    public int MaxStoredInjectors = 64;

    [DataField, AutoNetworkedField]
    public int MaxFabricationAmount = 20;

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> SelectedInjectors = [];
}
