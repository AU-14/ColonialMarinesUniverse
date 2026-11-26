using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Clothing;

/// <summary>Hides concealable holsters from observers when worn in the outer clothing slot.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUConcealsHolstersComponent : Component;
