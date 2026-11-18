using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// CMU14 ARCH-603: the Lodge kennel is a free hound spawner, the pattern of RMC's lesser-drone pylon
// (HiveLesserSpawnerComponent). Ghosts take hounds from its GhostRoleMobSpawner; SP buys rate and stock, not bodies
/// <summary>
/// The Lodge kennel's hound stock. <see cref="Current"/> takeovers are on offer, live hounds included. It starts at
/// <see cref="MinStock"/> when the kennel is pitched and grows by one every <see cref="Interval"/> up to
/// <see cref="Max"/>: <see cref="MinStock"/> or one hound for every <see cref="BadBloodsPerHound"/> living Bad Bloods,
/// whichever is more. A hound that dies takes its takeover with it, so the stock regrows one at a time.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BadBloodKennelSpawnerComponent : Component
{
    [DataField]
    public int MinStock = 2;

    [DataField]
    public int BadBloodsPerHound = 2;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(120);

    /// <summary>
    /// While the Blood Reserve has anyone waiting, only they can take a hound for this long after one is stocked.
    /// </summary>
    [DataField]
    public TimeSpan ReservePriority = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAt;

    /// <summary>
    /// When the last takeover was stocked, at pitch or by <see cref="Interval"/>. The reserve's window starts here.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan StockedAt;

    [ViewVariables]
    public int Max;

    [ViewVariables]
    public int Current;

    [ViewVariables]
    public List<EntityUid> LiveHounds = new();

    /// <summary>
    /// STATE-602: kennel upgrades bought at the armory, by ware.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntProtoId, int> Upgrades = new();
}

/// <summary>
/// CMU14 STATE-602: on an armory ware that upgrades the Lodge kennel and is not an item. Buying it multiplies the
/// kennel's <see cref="BadBloodKennelSpawnerComponent.Interval"/> and raises its
/// <see cref="BadBloodKennelSpawnerComponent.MinStock"/>, and the ware itself is deleted.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodKennelUpgradeComponent : Component
{
    [DataField]
    public float IntervalMultiplier = 1f;

    [DataField]
    public int MinStockBonus;

    /// <summary>
    /// How often the clan can buy this ware for one kennel.
    /// </summary>
    [DataField]
    public int MaxBuys = 1;
}
