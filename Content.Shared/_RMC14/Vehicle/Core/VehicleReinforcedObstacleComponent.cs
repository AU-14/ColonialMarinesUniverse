using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>
/// Reinforcement that can damage heavy vehicles when rammed. Ordinary structures
/// can consume momentum without damaging the vehicle that breaks through them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleReinforcedObstacleComponent : Component;
