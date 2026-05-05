using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery;

/// <summary>
///     Patient-side singleton lock — ensures only one CMU surgery per
///     patient at a time. Set in lockstep with a
///     <see cref="CMUSurgeryInFlightComponent"/> on the part being operated
///     on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCMUSurgeryFlowSystem))]
public sealed partial class CMUSurgeryInProgressComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Part;

    [DataField, AutoNetworkedField]
    public string LeafSurgeryId = string.Empty;

    /// <summary>
    ///     For reattach surgeries, <see cref="Part"/> is the patient body
    ///     (no <see cref="BodyPartComponent"/>) — these fields disambiguate
    ///     which severed slot the in-flight surgery is targeting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartType TargetPartType;

    [DataField, AutoNetworkedField]
    public BodyPartSymmetry TargetSymmetry;

    /// <summary>
    ///     The functional repair is done and the medic must choose either
    ///     another repair on this same open part or a close-up surgery.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AwaitingClosureChoice;
}
