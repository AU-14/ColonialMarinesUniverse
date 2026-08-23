using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs;

/// <summary>
///     Temporary organ-stabilization window supplied by Dathwei. This is
///     deliberately separate from <see cref="OrganStasisComponent"/>, which
///     represents a detached organ and suppresses its passive regeneration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUOrganStabilizedComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}
