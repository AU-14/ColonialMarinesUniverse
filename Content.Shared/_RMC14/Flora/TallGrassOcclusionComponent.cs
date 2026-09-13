using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Flora;

/// <summary>
/// Adds the portion of a tall-grass sprite that must render above mobs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TallGrassOcclusionComponent : Component
{
    [DataField, AutoNetworkedField]
    public string State = "tallgrass_overlay";

    /// <summary>
    /// Whether the cardinal direction of the base sprite maps to the diagonal half of an eight-direction overlay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Diagonal;
}
