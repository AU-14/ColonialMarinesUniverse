using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery;

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
    ///     May be deleted by the time a fresh surgeon walks up — UI should
    ///     fall back to <see cref="SurgeonName"/> in that case.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Surgeon;

    /// <summary>
    ///     Persists even if the surgeon entity gets deleted (round-end
    ///     disconnect, etc).
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SurgeonName = string.Empty;

    [DataField, AutoPausedField, AutoNetworkedField]
    public TimeSpan StartedAt;
}
