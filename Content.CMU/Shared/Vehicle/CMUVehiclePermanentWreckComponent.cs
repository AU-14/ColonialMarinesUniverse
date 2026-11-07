using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>The hull becomes permanently unrepairable when damage reduces its integrity to zero.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehiclePermanentWreckComponent : Component;
