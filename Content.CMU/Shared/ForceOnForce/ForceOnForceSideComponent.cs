using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ForceOnForce;

/// <summary>
///     Force on Force war side of a mob whose marine faction is not a war side, such as a
///     member of a third party whose allegiance matches a side's platoon. Identification
///     treats the mob as a member of this side. Server sets it when the party deploys.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ForceOnForceSideComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Side = string.Empty;
}
