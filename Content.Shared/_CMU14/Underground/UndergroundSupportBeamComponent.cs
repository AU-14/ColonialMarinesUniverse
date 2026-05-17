using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Placed on wooden support beam entities underground.
/// Prevents cave-ins within a radius around this beam.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundSupportBeamComponent : Component
{
    /// <summary>
    /// Tile radius of support. 2 means a 5x5 area (2 tiles in each direction).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Radius = 2;
}
