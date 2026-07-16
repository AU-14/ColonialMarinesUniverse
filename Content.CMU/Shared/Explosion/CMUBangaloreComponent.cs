using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Explosion;

/// <summary>
///     Placed on the ground with a short do-after when used in hand. Once placed it is anchored and can no
///     longer be picked back up; it arms its <see cref="Content.Shared._RMC14.Explosion.RMCExplosiveDeleteComponent"/>
///     timer immediately.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUBangaloreComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlacementDelay = 3;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? DeploySound;
}
