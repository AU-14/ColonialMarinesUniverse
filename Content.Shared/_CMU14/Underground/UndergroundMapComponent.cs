using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Placed on the underground grid entity. Stores a reference to the corresponding surface grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid SurfaceGrid;
}
