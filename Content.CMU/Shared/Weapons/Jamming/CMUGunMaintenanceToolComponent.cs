using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Jamming;

/// <summary>
///     Use this on a gun to remove wear, like gun oil or a cleaning rag.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUGunJammingSystem))]
public sealed partial class CMUGunMaintenanceToolComponent : Component
{
    /// <summary>
    ///     Wear removed per use, as a fraction of the gun's max wear.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WearRemoved = 0.25f;

    /// <summary>
    ///     This tool can't bring a gun's wear below this fraction of its max wear.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinWear;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    /// <summary>
    ///     Uses left. Null means unlimited.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? Uses;

    [DataField, AutoNetworkedField]
    public bool DeleteWhenEmpty = true;

    /// <summary>
    ///     Whether a finished use also clears the gun's jam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ClearsJam;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;
}
