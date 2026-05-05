using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Brain;

/// <summary>
///     CMU-prefixed to avoid clashing with vanilla SS14's <c>BrainComponent</c>
///     at YAML registration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedBrainSystem))]
public sealed partial class CMUBrainComponent : Component
{
    /// <summary>
    ///     Probability per minute (0..1) of a single disorientation event while
    ///     stage is Bruised.
    /// </summary>
    [DataField]
    public float DisorientationChancePerMinute = 0.05f;

    [DataField, AutoNetworkedField]
    public float ActionSpeedMultiplier = 1.0f;

    [DataField, AutoPausedField]
    public TimeSpan NextDisorientCheck;

    [DataField, AutoPausedField]
    public TimeSpan NextUnconsciousCheck;

    /// <summary>
    ///     Permadeath flag applied once on Dead-stage transition. Prevents the
    ///     stage handler from re-applying the holocard / sleep status on every
    ///     re-entry of the Dead stage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PermadeathApplied;
}
