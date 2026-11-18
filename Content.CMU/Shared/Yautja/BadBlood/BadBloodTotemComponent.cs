using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// A Clan Totem: the clan's hold on a piece of the map. While it stands consecrated it pays the clan Superiority
/// Points, more the longer it has stood, and Bad Bloods around it tire and hunger more slowly. Marines take it out of
/// play with the Desecrate verb, the clan takes it back with Reconsecrate.
/// The clan's Totem list and limit are on the rule entity. This is the state of one Totem.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodTotemComponent : Component
{
    /// <summary>
    /// When it was raised. The marine map pin counts from this (FLOW-604); ripening counts <see cref="StoodFor"/>.
    /// </summary>
    [ViewVariables]
    public TimeSpan PlacedAt;

    /// <summary>
    /// CMU14 FLOW-607: how long it has stood uncontested. Income ripens by this, so time the prey holds the ground
    /// around it does not count.
    /// </summary>
    [ViewVariables]
    public TimeSpan StoodFor;

    /// <summary>
    /// CMU14 FLOW-607: a living marine stands within <see cref="ContestRadius"/> and no living Bad Blood does. A
    /// contested Totem books no income, does not ripen and has no aura. Only a consecrated Totem is contested.
    /// </summary>
    [ViewVariables]
    public bool Contested;

    /// <summary>
    /// When the running contest started. Null while it is not contested.
    /// </summary>
    [ViewVariables]
    public TimeSpan? ContestedSince;

    /// <summary>
    /// The clan is told a Totem is contested at most this often, per Totem.
    /// </summary>
    [DataField]
    public TimeSpan ContestAnnounceCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// When the clan was last told this Totem is contested, for <see cref="ContestAnnounceCooldown"/>.
    /// </summary>
    [ViewVariables]
    public TimeSpan? LastContestAnnouncedAt;

    /// <summary>
    /// The clan was told of the running contest, so it is also told when it ends.
    /// </summary>
    [ViewVariables]
    public bool ContestAnnounced;

    /// <summary>
    /// Marines who have had the contest popup during the running contest. Cleared when it ends.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> ContestWarned = new();

    /// <summary>
    /// The Totem's light while it works. It is dimmed to <see cref="ContestedLightEnergy"/> while contested and off
    /// while desecrated.
    /// </summary>
    [DataField]
    public float LightEnergy = 1.5f;

    [DataField]
    public float ContestedLightEnergy = 0.4f;

    /// <summary>
    /// The clan counts it: it was raised while the rule ran and the clan was under its limit.
    /// </summary>
    [ViewVariables]
    public bool Registered;

    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Income earned and not yet paid to the clan, in SP.
    /// </summary>
    [ViewVariables]
    public double PendingIncome;

    /// <summary>
    /// Everything this Totem has paid the clan so far, in SP.
    /// </summary>
    [ViewVariables]
    public FixedPoint4 PaidIncome;

    /// <summary>
    /// SP per minute for a Totem younger than <see cref="GrowingAfter"/>.
    /// </summary>
    [DataField]
    public float YoungIncome = 0.2f;

    /// <summary>
    /// SP per minute between <see cref="GrowingAfter"/> and <see cref="RipeAfter"/>.
    /// </summary>
    [DataField]
    public float GrowingIncome = 0.4f;

    /// <summary>
    /// SP per minute once ripe.
    /// </summary>
    [DataField]
    public float RipeIncome = 0.7f;

    [DataField]
    public TimeSpan GrowingAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A Totem that has stood uncontested this long (<see cref="StoodFor"/>) is ripe. Ripe Totems held together call
    /// the clan ship early.
    /// </summary>
    [DataField]
    public TimeSpan RipeAfter = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Pending income is paid to the clan in steps of this size.
    /// </summary>
    [DataField]
    public FixedPoint4 PayoutStep = FixedPoint4.New(0.01);

    /// <summary>
    /// In tiles. A living Bad Blood this close stops a marine reading the Totem for intel. FLOW-607: a living marine
    /// this close with no Bad Blood contests it. Desecration does not depend on who stands nearby.
    /// </summary>
    [DataField]
    public float ContestRadius = 7f;

    [DataField]
    public float AuraRadius = 7f;

    /// <summary>
    /// How long after it is raised the Totem shows on the marine and GOVFOR tactical maps.
    /// </summary>
    [DataField]
    public TimeSpan MapRevealDelay = TimeSpan.FromSeconds(120);

    [ViewVariables]
    public bool MapRevealed;

    /// <summary>
    /// Added once the Totem shows on the marine map.
    /// </summary>
    [DataField]
    public ComponentRegistry RevealComponents = new();

    /// <summary>
    /// How long the Lodge shows on the marine map after this Totem is destroyed.
    /// </summary>
    [DataField]
    public TimeSpan LodgeRevealTime = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Intel the marines get for destroying it.
    /// </summary>
    [DataField]
    public FixedPoint2 DestroyedIntel = FixedPoint2.New(0.7);

    [DataField]
    public string IntelTeam = "govfor";

    /// <summary>
    /// Dropped when it is destroyed.
    /// </summary>
    [DataField]
    public EntProtoId? Loot = "CMUYautjaHumanSkullTrophy";

    /// <summary>
    /// The destruction has been handled, so it is not handled again when the entity is deleted.
    /// </summary>
    [ViewVariables]
    public bool Destroyed;

    /// <summary>
    /// FLOW-503: a marine finished the Desecrate do-after on it. A desecrated Totem pays nothing, is not ripe and
    /// has no aura until a Bad Blood finishes the Reconsecrate do-after.
    /// </summary>
    [ViewVariables]
    public bool Desecrated;

    /// <summary>
    /// The marine Desecrate do-after. Moving or taking damage breaks it.
    /// </summary>
    [DataField]
    public TimeSpan DesecrateTime = TimeSpan.FromSeconds(45);

    /// <summary>
    /// The Bad Blood Reconsecrate do-after. Moving or taking damage breaks it.
    /// </summary>
    [DataField]
    public TimeSpan ReconsecrateTime = TimeSpan.FromSeconds(45);
}

