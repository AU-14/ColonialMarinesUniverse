using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Chemistry;

/// <summary>
/// Thrown entities with this skip the BarricadeBlock pass-over roll in ThrownItemSystem, so they always touch
/// barricades, sandbags and the like. Spray vapor is thrown, and without this it sailed over 88% of the time,
/// so contact chems (Repairing) almost never reached the barricade being sprayed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUThrownIgnoresBarricadeBlockComponent : Component;
