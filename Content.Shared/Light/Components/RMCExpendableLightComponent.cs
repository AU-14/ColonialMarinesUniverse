namespace Content.Shared.Light.Components;

/// <summary>
///     RMC data shared by the current client and server expendable-light components.
/// </summary>
public abstract partial class SharedExpendableLightComponent
{
    [DataField]
    public bool StartsActivated;

    [DataField]
    public bool UsesOverlay = true;

    /// <summary>
    ///     Can the expendable light be picked up while it's turned on.
    /// </summary>
    [DataField]
    public bool PickupWhileOn = true;

    /// <summary>
    ///     How much faster this light gets dimmed by acid.
    /// </summary>
    [DataField]
    public float AcidDamageMultiplier = 1;

    [DataField]
    public TimeSpan PhaseOneDuration;

    [DataField]
    public TimeSpan PhaseTwoDuration;

    [DataField]
    public TimeSpan PhaseThreeDuration;

    [DataField]
    public TimeSpan PhaseFourDuration;

    [DataField]
    public TimeSpan PhaseFiveDuration;

    [DataField]
    public string PhaseOneBehaviourID = "phase_1";

    [DataField]
    public string PhaseTwoBehaviourID = "phase_2";

    [DataField]
    public string PhaseThreeBehaviourID = "phase_3";

    [DataField]
    public string PhaseFourBehaviourID = "phase_4";

    [DataField]
    public string PhaseFiveBehaviourID = "phase_5";

    public bool Activated => CurrentState is ExpendableLightState.Lit
        or ExpendableLightState.PhaseOne
        or ExpendableLightState.PhaseTwo
        or ExpendableLightState.PhaseThree
        or ExpendableLightState.PhaseFour
        or ExpendableLightState.PhaseFive
        or ExpendableLightState.Fading;
}
