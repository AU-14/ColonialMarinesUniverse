using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// CMU14 Playtest 5 (docs/GraphAudit-BadBlood-Playtest5Plan.md ARCH-702, ECS-707, ARCH-707): the Bad Blood pack layer.
// Every clan beast (kennel hound, thrall) carries BadBloodBeast; the Beast-Master caste carries BadBloodBeastMaster.
// Beasts never use BadBloodHoundComponent for anything but the hound's own kennel and sleep logic.

/// <summary>
/// A clan beast: a kennel hound or a thrall. <see cref="Master"/> is the Bad Blood it answers to, a Beast-Master
/// before the Hunt Leader (BadBloodBeastSystem.GetMasterFor). Other systems read "has this component" as "clan beast".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodBeastComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    /// <summary>
    /// ECS-707 Last Order: the master died, the beast rages and looks for a new master at this time.
    /// </summary>
    [ViewVariables]
    public TimeSpan? RebindAt;

    /// <summary>
    /// ECS-707 Mend Beast: no Beast-Master can mend this beast again before this time.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextMendAt;
}

/// <summary>
/// The Beast-Master caste. Added with no fields by the caste prototype, so every field has its default here.
/// Holds the pack it commands and the pack actions (ECS-707).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodBeastMasterComponent : Component
{
    /// <summary>
    /// ARCH-707: the most living thralls (and unclaimed cages) this Beast-Master may have at once.
    /// </summary>
    [DataField]
    public int MaxThralls = 2;

    /// <summary>
    /// ECS-707 Pack Bond: bonded beasts this close regenerate faster.
    /// </summary>
    [DataField]
    public float BondRadius = 7f;

    /// <summary>
    /// Pack Bond: the beast's standing regeneration is multiplied by this inside <see cref="BondRadius"/>.
    /// </summary>
    [DataField]
    public float BondRegenMultiplier = 1.5f;

    /// <summary>
    /// ARCH-705: the caste's cost, the Beast-Master's armour counts for less.
    /// </summary>
    [DataField]
    public float ArmorMultiplier = 0.9f;

    /// <summary>
    /// STATE-702: the Beast-Master pays this share for the kennel's upgrade wares.
    /// </summary>
    [DataField]
    public float KennelWareMultiplier = 0.6f;

    /// <summary>
    /// STATE-702: the first falcon drone is free. Set once it is claimed.
    /// </summary>
    [ViewVariables]
    public bool FreeFalconClaimed;

    /// <summary>
    /// The beasts bound to this Beast-Master. Kept in step by BadBloodBeastSystem.Bind.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Beasts = new();

    // ---- Blood Scent ----

    [DataField]
    public EntProtoId BloodScentAction = "CMUActionBadBloodBloodScent";

    [DataField, AutoNetworkedField]
    public EntityUid? BloodScentActionEntity;

    [DataField]
    public TimeSpan BloodScentDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public float BloodScentMultiplier = 1.35f;

    // ---- Rally Howl ----

    [DataField]
    public EntProtoId RallyHowlAction = "CMUActionBadBloodRallyHowl";

    [DataField, AutoNetworkedField]
    public EntityUid? RallyHowlActionEntity;

    [DataField]
    public float RallyRadius = 15f;

    [DataField]
    public TimeSpan RallyDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public float RallySpeedMultiplier = 1.2f;

    // ---- Mend Beast ----

    [DataField]
    public EntProtoId MendBeastAction = "CMUActionBadBloodMendBeast";

    [DataField, AutoNetworkedField]
    public EntityUid? MendBeastActionEntity;

    [DataField]
    public TimeSpan MendDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float MendHeal = 60f;

    /// <summary>
    /// Per beast: one beast cannot be mended again for this long.
    /// </summary>
    [DataField]
    public TimeSpan MendCooldown = TimeSpan.FromSeconds(60);

    // ---- Pack Bond: the built-in hound camera ----

    [DataField]
    public EntProtoId PackSightAction = "CMUActionBadBloodPackSight";

    [DataField, AutoNetworkedField]
    public EntityUid? PackSightActionEntity;

    [DataField]
    public EntProtoId PackSightCameraPrototype = "CMUBadBloodHoundObservationPadInternalCamera";

    [ViewVariables]
    public EntityUid? PackSightCamera;

    // ---- Last Order ----

    /// <summary>
    /// When the Beast-Master dies, its beasts rage this long and then find a new master.
    /// </summary>
    [DataField]
    public TimeSpan LastOrderDuration = TimeSpan.FromSeconds(20);
}

