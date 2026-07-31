using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUSurgeryFlowSystem))]
public sealed partial class CMUSurgeryArmedStepComponent : Component
{
    /// <summary>
    ///     Compatibility attribution for UI feedback and legacy projections.
    ///     Manual execution authority lives on the active session attempt.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid LastOperator;

    [DataField, AutoNetworkedField]
    public CMUSurgeryArmedStateId StateId;

    [DataField, AutoNetworkedField]
    public string SurgeryId = string.Empty;

    /// <summary>
    ///     The leaf surgery the medic originally picked. Differs from
    ///     <see cref="SurgeryId"/> when a prereq chain is in progress —
    ///     <see cref="SurgeryId"/> points at the prereq currently being
    ///     run, this is the eventual target. Used for BUI display only;
    ///     the V1 step-event dispatch keys off <see cref="SurgeryId"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string LeafSurgeryId = string.Empty;

    [DataField, AutoNetworkedField]
    public int StepIndex;

    [DataField, AutoNetworkedField]
    public int LastCompletedLeafStepIndex = -1;

    /// <summary>
    ///     UI-less procedures may continue from retracted tissue without
    ///     making the optional hemostasis action a linear prerequisite.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowOptionalHemostasis;

    [DataField, AutoNetworkedField]
    public BodyPartType TargetPartType;

    [DataField, AutoNetworkedField]
    public BodyPartSymmetry TargetSymmetry;

    /// <summary>
    ///     Null = no specific tool required.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? RequiredToolCategory;

    [DataField, AutoNetworkedField]
    public string StepLabel = string.Empty;

    [DataField, AutoPausedField, AutoNetworkedField]
    public TimeSpan ArmedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan ExpireAfter = TimeSpan.FromSeconds(30);
}
