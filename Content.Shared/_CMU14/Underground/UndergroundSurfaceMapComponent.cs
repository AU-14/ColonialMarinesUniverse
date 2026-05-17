using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Placed on the surface planet grid entity. Stores a reference to the underground mirror grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundSurfaceMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? UndergroundGrid;
}
