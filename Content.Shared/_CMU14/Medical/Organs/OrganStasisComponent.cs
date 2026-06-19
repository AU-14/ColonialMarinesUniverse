using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs;

/// <summary>
///     Marker for detached organs. Freezes <see cref="OrganHealthComponent"/>
///     regen; once <see cref="ExpireAt"/> is reached the organ is marked Dead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedOrganHealthSystem))]
public sealed partial class OrganStasisComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpireAt;
}
