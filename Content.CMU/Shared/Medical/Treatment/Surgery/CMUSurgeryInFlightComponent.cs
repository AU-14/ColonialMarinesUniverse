using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Lifecycle is paired with <see cref="CMUSurgeryInProgressComponent"/>
///     on the patient body — set/cleared in lockstep.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(SharedCMUSurgeryFlowSystem), typeof(CMUSurgeryInFlightStateSystem))]
public sealed partial class CMUSurgeryInFlightComponent : Component
{
    /// <summary>
    ///     The deepest leaf in the requirement chain — not the prereq surgery
    ///     whose step is currently being run.
    /// </summary>
    [DataField]
    public string LeafSurgeryId = string.Empty;

    [DataField]
    public string LeafSurgeryDisplayName = string.Empty;

    /// <summary>
    ///     Historical credit for the most recent completed step. This is not
    ///     an owner or authorization check and may refer to a deleted entity.
    /// </summary>
    [DataField]
    public EntityUid Surgeon;

    /// <summary>
    ///     Historical operator name that persists if the entity is deleted.
    /// </summary>
    [DataField]
    public string SurgeonName = string.Empty;

    [DataField, AutoPausedField]
    public TimeSpan StartedAt;
}
