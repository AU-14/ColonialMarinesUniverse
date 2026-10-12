using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Rest;

/// <summary>
/// Lets a Wendigo rest with the xeno Rest action. A natural Wendigo is a xeno and uses the xeno rest handler; a
/// lab-made one is not, so <see cref="CMUWendigoRestSystem"/> toggles it. Either way the client swaps the sprite
/// to <see cref="RestingState"/> while resting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWendigoRestComponent : Component
{
    /// <summary>
    /// Sprite state shown while resting. The Wendigo has no resting art; the curled "dead" pose stands in.
    /// </summary>
    [DataField]
    public string RestingState = "dead";

    [DataField]
    public string StandingState = "alive";
}
