using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Doors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMDoorSystem))]
public sealed partial class RMCPodDoorComponent : Component
{
    // CMU14: empty instead of null, view variables can't edit a null string (empty still means "use the name")
    [DataField, AutoNetworkedField]
    public string? Id = string.Empty;

    /// <summary>
    /// The doafter time multiplier for when a xeno is prying a podlock
    /// </summary>
    [DataField]
    public float XenoPodlockPryMultiplier = 4.0f;
}
