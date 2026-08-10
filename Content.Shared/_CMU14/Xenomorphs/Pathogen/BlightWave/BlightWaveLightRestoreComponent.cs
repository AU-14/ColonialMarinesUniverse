using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.BlightWave;

/// <summary>
/// Temporarily added to a light-emitting entity to track its pre-wave radius
/// so we can restore it after LightOffDuration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUXenoBlightWaveLightRestoreComponent : Component
{
    [DataField, AutoNetworkedField]
    public float OriginalRadius;

    [DataField, AutoNetworkedField]
    public TimeSpan RestoreAt;
}