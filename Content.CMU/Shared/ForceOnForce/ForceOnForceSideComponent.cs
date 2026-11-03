using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ForceOnForce;

/// <summary>
///     Force on Force war side of a mob whose marine faction is not a war side, such as a
///     member of a third party whose allegiance matches exactly one side's platoon. When both
///     sides rolled platoons of that allegiance the tie stays unaligned and no component is
///     added. Identification treats the mob as a member of this side. Server sets it when the
///     party deploys.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ForceOnForceSideComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Side = string.Empty;
}
