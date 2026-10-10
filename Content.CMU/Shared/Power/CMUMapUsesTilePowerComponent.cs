using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Power;

/// <summary>Marks a map as opted into using tile/grid power, maps without use RMC area power</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMapUsesTilePowerComponent : Component;
