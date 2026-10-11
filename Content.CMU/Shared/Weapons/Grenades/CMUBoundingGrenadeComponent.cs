using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.Grenades;

/// <summary>
/// Bounding grenade like the M51A BFAB, ported from cmss13-pve#517.
/// When it lands armed, either thrown by hand with its pin pulled or fired from a launcher,
/// it hops up into the air and detonates at the top of the jump.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUBoundingGrenadeComponent : Component
{
    /// <summary>How long the hop takes before it goes off.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BoundTime = TimeSpan.FromSeconds(0.6);

    /// <summary>How high the grenade jumps, in tiles.</summary>
    [DataField, AutoNetworkedField]
    public float BoundHeight = 0.9f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? DetonateAt;

    [DataField, AutoNetworkedField]
    public EntityUid? User;
}
