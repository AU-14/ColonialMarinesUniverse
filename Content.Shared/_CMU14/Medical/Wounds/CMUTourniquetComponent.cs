using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Wounds;

/// <summary>
///     While present, the wound layer skips the part's internal-bleed tick
///     and the tourniquet system runs a necrosis countdown. Removing the
///     component stops the countdown and clears any necrosis on the part.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUTourniquetComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppliedAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NecrosisAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan SeveranceAt;

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField, AutoNetworkedField]
    public EntProtoId? RefundOnRemove;
}
