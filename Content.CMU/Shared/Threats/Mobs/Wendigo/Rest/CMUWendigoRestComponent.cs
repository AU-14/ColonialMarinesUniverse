using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Rest;

/// <summary>
/// Lets a Wendigo rest with the xeno Rest action. Wendigos are not xenos, so <see cref="CMUWendigoRestSystem"/>
/// toggles it. The client swaps the sprite to <see cref="RestingState"/> while resting.
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
