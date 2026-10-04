using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ChemicalIrritants;

/// <summary>
/// Component for clouds and dispensers that apply chemical irritants.
/// Effect parameters are defined once in ChemicalIrritantProfile and
/// copied onto the victim's ChemicalirritantComponent on contact.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedChemicalIrritantSystem))]
public sealed partial class ChemicalIrritantInjectorComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> ContactedEntities = new();

    [DataField(required: true), AutoNetworkedField]
    public float IrritantPerSecond = 10f;

    [DataField, AutoNetworkedField]
    public TimeSpan TimeBetweenGasInjects = TimeSpan.FromSeconds(1);
    
    [DataField, AutoNetworkedField]
    public bool AffectsInfectedNested = false;

    [DataField, AutoNetworkedField]
    public bool AffectsDead = false;

    [DataField, AutoNetworkedField]
    public TimeSpan NextGasInjectionAt;

    /// <summary>
    /// When >= 0, the injector has a finite supply. Set to -1 for infinite.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IrritantCapacity = -1f;

    [DataField, AutoNetworkedField]
    public float IrritantUsed = 0f;

    [DataField, AutoNetworkedField]
    public float FilterDamage = 5f;

    [DataField, AutoNetworkedField]
    public bool NeurotoxinFilterDamage = false;

    /// <summary>
    /// All effect parameters for this irritant. Copied to the victim on contact.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ChemicalIrritantProfile Profile = new();

    [DataField, AutoNetworkedField]
    public float DepletionPerTick = 2f;
}