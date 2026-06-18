using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ChemicalIrritants;

/// <summary>
/// Tracks chemical irritant effects currently affecting a victim.
/// Effect parameters are stored in CheemicalIrritantProfil and copied
/// from the injector on first contact.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChemicalIrritantComponent : Component
{

    [DataField, AutoNetworkedField]
    public float IrritantAmount = 0f;

    [DataField, AutoNetworkedField]
    public float DepletionPerTick = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    public TimeSpan NextIrritantEffectAt;

    [DataField, AutoNetworkedField]
    public TimeSpan TimeBetweenMessages = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan LastMessage;
    
    [DataField, AutoNetworkedField]
    public TimeSpan LastTripTime;

    /// <summary>
    /// Effect parameters copied from the injector on first contact.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ChemicalIrritantProfile Profile = new();
}