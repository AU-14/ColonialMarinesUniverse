using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Marker that specifies a map as being for FTLing entities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FTLMapComponent : Component
{
    public const float TravelSpeed = 20f; // CMU: shared by FTL physics and its visual backdrop.

    /// <summary>
    /// Offset for FTLing shuttles so they don't overlap each other.
    /// </summary>
    [DataField]
    public int Index;

    /// <summary>
    /// What parallax to use for the background, immediately gets deffered to ParallaxComponent.
    /// </summary>
    [DataField]
    public string Parallax = "FastSpace";

    /// <summary>
    /// Can FTL on this map only be done to beacons.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Beacons;
}