/// <summary>
/// On a Bad Blood standing in a Clan Totem's aura. The server adds and removes it as they walk in and out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodTotemAuraComponent : Component
{
    /// <summary>
    /// Hunger and thirst drain in the aura, as a share of the normal drain.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DrainMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public float BracerRegenMultiplier = 1.5f;

    /// <summary>
    /// Health mended per second once out of combat.
    /// </summary>
    [DataField]
    public float Heal = 0.5f;

    /// <summary>
    /// The aura mends nothing until its holder has gone this long without taking damage.
    /// </summary>
    [DataField]
    public TimeSpan HealDelay = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan LastDamagedAt;
}

/// <summary>
/// On every Bad Blood while the clan's rule runs: the Raise Totem action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodTotemBuilderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId RaiseTotemAction = "CMUActionBadBloodRaiseTotem";

    [DataField, AutoNetworkedField]
    public EntityUid? RaiseTotemActionEntity;

    [DataField]
    public EntProtoId TotemPrototype = "CMUBadBloodTotem";

    [DataField]
    public TimeSpan BuildTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A Totem may not stand this close to the Lodge or to another Totem, in tiles.
    /// </summary>
    [DataField]
    public float Spacing = 20f;

    /// <summary>
    /// A Totem may not stand this close to a dropship landing zone, in tiles.
    /// </summary>
    [DataField]
    public float LandingZoneDistance = 25f;
}

public sealed partial class BadBloodRaiseTotemActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class BadBloodRaiseTotemDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// A marine finished desecrating a Clan Totem (<see cref="Desecrate"/> true), or a Bad Blood finished
/// reconsecrating one (false). Started from the Totem's verb.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodTotemConsecrationDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool Desecrate;

    public BadBloodTotemConsecrationDoAfterEvent()
    {
    }

    public BadBloodTotemConsecrationDoAfterEvent(bool desecrate)
    {
        Desecrate = desecrate;
    }
}