/// <summary>
/// ARCH-707: a thralled xeno of the clan, bought as a cage at the kennel. Not a hound: its own caps, its own
/// unclaimed timeout (it goes back into a cage, not to a sleeper).
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodThrallBeastComponent : Component
{
    /// <summary>
    /// A thrall nobody drives for this long goes back into a cage of the same kind where it stands.
    /// </summary>
    [DataField]
    public TimeSpan UnclaimedTimeout = TimeSpan.FromMinutes(3);

    [ViewVariables]
    public TimeSpan? AbandonedAt;

    /// <summary>
    /// While the Blood Reserve has anyone waiting, only they can take an abandoned thrall for this long.
    /// </summary>
    [DataField]
    public TimeSpan ReservePriority = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The cage this thrall came out of, so an unclaimed thrall can go back into one.
    /// </summary>
    [ViewVariables]
    public EntProtoId? CagePrototype;
}

/// <summary>
/// ARCH-707: a thrall cage stocked at the kennel. A ghost takes the thrall from its GhostRoleMobSpawner; the cage's
/// own YautjaHivebreaker is the conversion's settings. <see cref="Master"/> is the Beast-Master who bought it.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodThrallCageComponent : Component
{
    [ViewVariables]
    public EntityUid? Master;

    /// <summary>
    /// When the cage was stocked. While the Blood Reserve has anyone waiting, only they can take the thrall for
    /// <see cref="ReservePriority"/> after this.
    /// </summary>
    [ViewVariables]
    public TimeSpan StockedAt;

    [DataField]
    public TimeSpan ReservePriority = TimeSpan.FromSeconds(60);
}

/// <summary>
/// ECS-707 Blood Scent: this target takes <see cref="Multiplier"/> from every beast bonded to <see cref="Master"/>
/// until <see cref="ExpiresAt"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodBloodScentComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Master;

    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float Multiplier = 1.35f;
}

/// <summary>
/// ECS-707 Rally Howl: a beast that heard its Beast-Master's howl runs faster until <see cref="ExpiresAt"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodRallyHowlComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.2f;
}

/// <summary>
/// STATE-702: on a kennel ware that is not an item. Buying it war-paints the kennel: its hounds bite harder.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodWarPaintWareComponent : Component
{
    [DataField]
    public float Multiplier = 1.15f;
}

/// <summary>
/// STATE-702: a war-painted kennel or a war-painted beast. On a beast it multiplies its own melee damage.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodWarPaintComponent : Component
{
    [DataField]
    public float Multiplier = 1.15f;
}

/// <summary>
/// STATE-701 / STATE-702: price rules of a Lodge armory, on top of BadBloodCampArmory. Read by
/// BadBloodCampSystem.GetArmoryPrice.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodArmoryPriceComponent : Component
{
    /// <summary>
    /// STATE-701 Grunt passive: a Bad Blood with no caste who is not the Hunt Leader pays this share for tier I wares.
    /// 1 turns it off.
    /// </summary>
    [DataField]
    public float GruntTierOneMultiplier = 1f;

    /// <summary>
    /// STATE-702: a Beast-Master pays its <see cref="BadBloodBeastMasterComponent.KennelWareMultiplier"/> for these.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> KennelWares = new();

    /// <summary>
    /// STATE-702: the first of these a Beast-Master buys is free.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> FirstFreeForBeastMaster = new();
}

public sealed partial class BadBloodBloodScentActionEvent : EntityTargetActionEvent;

public sealed partial class BadBloodRallyHowlActionEvent : InstantActionEvent;

public sealed partial class BadBloodMendBeastActionEvent : EntityTargetActionEvent;

public sealed partial class BadBloodPackSightActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class BadBloodMendBeastDoAfterEvent : SimpleDoAfterEvent;
