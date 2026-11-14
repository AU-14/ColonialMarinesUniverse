using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The Lodge armory: an automated vendor that sells from the clan's Superiority Points instead of the buyer's own
/// vendor points. Every entry is tier I and open to any Bad Blood unless it is listed here.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCampArmoryComponent : Component
{
    /// <summary>
    /// The vendor's points type. Each Bad Blood carries a whole-number copy of the clan's SP under this key, so
    /// the vendor window shows the clan's balance. The copy is display only, the price is taken from the rule.
    /// </summary>
    [DataField]
    public string PointsType = "BadBloodSp";

    /// <summary>
    /// The armory tier an entry needs. Entries that are not listed are tier I.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> Tiers = new();

    /// <summary>
    /// Entries only the clan's Leader can buy.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> LeaderOnly = new();

    /// <summary>
    /// What an entry costs once the clan ship has arrived (tier IV), where that differs from its normal price.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> TierFourPrices = new();

    /// <summary>
    /// Tier IV has been applied to this armory: its prices are the tier IV ones and nothing is Leader only.
    /// </summary>
    [ViewVariables]
    public bool TierFourApplied;
}

/// <summary>
/// On an armory ware that is a clan unlock and not an item: buying it unlocks the eradicator for the clan and the
/// ware itself is deleted.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodEradicatorUnlockComponent : Component;

/// <summary>
/// On an armory ware that is a clan unlock and not an item: buying it unlocks the lethal bolt for the whole clan
/// and the ware itself is deleted.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodLethalUnlockComponent : Component;

/// <summary>
/// On the rule entity: what the clan has unlocked at the armory. It outlives the Lodge.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodArmoryUnlocksComponent : Component
{
    [ViewVariables]
    public bool Eradicator;

    /// <summary>
    /// The clan has bought the lethal bolt. The eradicator cannot be bought before it.
    /// </summary>
    [ViewVariables]
    public bool Lethal;
}

/// <summary>
/// This Bad Blood may switch a plasma caster to the lethal bolt. Every Bad Blood holds it once the clan has bought
/// the lethal bolt at the armory or the clan ship has arrived, and always when no Bad Blood rule is running. A Bad
/// Blood without it has the stun and the immobilizer only.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodLethalBoltComponent : Component;

/// <summary>
/// This Bad Blood may switch a plasma caster to the eradicator. The clan's Leader holds it once the clan has
/// bought the eradicator, every Bad Blood holds it once the clan ship has arrived. A Bad Blood without it is
/// refused the eradicator. The lethal bolt has its own gate, <see cref="BadBloodLethalBoltComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodEradicatorComponent : Component
{
    /// <summary>
    /// The eradicator cannot be selected again before this time. Set by each eradicator shot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextUseAt;

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);
}
