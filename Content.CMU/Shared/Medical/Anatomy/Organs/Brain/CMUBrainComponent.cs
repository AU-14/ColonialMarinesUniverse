using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;

/// <summary>
///     CMU-prefixed to avoid clashing with vanilla SS14's <c>BrainComponent</c>
///     at YAML registration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedBrainSystem))]
public sealed partial class CMUBrainComponent : Component
{
    [DataField]
    public float BruisedDisorientationChance = 0.05f;

    [DataField]
    public float DamagedDisorientationChance = 0.05f;

    [DataField]
    public float FailingDisorientationChance = 0.05f;

    [DataField]
    public TimeSpan DisorientationCheckInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DisorientationBlurDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public float DisorientationBlurStrength = 1.25f;

    [DataField]
    public TimeSpan DisorientationKnockdownDuration = TimeSpan.FromSeconds(1);

    [DataField]
    public float DisorientationDrunkPower = 4f;

    [DataField]
    public float BruisedVisionBlur = 0.25f;

    [DataField]
    public float DamagedVisionBlur = 0.75f;

    [DataField]
    public float FailingVisionBlur = 1.5f;

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

/// <summary>
///     Persistent visual impairment contributed by brain damage independently
///     from damage to the eyes themselves.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedBrainSystem))]
public sealed partial class CMUBrainVisionImpairmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Magnitude;
}
