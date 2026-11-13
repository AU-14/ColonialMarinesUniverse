using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// A Clan Totem: the clan's hold on a piece of the map. While it stands and no marine is near it pays the clan
/// Superiority Points, more the longer it has stood, and Bad Bloods around it tire and hunger more slowly.
/// The clan's Totem list and limit are on the rule entity. This is the state of one Totem.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodTotemComponent : Component
{
    /// <summary>
    /// When it was raised. Income ripens by how long it has stood.
    /// </summary>
    [ViewVariables]
    public TimeSpan PlacedAt;

    /// <summary>
    /// The clan counts it: it was raised while the rule ran and the clan was under its limit.
    /// </summary>
    [ViewVariables]
    public bool Registered;

    /// <summary>
    /// A marine stands within <see cref="ContestRadius"/>. A contested Totem pays nothing.
    /// </summary>
    [ViewVariables]
    public bool Contested;

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
    /// A Totem that has stood this long is ripe. Ripe Totems held together call the clan ship early.
    /// </summary>
    [DataField]
    public TimeSpan RipeAfter = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Pending income is paid to the clan in steps of this size.
    /// </summary>
    [DataField]
    public FixedPoint4 PayoutStep = FixedPoint4.New(0.01);

    /// <summary>
    /// A living marine this close stops the income, in tiles.
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
