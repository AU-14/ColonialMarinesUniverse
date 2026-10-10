using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared._RMC14.Xenonids.Designer;
using Content.Shared.Damage;
using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Weeds;

// TODO RMC14 field deltas for auto states
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedXenoWeedsSystem), typeof(HiveBoonSystem), typeof(WeedboundWallSystem))]
public sealed partial class XenoWeedsComponent : Component
{
    [DataField]
    public int Range = 5;

    [DataField]
    public float SpeedMultiplierXeno = 1.05f; //MOVE_DELAY * 0.95

    [DataField]
    public float SpeedMultiplierOutsider = 0.5714f;

    [DataField]
    public float SpeedMultiplierOutsiderArmor = 0.6666f;

    /// <summary>
    /// How much health is healed when the weeds stop spreading.
    /// </summary>
    [DataField]
    public DamageSpecifier HealOnStopSpreading = new();

    [DataField]
    public bool HasHealed = false;

    [DataField]
    public bool IsSource = true;

    [DataField]
    public EntityUid? Source;

    [DataField]
    public EntProtoId Spawns = "XenoWeeds";

    [DataField]
    public List<EntityUid> Spread = new();

    /// <summary>
    /// All anchored entities with Weedable component adjacent to this entity
    /// are added here.
    /// </summary>
    [DataField]
    public List<EntityUid> LocalWeeded = new();

    [DataField]
    public TimeSpan MinRandomDelete = TimeSpan.FromSeconds(9);

    [DataField]
    public TimeSpan MaxRandomDelete = TimeSpan.FromSeconds(10);

    [DataField]
    public bool SpreadsOnSemiWeedable;

    [DataField]
    public float FruitGrowthMultiplier = 1.0f;

    [DataField]
    public int Level = 1;

    [DataField]
    public bool BlockOtherWeeds;

    [DataField]
    public List<EntityUid> WeedboundStructures = new();
}
