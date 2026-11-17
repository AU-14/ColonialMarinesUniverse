using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// ARCH-04 to ARCH-06: the Bad Blood castes. A Bad Blood takes one up at its station on the Lodge and keeps it for
/// the rest of that life.
/// </summary>
[Serializable, NetSerializable]
public enum BadBloodCaste : byte
{
    /// <summary>
    /// Builds anywhere, sees as an honourable hunter does, and carries anti-tank and demolition gear.
    /// </summary>
    Engineer,

    /// <summary>
    /// Works Yautja medicine faster than other Bad Bloods and carries the clan's medical supplies.
    /// </summary>
    Medic,
}

/// <summary>
/// On a Bad Blood who has taken up a caste.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodCasteComponent : Component
{
    [DataField, AutoNetworkedField]
    public BadBloodCaste Caste;

    /// <summary>
    /// What a Medic's medical and surgical steps take, as a share of another Bad Blood's.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MedicDelayMultiplier = 0.6f;
}
