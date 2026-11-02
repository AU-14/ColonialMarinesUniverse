using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.Jamming;

/// <summary>
///     Tracks a gun's wear and jam state. Handheld guns get this automatically from the default
///     <see cref="CMUGunJammingPrototype"/>; add it to a gun prototype to change that gun's settings.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUGunJammingSystem))]
public sealed partial class CMUGunJammingComponent : Component
{
    /// <summary>
    ///     Balance profile this gun uses.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CMUGunJammingPrototype> Profile = CMUGunJammingSystem.DefaultProfile;

    /// <summary>
    ///     With this off, this gun doesn't wear or jam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     Scales all wear this gun takes, including from stock slams.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WearMultiplier = 1f;

    /// <summary>
    ///     Scales this gun's chance to jam on each round fired.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float JamChanceMultiplier = 1f;

    /// <summary>
    ///     Scales this gun's chance to be destroyed by a stock slam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SlamBreakChanceMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public bool CanSlam = true;

    /// <summary>
    ///     Current wear, from 0 to the profile's max wear. Set it in a prototype or map to spawn a worn gun.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Wear;

    [DataField, AutoNetworkedField]
    public CMUGunJamType Jam = CMUGunJamType.None;

    /// <summary>
    ///     The internals are destroyed and the gun can't fire.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPopup;
}
