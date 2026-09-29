using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.GameStates;

namespace Content.Shared.Parallax;

/// <summary>
/// Handles per-map parallax
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ParallaxComponent : Component
{
    // I wish I could use a typeserializer here but parallax is extremely client-dependent.
    [DataField, AutoNetworkedField]
    public string Parallax = "Default";

    // CMU: abstract transit uses steady camera-relative travel. Ordinary maps
    // keep their world-relative parallax when this is null.
    [DataField, AutoNetworkedField]
    public Vector2? TravelVelocity;
}
