using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Lifecycle is paired with <see cref="CMUSurgeryInProgressComponent"/>
///     on the patient body — set/cleared in lockstep.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUSurgeryFlowSystem))]
public sealed partial class CMUSurgeryInFlightComponent : Component
{
    /// <summary>
    ///     The deepest leaf in the requirement chain — not the prereq surgery
    ///     whose step is currently being run.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string LeafSurgeryId = string.Empty;

    [DataField, AutoNetworkedField]
    public string LeafSurgeryDisplayName = string.Empty;

    /// <summary>
    ///     Historical credit for the most recent completed step. This is not
    ///     an owner or authorization check and may refer to a deleted entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Surgeon;

    /// <summary>
    ///     Historical operator name that persists if the entity is deleted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SurgeonName = string.Empty;

    [DataField, AutoPausedField, AutoNetworkedField]
    public TimeSpan StartedAt;
}
