using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vehicle;

// non-tank vehicle an OB wrote off for the round. pure gameplay marker, no visuals hang off it on purpose
// networked only so predicted repair/insert attempts get refused on the client too
[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleTotaledComponent : Component;
