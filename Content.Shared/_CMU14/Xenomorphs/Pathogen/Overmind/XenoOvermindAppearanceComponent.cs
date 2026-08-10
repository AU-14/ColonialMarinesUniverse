using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;

/// <summary>
/// Tracks the Overmind's visual state: incorporeal (eye/ghost) vs manifested (physical form),
/// and whether a transition animation is currently playing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUXenoOvermindAppearanceComponent : Component
{
    /// <summary>
    /// True while the Overmind is in incorporeal (ghost) form - invisible, passable, eye sprite.
    /// False while manifested (physical, can fight).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Incorporeal = true;

    /// <summary>
    /// True while the appear/disappear transition animation is playing.
    /// Blocks movement, ability use, and re-triggering change_form during this window.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InTransition;

    /// <summary>
    /// Duration of the appear/disappear animation. Must match the RSI frame delays.
    /// </summary>
    [DataField]
    public TimeSpan TransitionDuration = TimeSpan.FromSeconds(2.0);

    /// <summary>GameTick at which the current transition ends.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TransitionEndsAt;
}