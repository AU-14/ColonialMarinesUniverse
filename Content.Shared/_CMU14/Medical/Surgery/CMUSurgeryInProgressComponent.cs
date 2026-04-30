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
}
