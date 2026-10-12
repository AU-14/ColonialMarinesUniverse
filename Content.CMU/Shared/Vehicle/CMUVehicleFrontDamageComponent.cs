using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// Head on crashes crumple a vehicle's front, shown in levels as <see cref="Damage"/> passes each of
/// <see cref="Thresholds"/>. A welder on the front beats it back out a pass at a time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleFrontDamageSystem))]
public sealed partial class CMUVehicleFrontDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Damage;

    [DataField]
    public float MaxDamage = 60f;

    /// <summary>
    /// The damage each level of crumple starts at, in rising order.
    /// </summary>
    [DataField]
    public List<float> Thresholds = new() { 8f, 25f, 45f };

    /// <summary>
    /// The share of a head on crash's hull damage the front takes.
    /// </summary>
    [DataField]
    public float CrashShare = 1f;

    [DataField]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Welding";

    [DataField]
    public TimeSpan RepairDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Damage taken off by each welding pass.
    /// </summary>
    [DataField]
    public float RepairAmount = 15f;

    [DataField]
    public float RepairFuel = 5f;

    /// <summary>
    /// How far forward on the body (in its crayon map's voxels) a click has to land to work the front.
    /// </summary>
    [DataField]
    public float FrontVoxel = 22f;

    /// <summary>
    /// Sprite layers showing the crumple on the vehicle and on its overlay, set to the state with the
    /// level appended and hidden while the front is straight.
    /// </summary>
    [DataField]
    public string Layer = "front_dent";

    [DataField]
    public string State = "front_dent_";

    [DataField]
    public string OverlayState = "front_dent_overlay_";

    /// <summary>
    /// A closed hood's state with the level appended, for a vehicle whose hood crumples with the front.
    /// </summary>
    [DataField]
    public string? ClosedHoodState;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleFrontRepairDoAfterEvent : SimpleDoAfterEvent;
